using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DanroJump.Bootstrap
{
    public sealed class UnityStartupProtectionRuntime : IStartupProtectionRuntime
    {
        public bool IsEditor => Application.isEditor;
        public bool IsDevelopmentBuild => Debug.isDebugBuild;
        public string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;
        public string StreamingAssetsPath => Application.streamingAssetsPath;

        public void ApplyLegacyStartupPresentation(StartupProtectionOptions options)
        {
            Screen.fullScreenMode = options.LegacyFullScreenMode;
            Screen.SetResolution(options.LegacyTargetWidth, options.LegacyTargetHeight, options.LegacyFullScreenMode);

            if (options.FocusWindowOnSuccess)
            {
                ForceToFront();
            }

            if (options.ClickCenterOnSuccess)
            {
                ClickCenter();
            }
        }

        public void QuitApplication()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void Log(string message)
        {
            Debug.Log(message);
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        public void LogError(string message)
        {
            Debug.LogError(message);
        }

        public void LogException(Exception exception)
        {
            Debug.LogException(exception);
        }

        private static void ForceToFront()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IntPtr handle = GetActiveWindow();
            if (handle != IntPtr.Zero)
            {
                SetForegroundWindow(handle);
            }
#endif
        }

        private static void ClickCenter()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            int screenWidth = Screen.currentResolution.width;
            int screenHeight = Screen.currentResolution.height;
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return;
            }

            int absoluteX = (int)(screenWidth * 0.5f * 65535 / screenWidth);
            int absoluteY = (int)(screenHeight * 0.5f * 65535 / screenHeight);

            INPUT[] inputs =
            {
                new INPUT
                {
                    type = InputMouse,
                    mi = new MOUSEINPUT
                    {
                        dx = absoluteX,
                        dy = absoluteY,
                        dwFlags = MouseEventfMove | MouseEventfAbsolute
                    }
                },
                new INPUT
                {
                    type = InputMouse,
                    mi = new MOUSEINPUT
                    {
                        dwFlags = MouseEventfLeftDown
                    }
                },
                new INPUT
                {
                    type = InputMouse,
                    mi = new MOUSEINPUT
                    {
                        dwFlags = MouseEventfLeftUp
                    }
                }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint InputMouse = 0;
        private const uint MouseEventfMove = 0x0001;
        private const uint MouseEventfAbsolute = 0x8000;
        private const uint MouseEventfLeftDown = 0x0002;
        private const uint MouseEventfLeftUp = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
#endif
    }
}
