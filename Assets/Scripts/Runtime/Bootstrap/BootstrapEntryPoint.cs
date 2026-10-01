using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using DanroJump.Debugging;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace DanroJump.Bootstrap
{
    /// <summary>
    /// Первая точка входа приложения: фиксирует портретное соотношение сторон и загружает главное меню.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class BootstrapEntryPoint : MonoBehaviour
    {
        private const float AspectTolerance = 0.0025f;
        private const string GameLogFileName = "game.log";
        private static BootstrapEntryPoint instance;

        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private bool forceBuildAspectRatio = true;
        [SerializeField] [Min(1)] private int targetAspectWidth = 9;
        [SerializeField] [Min(1)] private int targetAspectHeight = 16;
        [SerializeField] [Min(1)] private int preferredPortraitHeight = 1920;
        [SerializeField] private bool clampToDisplaySize = true;
        [SerializeField] private FullScreenMode buildFullScreenMode = FullScreenMode.Windowed;
        [SerializeField] [Min(1)] private int startupEnforcementFrames = 8;
        [SerializeField] [Min(0.1f)] private float aspectCheckInterval = 0.5f;

        private float nextAspectCheckTime;
        private InputAction fullscreenToggleAction;
        private FileLogger fileLogger;

        /// <summary>
        /// Создает singleton bootstrap-объект и применяет разрешение билда до загрузки меню.
        /// </summary>
        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            // В редакторе не вмешиваемся в Game View, а в билде принудительно держим 9:16.
            if (!Application.isEditor && forceBuildAspectRatio)
            {
                ApplyBuildAspectResolution();
                EnforceBuildAspectOnStartupAsync(this.GetCancellationTokenOnDestroy()).Forget(Debug.LogException);
            }

            // Инициализируем файловый логгер — пишем game.log в корень папки игры.
            InitFileLogger();
        }

        /// <summary>
        /// Регистрирует InputAction для переключения полноэкранного режима по F11.
        /// </summary>
        private void OnEnable()
        {
            fullscreenToggleAction = new InputAction("FullscreenToggle", InputActionType.Button, "<Keyboard>/f11");
            fullscreenToggleAction.performed += OnFullscreenTogglePerformed;
            fullscreenToggleAction.Enable();
        }

        /// <summary>
        /// Снимает подписку и отключает InputAction при деактивации объекта.
        /// </summary>
        private void OnDisable()
        {
            if (fullscreenToggleAction != null)
            {
                fullscreenToggleAction.performed -= OnFullscreenTogglePerformed;
                fullscreenToggleAction.Disable();
                fullscreenToggleAction.Dispose();
                fullscreenToggleAction = null;
            }
        }

        private void OnFullscreenTogglePerformed(InputAction.CallbackContext context)
        {
            ToggleFullscreen();
        }

        /// <summary>
        /// Периодически возвращает разрешение к целевому aspect ratio, если окно было изменено.
        /// </summary>
        private void Update()
        {
            if (Application.isEditor || !forceBuildAspectRatio || Time.unscaledTime < nextAspectCheckTime)
            {
                return;
            }

            nextAspectCheckTime = Time.unscaledTime + aspectCheckInterval;

            if (!HasTargetAspectRatio(Screen.width, Screen.height))
            {
                ApplyBuildAspectResolution();
            }
        }

        /// <summary>
        /// Переключает полноэкранный режим по нажатию F11.
        /// В билде учитывает целевой aspect ratio и разрешение монитора.
        /// </summary>
        private void ToggleFullscreen()
        {
            bool isCurrentlyFullscreen = Screen.fullScreen;

            if (isCurrentlyFullscreen)
            {
                // Выход из полноэкранного режима — переходим в Windowed с aspect-корректным разрешением
                Vector2Int resolution = CalculateTargetResolution();
                Screen.SetResolution(resolution.x, resolution.y, FullScreenMode.Windowed);
            }
            else
            {
                // Вход в полноэкранный режим — используем native разрешение монитора
                Resolution native = Screen.currentResolution;
                FullScreenMode targetMode = FullScreenMode.FullScreenWindow;
#if UNITY_STANDALONE_WIN
                // На Windows ExclusiveFullScreen даёт минимальный input lag
                targetMode = FullScreenMode.FullScreenWindow;
#endif
                Screen.SetResolution(native.width, native.height, targetMode);
            }
        }

        /// <summary>
        /// Загружает сцену главного меню после bootstrap-настройки.
        /// </summary>
        private void Start()
        {
            LoadMainMenuAsync(this.GetCancellationTokenOnDestroy()).Forget(Debug.LogException);
        }

        private async UniTask LoadMainMenuAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (StartupProtectionController.HasInstance)
                {
                    StartupProtectionState protectionState =
                        await StartupProtectionController.WaitUntilFinishedAsync(cancellationToken);
                    if (protectionState == StartupProtectionState.Failed)
                    {
                        Debug.LogError("[BootstrapEntryPoint] Startup protection failed. Main menu loading was blocked.", this);
                        return;
                    }
                }
                else
                {
                    Debug.LogWarning("[BootstrapEntryPoint] Startup protection controller is missing in Bootstrap scene.", this);
                }

                StartupWindowFocusController startupWindowFocus =
                    FindAnyObjectByType<StartupWindowFocusController>();
                if (startupWindowFocus != null)
                {
                    await startupWindowFocus.RunStartupFocusAsync(cancellationToken);
                }

                if (string.IsNullOrWhiteSpace(mainMenuSceneName))
                {
                    Debug.LogError("[BootstrapEntryPoint] Main menu scene name is empty.", this);
                    return;
                }

                if (!SceneExistsInBuildSettings(mainMenuSceneName))
                {
                    Debug.LogError($"[BootstrapEntryPoint] Scene '{mainMenuSceneName}' is not registered in Build Settings.", this);
                    return;
                }

                AsyncOperation operation = SceneManager.LoadSceneAsync(mainMenuSceneName, LoadSceneMode.Single);
                if (operation == null)
                {
                    Debug.LogError($"[BootstrapEntryPoint] Failed to start loading scene '{mainMenuSceneName}'.", this);
                    return;
                }

                while (!operation.isDone)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Очищает singleton-ссылку при выходе из Play Mode или уничтожении bootstrap-объекта.
        /// </summary>
        private void OnDestroy()
        {
            DisposeFileLogger();

            if (instance == this)
            {
                instance = null;
            }
        }

        /// <summary>
        /// Создаёт FileLogger и подписывает его на все сообщения Unity Debug.Log.
        /// В билде лог пишется рядом с .exe, в редакторе — в корень проекта.
        /// </summary>
        private void InitFileLogger()
        {
            if (fileLogger != null)
            {
                return;
            }

            try
            {
                string logDirectory = ResolveGameRootDirectory();
                string logPath = Path.Combine(logDirectory, GameLogFileName);
                fileLogger = new FileLogger(logPath, append: true);
                Application.logMessageReceived += fileLogger.HandleUnityLog;
                Debug.Log($"[BootstrapEntryPoint] Game log writes to: {fileLogger.FilePath}", this);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[BootstrapEntryPoint] Failed to init FileLogger: {ex.Message}");
            }
        }

        private static string ResolveGameRootDirectory()
        {
            string dataPath = Application.dataPath;
            var dataDirectory = new DirectoryInfo(dataPath);

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            return dataDirectory.Parent?.Parent?.FullName ?? Path.GetFullPath(".");
#else
            return dataDirectory.Parent?.FullName ?? Path.GetFullPath(".");
#endif
        }

        /// <summary>
        /// Корректно отписывается и освобождает FileLogger.
        /// </summary>
        private void DisposeFileLogger()
        {
            if (fileLogger == null)
            {
                return;
            }

            Application.logMessageReceived -= fileLogger.HandleUnityLog;
            fileLogger.Dispose();
            fileLogger = null;
        }

        /// <summary>
        /// Несколько кадров подряд повторяет установку разрешения, пока Unity завершает старт окна.
        /// </summary>
        private async UniTask EnforceBuildAspectOnStartupAsync(CancellationToken cancellationToken)
        {
            try
            {
                for (int i = 0; i < startupEnforcementFrames; i++)
                {
                    if (!HasTargetAspectRatio(Screen.width, Screen.height))
                    {
                        ApplyBuildAspectResolution();
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Применяет рассчитанное разрешение к окну игры.
        /// </summary>
        private void ApplyBuildAspectResolution()
        {
            Vector2Int resolution = CalculateTargetResolution();
            FullScreenMode mode = Debug.isDebugBuild ? buildFullScreenMode : FullScreenMode.FullScreenWindow;
            Screen.SetResolution(resolution.x, resolution.y, mode);
        }

        /// <summary>
        /// Рассчитывает ближайшее разрешение с целевым aspect ratio, сохраняя пользовательский размер окна.
        /// </summary>
        private Vector2Int CalculateTargetResolution()
        {
            var targetAspect = GetTargetAspect();
            var currentWidth = Mathf.Max(1, Screen.width);
            var currentHeight = Mathf.Max(1, Screen.height);
            var widthByCurrentHeight = Mathf.Max(1, Mathf.RoundToInt(currentHeight * targetAspect));
            var heightByCurrentWidth = Mathf.Max(1, Mathf.RoundToInt(currentWidth / targetAspect));
            var keepHeightDelta = Mathf.Abs(widthByCurrentHeight - currentWidth);
            var keepWidthDelta = Mathf.Abs(heightByCurrentWidth - currentHeight);

            var width = currentWidth;
            var height = currentHeight;
            if (currentWidth <= 1 || currentHeight <= 1)
            {
                height = Mathf.Max(1, preferredPortraitHeight);
                width = Mathf.Max(1, Mathf.RoundToInt(height * targetAspect));
            }
            else if (keepHeightDelta <= keepWidthDelta)
            {
                width = widthByCurrentHeight;
            }
            else
            {
                height = heightByCurrentWidth;
            }

            if (clampToDisplaySize && Display.main != null)
            {
                int displayWidth = Display.main.systemWidth;
                int displayHeight = Display.main.systemHeight;

                if (displayHeight > 0 && height > displayHeight)
                {
                    height = displayHeight;
                    width = Mathf.Max(1, Mathf.RoundToInt(height * targetAspect));
                }

                if (displayWidth > 0 && width > displayWidth)
                {
                    width = displayWidth;
                    height = Mathf.Max(1, Mathf.RoundToInt(width / targetAspect));
                }
            }

            return new Vector2Int(width, height);
        }

        /// <summary>
        /// Проверяет, совпадает ли текущее разрешение с целевым соотношением сторон.
        /// </summary>
        private bool HasTargetAspectRatio(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            return Mathf.Abs(width / (float)height - GetTargetAspect()) <= AspectTolerance;
        }

        /// <summary>
        /// Возвращает целевой aspect ratio из настроек ширины и высоты.
        /// </summary>
        private float GetTargetAspect()
        {
            return targetAspectWidth / (float)targetAspectHeight;
        }

        /// <summary>
        /// Проверяет, добавлена ли сцена в Build Settings.
        /// </summary>
        private static bool SceneExistsInBuildSettings(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string buildSceneName = Path.GetFileNameWithoutExtension(scenePath);

                if (buildSceneName == sceneName)
                    return true;
            }

            return false;
        }
    }
}
