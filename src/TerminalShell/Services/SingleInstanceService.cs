using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace TerminalShell.Services
{
    public interface ISingleInstanceService : IDisposable
    {
        bool EnsureSingleInstance();
        void FocusExistingWindow(string windowTitle);
    }

    public class SingleInstanceService : ISingleInstanceService
    {
        private Mutex? _mutex;
        private readonly string _appName;
        private string _instanceScope = string.Empty;

        public SingleInstanceService(string appName = "TerminalShell")
        {
            _appName = appName;
        }

        public bool EnsureSingleInstance()
        {
            string exePath = GetCurrentProcessExecutablePath();
            _instanceScope = NormalizeInstanceScope(exePath, _appName);
            string mutexName = BuildMutexName(_instanceScope, _appName);

            _mutex = new Mutex(true, mutexName, out bool createdNew);
            return createdNew;
        }

        public void FocusExistingWindow(string windowTitle)
        {
            Process current = Process.GetCurrentProcess();
            string currentScope = string.IsNullOrWhiteSpace(_instanceScope)
                ? NormalizeInstanceScope(GetCurrentProcessExecutablePath(), _appName)
                : _instanceScope;

            foreach (var process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id != current.Id)
                {
                    string otherScope = NormalizeInstanceScope(GetProcessExecutablePath(process), _appName);
                    if (!string.Equals(otherScope, currentScope, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(process.MainWindowHandle, 9); // SW_RESTORE
                        SetForegroundWindow(process.MainWindowHandle);
                        return;
                    }
                }
            }
        }

        public void Dispose()
        {
            _mutex?.Dispose();
            _mutex = null;
        }

        internal static string NormalizeInstanceScope(string? executablePath, string appName)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return appName.ToLowerInvariant();
            }

            try
            {
                string? directory = Path.GetDirectoryName(executablePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return appName.ToLowerInvariant();
                }

                return Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToLowerInvariant();
            }
            catch
            {
                return appName.ToLowerInvariant();
            }
        }

        internal static string BuildMutexName(string instanceScope, string appName)
        {
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(instanceScope));
            return "Global\\" + appName + "_" + BitConverter.ToString(hash).Replace("-", "");
        }

        private static string GetCurrentProcessExecutablePath()
        {
            return Process.GetCurrentProcess().MainModule?.FileName
                ?? Environment.ProcessPath
                ?? string.Empty;
        }

        private static string? GetProcessExecutablePath(Process process)
        {
            try
            {
                return process.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }

        #region Win32 API
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        #endregion
    }
}
