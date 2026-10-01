using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DanroJump.Bootstrap
{
    public sealed class WindowsLauncherLicenseChecker : IStartupLicenseChecker
    {
        private const uint WaitTimeout = 0x00000102;
        private readonly IStartupProtectionRuntime runtime;

        public WindowsLauncherLicenseChecker(IStartupProtectionRuntime runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public UniTask<StartupLicenseResult> CheckAsync(
            string launcherName,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return UniTask.RunOnThreadPool(
                () => CheckOnThread(launcherName, timeout),
                configureAwait: false,
                cancellationToken: cancellationToken);
#else
            return UniTask.FromResult(StartupLicenseResult.Failed("Windows launcher check is unavailable on this platform."));
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private StartupLicenseResult CheckOnThread(string launcherName, TimeSpan timeout)
        {
            string launcherPath = FindLauncherPath(launcherName);
            if (string.IsNullOrEmpty(launcherPath))
            {
                return StartupLicenseResult.Failed($"{launcherName} was not found.");
            }

            var startupInfo = new STARTUPINFO
            {
                cb = Marshal.SizeOf<STARTUPINFO>()
            };

            bool success = CreateProcess(
                launcherPath,
                null,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                0,
                IntPtr.Zero,
                Path.GetDirectoryName(launcherPath),
                ref startupInfo,
                out PROCESS_INFORMATION processInfo);

            if (!success)
            {
                return StartupLicenseResult.Failed($"Failed to start {launcherName}. Win32 error: {Marshal.GetLastWin32Error()}.");
            }

            try
            {
                uint waitMs = timeout.TotalMilliseconds >= uint.MaxValue
                    ? uint.MaxValue
                    : (uint)Math.Max(1d, timeout.TotalMilliseconds);
                uint waitResult = WaitForSingleObject(processInfo.hProcess, waitMs);

                if (waitResult == WaitTimeout)
                {
                    return StartupLicenseResult.Failed($"{launcherName} timed out.");
                }

                if (!GetExitCodeProcess(processInfo.hProcess, out uint exitCode))
                {
                    return StartupLicenseResult.Failed($"Failed to read {launcherName} exit code. Win32 error: {Marshal.GetLastWin32Error()}.");
                }

                return exitCode == 0
                    ? StartupLicenseResult.Valid($"{launcherName} returned success.")
                    : StartupLicenseResult.Failed($"{launcherName} returned exit code {exitCode}.");
            }
            finally
            {
                CloseHandle(processInfo.hProcess);
                CloseHandle(processInfo.hThread);
            }
        }

        private string FindLauncherPath(string launcherName)
        {
            string pathInBase = Path.Combine(runtime.BaseDirectory, launcherName);
            if (File.Exists(pathInBase))
            {
                return pathInBase;
            }

            string pathInStreamingAssets = Path.Combine(runtime.StreamingAssetsPath, launcherName);
            if (File.Exists(pathInStreamingAssets))
            {
                return pathInStreamingAssets;
            }

            return null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CreateProcess(
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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);
#endif
    }
}
