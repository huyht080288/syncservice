using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Timers;
using Svc.Core;
using Svc.Shared;

namespace Svc
{
    public partial class SvcBackupService : ServiceBase
    {
        //private Timer _monitorTimer;
        private SyncEngine _syncEngine;
        private bool _isRunning = false;

        public SvcBackupService()
        {
            this.ServiceName = "SvcBackupService";
        }

        protected override void OnStart(string[] args)
        {
            Log("Service starting...");
            //_monitorTimer = new Timer(60000); // Kiểm tra mỗi 1 phút
            //_monitorTimer.Elapsed += (s, e) => CheckConfiguration();
            //_monitorTimer.Start();
            CheckConfiguration();

        }
        public void OnStart()
        {
            Log("Service starting...");
            //_monitorTimer = new Timer(60000); // Kiểm tra mỗi 1 phút
            //_monitorTimer.Elapsed += (s, e) => CheckConfiguration();
            //_monitorTimer.Start();
            CheckConfiguration();

        }

        private async void CheckConfiguration()
        {
            if (RegistryHelper.IsConfigured())
            {
                try
                {
                    if (!_isRunning)
                    {
                        Log("Configuration found.");
                        _isRunning = true;
                        //_monitorTimer.Stop(); // Dừng nhắc nhở

                        Log("Starting Sync Engine...");
                        _syncEngine = new SyncEngine();
                        await _syncEngine.InitializeAsync();
                        _syncEngine.Start();
                        Log("Started Sync Engine...");
                    }
                }
                catch (Exception ex)
                {
                    Log("Error starting engine: " + ex.Message);
                    _isRunning = false;
                    //_monitorTimer.Start();
                }
            }
            else
            {
                if (Process.GetProcessesByName("SvcSetup").Length == 0)
                {
                    Log("Not configured. Launching Setup App...");
                    InteractiveProcessHelper.StartProcessAsUser("SvcSetup.exe");
                }
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

        protected override void OnStop()
        {
            _syncEngine?.Stop();
            //_monitorTimer?.Stop();
            Log("Service stopped.");
        }
    }
}