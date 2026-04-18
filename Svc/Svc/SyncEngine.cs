using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Google.Apis.Drive.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Svc.Shared;
using System.Reflection;
using System.Data.SQLite;

namespace Svc.Core
{
    public class SyncEngine
    {
        private DriveService _driveService;
        private FileSystemWatcher _watcher;
        private string _localRoot;
        private string _driveRootId;

        // Timer để debounce sự kiện FileSystemWatcher (tránh upload 1 file nhiều lần)
        private readonly Dictionary<string, Timer> _debouncers = new Dictionary<string, Timer>();

        public async Task InitializeAsync()
        {
            try
            {
                _localRoot = RegistryHelper.GetConfig("LocalRootPath");
                _driveRootId = RegistryHelper.GetConfig("DriveRootFolderId");
                Log("_localRoot: " + _localRoot);
                Log("_driveRootId: " + _driveRootId);
                string clientId = RegistryHelper.GetConfig("ClientId");
                Log("clientId: " + (string.IsNullOrEmpty(clientId) ? "<missing>" : "<redacted>"));
                string clientSecret = RegistryHelper.GetConfig("ClientSecret");
                Log("clientSecret: " + (string.IsNullOrEmpty(clientSecret) ? "<missing>" : "<redacted>"));
                string tokenPath = RegistryHelper.GetConfig("TokenStorePath");
                Log("tokenPath: " + tokenPath);

                if (string.IsNullOrWhiteSpace(_localRoot)) throw new InvalidOperationException("LocalRootPath is not configured.");
                if (string.IsNullOrWhiteSpace(_driveRootId)) throw new InvalidOperationException("DriveRootFolderId is not configured.");
                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                    throw new InvalidOperationException("ClientId/ClientSecret not configured.");

                var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    new ClientSecrets { ClientId = clientId, ClientSecret = clientSecret },
                    new[] { DriveService.Scope.Drive },
                    "user",
                    System.Threading.CancellationToken.None,
                    new FileDataStore(tokenPath, true));

                _driveService = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Svc BK",
                });
                Log("Initialize: " + "STT");
                DatabaseHelper.Initialize();
                Log("Initialize: " + "END");
            }
            catch (Exception ex)
            {
                Log("Error InitializeAsync: " + ex.ToString());
                throw;
            }
        }

        private void Log(string message)
        {
            try
            {
                string logFile = @"C:\ProgramData\Svc\service.log";
                File.AppendAllText(logFile, $"{DateTime.Now}: {message}{Environment.NewLine}");
            }
            catch { }
        }

        public void Start()
        {
            if (string.IsNullOrWhiteSpace(_localRoot) || !Directory.Exists(_localRoot))
            {
                Log("Start aborted: Local root invalid or does not exist: " + _localRoot);
                return;
            }

            // 1. Thực hiện quét ban đầu (Initial Scan) trong một task riêng
            Task.Run(() => PerformInitialScanAsync(_localRoot));

            // 2. Cấu hình Watcher
            _watcher = new FileSystemWatcher(_localRoot);
            _watcher.IncludeSubdirectories = true;
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite;

            _watcher.Created += (s, e) => ScheduleSync(e.FullPath);
            _watcher.Changed += (s, e) => ScheduleSync(e.FullPath);
            _watcher.Deleted += (s, e) => Task.Run(() => HandleDeleteAsync(e.FullPath));
            _watcher.Renamed += (s, e) => Task.Run(() => HandleRenameAsync(e.OldFullPath, e.FullPath));

            _watcher.EnableRaisingEvents = true;
        }

        private void ScheduleSync(string path)
        {
            Log("ScheduleSync STT: " + path);
            lock (_debouncers)
            {
                if (_debouncers.ContainsKey(path))
                {
                    try
                    {
                        _debouncers[path].Stop();
                        _debouncers[path].Dispose();
                    }
                    catch { }
                    _debouncers.Remove(path);
                }

                var timer = new Timer(2000) { AutoReset = false };
                timer.Elapsed += (s, e) =>
                {
                    try
                    {
                        Task.Run(() => SyncFileOrFolder(path)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Log("Error scheduling SyncFileOrFolder: " + ex.ToString());
                    }
                    finally
                    {
                        lock (_debouncers)
                        {
                            if (_debouncers.ContainsKey(path))
                            {
                                try
                                {
                                    _debouncers[path].Dispose();
                                }
                                catch { }
                                _debouncers.Remove(path);
                            }
                        }
                    }
                };
                _debouncers[path] = timer;
                timer.Start();
            }
            Log("ScheduleSync END: " + path);
        }

        private async Task SyncFileOrFolder(string path)
        {
            Log("SyncFileOrFolder STT: " + path);
            try
            {
                if (Directory.Exists(path))
                {
                    Log("SyncFileOrFolder.EnsureDriveFolder STT: " + path);
                    await EnsureDriveFolder(path);
                    Log("SyncFileOrFolder.EnsureDriveFolder END: " + path);
                }
                else if (File.Exists(path))     
                {
                    Log("SyncFileOrFolder.UploadFile STT: " + path);
                    await UploadFile(path);
                    Log("SyncFileOrFolder.UploadFile END: " + path);
                }
            }
            catch (Exception ex)
            {
                Log("Error in SyncFileOrFolder for path " + path + ": " + ex.ToString());
                // consider retry logic for locked files (exponential backoff)
            }
            Log("SyncFileOrFolder END: " + path);
        }

        private async Task<string> EnsureDriveFolder(string localFolderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(localFolderPath)) throw new ArgumentNullException(nameof(localFolderPath));

                string normalizedLocalRoot = Path.GetFullPath(_localRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string normalizedPath = Path.GetFullPath(localFolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // If this folder already mapped
                string remoteId = DatabaseHelper.GetRemoteId(normalizedPath);
                if (!string.IsNullOrEmpty(remoteId)) return remoteId;

                // If this folder is the local root, return drive root id
                if (string.Equals(normalizedPath, normalizedLocalRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return _driveRootId;
                }

                // Determine parent
                string parentPath = Path.GetDirectoryName(normalizedPath);
                if (string.IsNullOrEmpty(parentPath))
                {
                    // Fallback: attach to drive root
                    parentPath = normalizedLocalRoot;
                }

                string parentId = string.Equals(parentPath, normalizedLocalRoot, StringComparison.OrdinalIgnoreCase)
                    ? _driveRootId
                    : await EnsureDriveFolder(parentPath);

                var folderMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = Path.GetFileName(normalizedPath),
                    MimeType = "application/vnd.google-apps.folder",
                    Parents = new List<string> { parentId }
                };

                var request = _driveService.Files.Create(folderMetadata);
                request.Fields = "id";
                var folder = await request.ExecuteAsync();

                DatabaseHelper.UpsertFile(normalizedPath, folder.Id, true, 0, Directory.GetLastWriteTime(normalizedPath));
                return folder.Id;
            }
            catch (Exception ex)
            {
                Log("Error EnsureDriveFolder for " + localFolderPath + ": " + ex.ToString());
                throw;
            }
        }

        private async Task UploadFile(string path)
        {
            try
            {
                Log("UploadFile: start: " + path);

                string fileName = Path.GetFileName(path);
                string parentPath = Path.GetDirectoryName(path);
                string parentId = await EnsureDriveFolder(parentPath);
                string existingId = DatabaseHelper.GetRemoteId(Path.GetFullPath(path)); // use full path for DB lookup

                var fileMetadata = new Google.Apis.Drive.v3.Data.File() { Name = fileName };
                if (string.IsNullOrEmpty(existingId))
                {
                    fileMetadata.Parents = new List<string> { parentId };
                    Log("UploadFile: creating new remote file under parentId=" + parentId);
                }
                else
                {
                    Log("UploadFile: updating existing remote file id=" + existingId);
                }

                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (!string.IsNullOrEmpty(existingId))
                    {
                        var updateRequest = _driveService.Files.Update(fileMetadata, existingId, stream, "application/octet-stream");
                        updateRequest.Fields = "id"; // ensure ResponseBody has id
                        var progress = await updateRequest.UploadAsync();
                        var updated = updateRequest.ResponseBody;
                        string usedId = updated?.Id ?? existingId;
                        DatabaseHelper.UpsertFile(Path.GetFullPath(path), usedId, false, new FileInfo(path).Length, File.GetLastWriteTime(path));
                        Log($"UploadFile: update completed for {path}, remoteId={usedId}, status={progress.Status}");
                    }
                    else
                    {
                        var createRequest = _driveService.Files.Create(fileMetadata, stream, "application/octet-stream");
                        createRequest.Fields = "id"; // MUST set fields to get ResponseBody.Id after upload
                        var progress = await createRequest.UploadAsync();
                        var createdFile = createRequest.ResponseBody;
                        string createdId = createdFile?.Id;
                        DatabaseHelper.UpsertFile(Path.GetFullPath(path), createdId, false, new FileInfo(path).Length, File.GetLastWriteTime(path));
                        Log($"UploadFile: create completed for {path}, remoteId={(createdId ?? "<null>")}, status={progress.Status}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Error UploadFile for " + path + ": " + ex.ToString());
            }
        }

        private async Task HandleDeleteAsync(string path)
        {
            try
            {
                string remoteId = DatabaseHelper.GetRemoteId(path);
                if (!string.IsNullOrEmpty(remoteId))
                {
                    try
                    {
                        await _driveService.Files.Delete(remoteId).ExecuteAsync();
                    }
                    catch (Exception ex)
                    {
                        Log("Drive delete failed for remoteId " + remoteId + ": " + ex.ToString());
                    }
                    DatabaseHelper.DeleteByPath(path);
                }
            }
            catch (Exception ex)
            {
                Log("Error HandleDeleteAsync for " + path + ": " + ex.ToString());
            }
        }

        private async Task HandleRenameAsync(string oldPath, string newPath)
        {
            try
            {
                string remoteId = DatabaseHelper.GetRemoteId(oldPath);
                if (!string.IsNullOrEmpty(remoteId))
                {
                    var fileMetadata = new Google.Apis.Drive.v3.Data.File() { Name = Path.GetFileName(newPath) };

                    try
                    {
                        await _driveService.Files.Update(fileMetadata, remoteId).ExecuteAsync();
                    }
                    catch (Exception ex)
                    {
                        Log("Drive rename/update failed for remoteId " + remoteId + ": " + ex.ToString());
                    }

                    // Update DB: the remoteId remains the same but the path must be updated
                    DatabaseHelper.UpdatePath(oldPath, newPath, remoteId);
                }
                else
                {
                    // If no remoteId for oldPath, this might be a new file; schedule sync for the newPath
                    ScheduleSync(newPath);
                }
            }
            catch (Exception ex)
            {
                Log("Error HandleRenameAsync from " + oldPath + " to " + newPath + ": " + ex.ToString());
            }
        }

        private async Task PerformInitialScanAsync(string path)
        {
            // Implement recursive scan and comparison with DB:
            // - Walk directory tree under _localRoot (Path.GetFullPath)
            // - For each folder: call EnsureDriveFolder
            // - For each file: compare LastWriteTime/length with DB and call UploadFile if changed or missing
            // - For any DB entries not present on disk: delete remote and remove DB entry

            const string dbPath = @"C:\ProgramData\Svc\file_map.db";
            try
            {
                string normalizedLocalRoot = Path.GetFullPath(_localRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                Log("PerformInitialScan: start for root: " + normalizedLocalRoot);

                using (var conn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
                {
                    conn.Open();

                    // Recursive directory scanner
                    async Task ScanDirAsync(string dir)
                    {
                        string normalizedDir = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (!visited.Add(normalizedDir)) return;

                        // Ensure folder mapping on Drive
                        try
                        {
                            await EnsureDriveFolder(normalizedDir);
                        }
                        catch (Exception ex)
                        {
                            Log("PerformInitialScan: EnsureDriveFolder failed for " + normalizedDir + ": " + ex.ToString());
                        }

                        string[] files = Array.Empty<string>();
                        string[] subdirs = Array.Empty<string>();
                        try
                        {
                            files = Directory.GetFiles(normalizedDir);
                            subdirs = Directory.GetDirectories(normalizedDir);
                        }
                        catch (Exception ex)
                        {
                            Log("PerformInitialScan: access error on " + normalizedDir + ": " + ex.ToString());
                        }

                        // Process files
                        foreach (var f in files)
                        {
                            string normalizedFile = Path.GetFullPath(f);
                            visited.Add(normalizedFile);

                            long fileSize = 0;
                            DateTime lastWrite = DateTime.MinValue;
                            try
                            {
                                var fi = new FileInfo(normalizedFile);
                                fileSize = fi.Length;
                                lastWrite = fi.LastWriteTime;
                            }
                            catch (Exception ex)
                            {
                                Log("PerformInitialScan: file info error for " + normalizedFile + ": " + ex.ToString());
                                continue;
                            }

                            // Query DB for this file
                            string dbRemoteId = null;
                            long dbSize = -1;
                            DateTime dbLastWrite = DateTime.MinValue;
                            using (var cmd = new SQLiteCommand("SELECT RemoteId, FileSize, LastWriteTime FROM FileMap WHERE LocalPath = @lp", conn))
                            {
                                cmd.Parameters.AddWithValue("@lp", normalizedFile);
                                using (var rdr = cmd.ExecuteReader())
                                {
                                    if (rdr.Read())
                                    {
                                        dbRemoteId = rdr.IsDBNull(0) ? null : rdr.GetString(0);
                                        dbSize = rdr.IsDBNull(1) ? -1 : (long)rdr.GetInt64(1);
                                        if (!rdr.IsDBNull(2))
                                        {
                                            DateTime parsed;
                                            if (DateTime.TryParse(rdr.GetString(2), out parsed)) dbLastWrite = parsed;
                                        }
                                    }
                                }
                            }

                            bool needUpload = false;
                            if (dbRemoteId == null)
                            {
                                needUpload = true;
                                Log("PerformInitialScan: file missing in DB, will upload: " + normalizedFile);
                            }
                            else if (dbSize != fileSize || dbLastWrite < lastWrite)
                            {
                                needUpload = true;
                                Log($"PerformInitialScan: file changed (size/lastWrite) - dbSize={dbSize}, fileSize={fileSize}, dbLastWrite={dbLastWrite:o}, fileLastWrite={lastWrite:o} -> will upload: {normalizedFile}");
                            }

                            if (needUpload)
                            {
                                try
                                {
                                    await UploadFile(normalizedFile);
                                }
                                catch (Exception ex)
                                {
                                    Log("PerformInitialScan: UploadFile failed for " + normalizedFile + ": " + ex.ToString());
                                }
                            }
                        }

                        // Recurse subdirectories
                        foreach (var d in subdirs)
                        {
                            await ScanDirAsync(d);
                        }
                    } // ScanDirAsync

                    // Start scan
                    await ScanDirAsync(normalizedLocalRoot);

                    // After scanning local FS, detect DB entries under root that were not visited => delete remote & DB entry
                    using (var cmd = new SQLiteCommand("SELECT LocalPath, RemoteId, IsFolder FROM FileMap WHERE LocalPath = @root OR LocalPath LIKE @sub", conn))
                    {
                        cmd.Parameters.AddWithValue("@root", normalizedLocalRoot);
                        cmd.Parameters.AddWithValue("@sub", normalizedLocalRoot + "\\%");
                        using (var rdr = cmd.ExecuteReader())
                        {
                            var toRemove = new List<string>();
                            while (rdr.Read())
                            {
                                string lp = rdr.IsDBNull(0) ? null : rdr.GetString(0);
                                string rid = rdr.IsDBNull(1) ? null : rdr.GetString(1);
                                bool isFolder = !rdr.IsDBNull(2) && rdr.GetInt32(2) == 1;
                                if (string.IsNullOrEmpty(lp)) continue;
                                string normLp = Path.GetFullPath(lp).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                                if (!visited.Contains(normLp))
                                {
                                    Log("PerformInitialScan: DB entry not present on disk: " + normLp);
                                    if (!string.IsNullOrEmpty(rid))
                                    {
                                        try
                                        {
                                            await _driveService.Files.Delete(rid).ExecuteAsync();
                                            Log("PerformInitialScan: deleted remote id " + rid + " for " + normLp);
                                        }
                                        catch (Exception ex)
                                        {
                                            Log("PerformInitialScan: failed to delete remote id " + rid + " for " + normLp + ": " + ex.ToString());
                                        }
                                    }
                                    toRemove.Add(normLp);
                                }
                            }

                            // Remove from DB (use DeleteByPath which handles subpaths)
                            foreach (var rem in toRemove)
                            {
                                try
                                {
                                    DatabaseHelper.DeleteByPath(rem);
                                    Log("PerformInitialScan: removed DB entry for " + rem);
                                }
                                catch (Exception ex)
                                {
                                    Log("PerformInitialScan: failed to remove DB entry for " + rem + ": " + ex.ToString());
                                }
                            }
                        }
                    }
                } // using conn

                Log("PerformInitialScan: completed for root: " + normalizedLocalRoot);
            }
            catch (Exception ex)
            {
                Log("Error PerformInitialScan: " + ex.ToString());
            }
        }

        public void Stop()
        {
            try
            {
                _watcher?.Dispose();
            }
            catch { }

            lock (_debouncers)
            {
                foreach (var kv in _debouncers)
                {
                    try
                    {
                        kv.Value.Stop();
                        kv.Value.Dispose();
                    }
                    catch { }
                }
                _debouncers.Clear();
            }
        }
    }
}