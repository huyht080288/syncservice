using System.ServiceProcess;

namespace Svc
{
    static class Program
    {
        /// <summary>
        /// Điểm khởi đầu chính cho ứng dụng.
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new SvcBackupService()
            };
            //var a = new SvcBackupService();
            //a.OnStart();
            ServiceBase.Run(ServicesToRun);
        }
    }
}