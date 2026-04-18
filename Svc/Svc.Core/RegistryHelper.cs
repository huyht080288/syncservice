using System;
using System.Data.SQLite;
using System.IO;
using Microsoft.Win32;

namespace Svc.Shared
{
    // Lớp quản lý Registry
    public static class RegistryHelper
    {
        private const string RegistryPath = @"SOFTWARE\SvcBK";

        public static void SaveConfig(string localPath, string driveId, string clientId, string clientSecret)
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey key = baseKey.CreateSubKey(RegistryPath))
                {
                    if (key != null)
                    {
                        key.SetValue("LocalRootPath", localPath);
                        key.SetValue("DriveRootFolderId", driveId);
                        key.SetValue("ClientId", clientId);
                        key.SetValue("ClientSecret", clientSecret);
                        key.SetValue("TokenStorePath", @"C:\ProgramData\Svc\Tokens");
                        key.SetValue("IsConfigured", 1, RegistryValueKind.DWord);
                    }
                }
            }
        }

        public static string GetConfig(string name)
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey key = baseKey.OpenSubKey(RegistryPath))
                {
                    return key?.GetValue(name)?.ToString();
                }
            }
        }

        public static bool IsConfigured()
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            {
                using (RegistryKey key = baseKey.OpenSubKey(RegistryPath))
                {
                    if (key == null) return false;
                    var val = key.GetValue("IsConfigured");
                    return val != null && Convert.ToInt32(val) == 1;
                }
            }
        }
    }

    // Lớp quản lý SQLite dựa trên schema trong implementation_details.md
    public static class DatabaseHelper
    {
        private static string DbPath = @"C:\ProgramData\Svc\file_map.db";
        private static string ConnStr => $"Data Source={DbPath};Version=3;";

        public static void Initialize()
        {
            if (!Directory.Exists(@"C:\ProgramData\Svc")) Directory.CreateDirectory(@"C:\ProgramData\Svc");

            if (!File.Exists(DbPath))
            {
                SQLiteConnection.CreateFile(DbPath);
                using (var conn = new SQLiteConnection(ConnStr))
                {
                    conn.Open();
                    string sql = @"
                        CREATE TABLE FileMap (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
                            LocalPath TEXT UNIQUE,
                            RemoteId TEXT,
                            IsFolder INTEGER,
                            LastWriteTime TEXT,
                            FileSize INTEGER,
                            SyncStatus INTEGER DEFAULT 1,
                            MD5Hash TEXT
                        );
                        CREATE INDEX idx_localpath ON FileMap(LocalPath);";
                    using (var cmd = new SQLiteCommand(sql, conn)) cmd.ExecuteNonQuery();
                }
            }
        }

        public static string GetRemoteId(string localPath)
        {
            using (var conn = new SQLiteConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("SELECT RemoteId FROM FileMap WHERE LocalPath = @path", conn))
                {
                    cmd.Parameters.AddWithValue("@path", localPath);
                    return cmd.ExecuteScalar()?.ToString();
                }
            }
        }

        public static void UpsertFile(string localPath, string remoteId, bool isFolder, long size, DateTime lastWrite)
        {
            using (var conn = new SQLiteConnection(ConnStr))
            {
                conn.Open();
                string sql = @"INSERT OR REPLACE INTO FileMap (LocalPath, RemoteId, IsFolder, LastWriteTime, FileSize, SyncStatus) 
                               VALUES (@lp, @ri, @if, @lwt, @fs, 1)";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@lp", localPath);
                    cmd.Parameters.AddWithValue("@ri", remoteId);
                    cmd.Parameters.AddWithValue("@if", isFolder ? 1 : 0);
                    cmd.Parameters.AddWithValue("@lwt", lastWrite.ToString("o"));
                    cmd.Parameters.AddWithValue("@fs", size);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void DeleteByPath(string localPath)
        {
            using (var conn = new SQLiteConnection(ConnStr))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand("DELETE FROM FileMap WHERE LocalPath = @path OR LocalPath LIKE @sub", conn))
                {
                    cmd.Parameters.AddWithValue("@path", localPath);
                    cmd.Parameters.AddWithValue("@sub", localPath + "\\%");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // Thêm phương thức UpdatePath để cập nhật đường dẫn khi đổi tên/move
        // - oldPath/newPath sẽ được chuẩn hóa bằng Path.GetFullPath và trim separator
        // - Cập nhật tất cả các bản ghi có LocalPath = oldPath hoặc bắt đầu bằng oldPath + '\'
        // - Thực hiện trong transaction, cập nhật các entry con trước (độ dài giảm dần) để tránh xung đột UNIQUE
        // - Nếu target path đã tồn tại trong DB sẽ ném InvalidOperationException (được caller bắt/log)
        public static void UpdatePath(string oldPath, string newPath, string remoteId)
        {
            if (string.IsNullOrWhiteSpace(oldPath)) throw new ArgumentNullException(nameof(oldPath));
            if (string.IsNullOrWhiteSpace(newPath)) throw new ArgumentNullException(nameof(newPath));

            string normOld = Path.GetFullPath(oldPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normNew = Path.GetFullPath(newPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            using (var conn = new SQLiteConnection(ConnStr))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    // Lấy danh sách các LocalPath cần cập nhật
                    using (var sel = new SQLiteCommand("SELECT LocalPath FROM FileMap WHERE LocalPath = @old OR LocalPath LIKE @oldSub", conn, tx))
                    {
                        sel.Parameters.AddWithValue("@old", normOld);
                        sel.Parameters.AddWithValue("@oldSub", normOld + "\\%");
                        var paths = new System.Collections.Generic.List<string>();
                        using (var rdr = sel.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                paths.Add(rdr.GetString(0));
                            }
                        }

                        // Cập nhật con trước (giảm dần theo độ dài) để tránh vi phạm UNIQUE
                        paths.Sort((a, b) => b.Length.CompareTo(a.Length));

                        foreach (var lp in paths)
                        {
                            string mapped;
                            if (string.Equals(lp, normOld, StringComparison.OrdinalIgnoreCase))
                            {
                                mapped = normNew;
                            }
                            else
                            {
                                // lp bắt đầu bằng normOld (bao gồm dấu '\'), giữ phần còn lại
                                mapped = normNew + lp.Substring(normOld.Length);
                            }

                            // Kiểm tra xung đột target tồn tại
                            using (var chk = new SQLiteCommand("SELECT COUNT(1) FROM FileMap WHERE LocalPath = @newp", conn, tx))
                            {
                                chk.Parameters.AddWithValue("@newp", mapped);
                                long cnt = (long)chk.ExecuteScalar();
                                if (cnt > 0)
                                {
                                    throw new InvalidOperationException($"UpdatePath conflict: target path already exists in DB: {mapped}");
                                }
                            }

                            // Cập nhật bản ghi. Nếu là bản ghi chính (exact match) thì cập nhật RemoteId nếu được truyền vào.
                            if (string.Equals(lp, normOld, StringComparison.OrdinalIgnoreCase))
                            {
                                using (var upd = new SQLiteCommand("UPDATE FileMap SET LocalPath = @newp, RemoteId = @rid WHERE LocalPath = @oldp", conn, tx))
                                {
                                    upd.Parameters.AddWithValue("@newp", mapped);
                                    upd.Parameters.AddWithValue("@rid", remoteId ?? (object)DBNull.Value);
                                    upd.Parameters.AddWithValue("@oldp", lp);
                                    upd.ExecuteNonQuery();
                                }
                            }
                            else
                            {
                                using (var upd = new SQLiteCommand("UPDATE FileMap SET LocalPath = @newp WHERE LocalPath = @oldp", conn, tx))
                                {
                                    upd.Parameters.AddWithValue("@newp", mapped);
                                    upd.Parameters.AddWithValue("@oldp", lp);
                                    upd.ExecuteNonQuery();
                                }
                            }
                        }
                    }

                    tx.Commit();
                }
            }
        }
    }
}