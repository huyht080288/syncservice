using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;

namespace Svc
{
    /// <summary>
    /// Giúp Windows Service (Session 0) có thể khởi chạy ứng dụng GUI trên màn hình User.
    /// Tương thích hoàn toàn với .NET Framework 4.8.
    /// </summary>
    public static class InteractiveProcessHelper
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hSnapshot);

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr Token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        public static void StartProcessAsUser(string appFileName)
        {
            IntPtr userToken = IntPtr.Zero;
            try
            {
                uint sessionId = WTSGetActiveConsoleSessionId();
                if (sessionId == 0xFFFFFFFF) return; // Không có session người dùng nào active

                if (WTSQueryUserToken(sessionId, out userToken))
                {
                    STARTUPINFO si = new STARTUPINFO();
                    si.cb = Marshal.SizeOf(si);
                    si.lpDesktop = @"winsta0\default"; // Buộc hiển thị trên màn hình desktop người dùng

                    PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

                    // Lấy đường dẫn tuyệt đối của SvcConfig.exe nằm cùng thư mục với Service
                    string appPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, appFileName);

                    if (!File.Exists(appPath)) return;

                    bool result = CreateProcessAsUser(
                        userToken,
                        appPath,
                        null,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        0,
                        IntPtr.Zero,
                        null,
                        ref si,
                        out pi);

                    if (result)
                    {
                        if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                        if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);
                    }
                }
            }
            catch (Exception)
            {
                // Xử lý log lỗi nếu cần
            }
            finally
            {
                if (userToken != IntPtr.Zero) CloseHandle(userToken);
            }
        }
    }
}