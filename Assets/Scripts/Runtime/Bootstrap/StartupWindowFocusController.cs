using System;
using System.Runtime.InteropServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DanroJump.Bootstrap
{
    /// <summary>
    /// Возвращает фокус окну игры при старте приложения и делает клик по центру окна.
    /// </summary>
    [DefaultExecutionOrder(-12000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Bootstrap/Startup Window Focus Controller")]
    public sealed class StartupWindowFocusController : MonoBehaviour
    {
        private static StartupWindowFocusController instance;

        [SerializeField] private bool clickCenterOnStartup = true;
        [SerializeField] [Min(0)] private int startupDelayFrames = 2;
        [SerializeField] [Min(0f)] private float postClickSettleSeconds = 0.2f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            MoveToPersistentScene(gameObject);
        }

        public async UniTask RunStartupFocusAsync(CancellationToken cancellationToken)
        {
            if (!clickCenterOnStartup)
            {
                FocusGameWindow();
                return;
            }

            await ClickCenterOnStartupAsync(cancellationToken);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private static void MoveToPersistentScene(GameObject gameObject)
        {
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public static void FocusGameWindow()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IntPtr handle = GetActiveWindow();
            if (handle != IntPtr.Zero)
            {
                SetForegroundWindow(handle);
            }
#endif
        }

        private async UniTask ClickCenterOnStartupAsync(CancellationToken cancellationToken)
        {
            try
            {
                for (int frame = 0; frame < startupDelayFrames; frame++)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                FocusGameWindow();
                ClickCenter();

                if (postClickSettleSeconds > 0f)
                {
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(postClickSettleSeconds),
                        DelayType.Realtime,
                        PlayerLoopTiming.Update,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static void ClickCenter()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IntPtr handle = GetActiveWindow();
            if (!TryGetWindowCenter(handle, out int x, out int y))
            {
                x = Mathf.RoundToInt(Screen.currentResolution.width * 0.5f);
                y = Mathf.RoundToInt(Screen.currentResolution.height * 0.5f);
            }

            MoveCursorToScreenPoint(x, y);
            INPUT[] inputs =
            {
                new INPUT
                {
                    type = InputMouse,
                    mi = new MOUSEINPUT { dwFlags = MouseEventfLeftDown }
                },
                new INPUT
                {
                    type = InputMouse,
                    mi = new MOUSEINPUT { dwFlags = MouseEventfLeftUp }
                }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
#endif
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const int SystemMetricVirtualScreenX = 76;
        private const int SystemMetricVirtualScreenY = 77;
        private const int SystemMetricVirtualScreenWidth = 78;
        private const int SystemMetricVirtualScreenHeight = 79;
        private const uint InputMouse = 0;
        private const uint MouseEventfMove = 0x0001;
        private const uint MouseEventfAbsolute = 0x8000;
        private const uint MouseEventfLeftDown = 0x0002;
        private const uint MouseEventfLeftUp = 0x0004;

        private static bool TryGetWindowCenter(IntPtr handle, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (handle == IntPtr.Zero || !GetWindowRect(handle, out RECT rect))
            {
                return false;
            }

            x = rect.Left + Mathf.Max(0, rect.Right - rect.Left) / 2;
            y = rect.Top + Mathf.Max(0, rect.Bottom - rect.Top) / 2;
            return true;
        }

        private static void MoveCursorToScreenPoint(int x, int y)
        {
            int virtualX = GetSystemMetrics(SystemMetricVirtualScreenX);
            int virtualY = GetSystemMetrics(SystemMetricVirtualScreenY);
            int virtualWidth = Mathf.Max(1, GetSystemMetrics(SystemMetricVirtualScreenWidth));
            int virtualHeight = Mathf.Max(1, GetSystemMetrics(SystemMetricVirtualScreenHeight));
            int absoluteX = Mathf.RoundToInt((x - virtualX) * 65535f / virtualWidth);
            int absoluteY = Mathf.RoundToInt((y - virtualY) * 65535f / virtualHeight);

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
                }
            };

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

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
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
#endif
    }
}
