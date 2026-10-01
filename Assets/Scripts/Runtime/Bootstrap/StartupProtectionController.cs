using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DanroJump.Bootstrap
{
    /// <summary>
    /// Запускает проверку защиты на уровне приложения до загрузки первых сцен.
    /// </summary>
    [DefaultExecutionOrder(-12000)]
    public sealed class StartupProtectionController : MonoBehaviour
    {
        private static StartupProtectionController instance;

        [SerializeField] private StartupProtectionOptions options = new StartupProtectionOptions();

        private CancellationTokenSource lifetimeCancellation;

        public static StartupProtectionState CurrentState { get; private set; } = StartupProtectionState.NotStarted;

        public static bool HasInstance => instance != null;

        public static async UniTask<StartupProtectionState> WaitUntilFinishedAsync(CancellationToken cancellationToken)
        {
            while (!IsFinished(CurrentState))
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            return CurrentState;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            MoveToPersistentScene(gameObject);

            CurrentState = StartupProtectionState.NotStarted;
            lifetimeCancellation = new CancellationTokenSource();
            RunStartupProtectionAsync(lifetimeCancellation.Token).Forget(Debug.LogException);
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
        }

        private static void MoveToPersistentScene(GameObject gameObject)
        {
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private static bool IsFinished(StartupProtectionState state)
        {
            return state == StartupProtectionState.Skipped
                || state == StartupProtectionState.Licensed
                || state == StartupProtectionState.Failed;
        }

        private async UniTask RunStartupProtectionAsync(CancellationToken cancellationToken)
        {
            var runtime = new UnityStartupProtectionRuntime();
            var checker = new WindowsLauncherLicenseChecker(runtime);
            var service = new StartupProtectionService(options, checker, runtime);

            CurrentState = await service.RunAsync(cancellationToken);
        }
    }
}
