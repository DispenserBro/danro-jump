using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DanroJump.SceneFlow
{
    /// <summary>
    /// Централизует загрузку сцен, чтобы gameplay и UI не зависели напрямую от SceneManager.
    /// </summary>
    public sealed class SceneFlowService : ISceneFlowService, IDisposable
    {
        private const string DefaultMainMenuSceneName = "MainMenu";

        private CancellationTokenSource delayedTransitionCancellation;

        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return;
            }

            CancelDelayedTransition();
            if (!SceneExists(sceneName))
            {
                Debug.LogWarning($"Scene '{sceneName}' is not available in Build Settings.");
                return;
            }

            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        public void OpenMainMenu()
        {
            LoadScene(DefaultMainMenuSceneName);
        }

        public void StartGameplay(string sceneName)
        {
            LoadScene(sceneName);
        }

        public void OpenSettingsScene(string sceneName)
        {
            LoadScene(sceneName);
        }

        public void ReturnToMainMenuAfterGameOver(TimeSpan delay)
        {
            CancelDelayedTransition();
            delayedTransitionCancellation = new CancellationTokenSource();
            ReturnToMainMenuAfterDelayAsync(delay, delayedTransitionCancellation.Token).Forget(Debug.LogException);
        }

        public void CancelGameOverReturn()
        {
            CancelDelayedTransition();
        }

        public bool SceneExists(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                return false;
            }

            for (var index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                var scenePath = SceneUtility.GetScenePathByBuildIndex(index);
                var candidateName = Path.GetFileNameWithoutExtension(scenePath);

                if (candidateName == sceneName)
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            CancelDelayedTransition();
        }

        private async UniTask ReturnToMainMenuAfterDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.Delay(GetDelayMilliseconds(delay), DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    OpenMainMenu();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static int GetDelayMilliseconds(TimeSpan delay)
        {
            return Mathf.Max(0, Mathf.CeilToInt((float)delay.TotalMilliseconds));
        }

        private void CancelDelayedTransition()
        {
            if (delayedTransitionCancellation == null)
            {
                return;
            }

            delayedTransitionCancellation.Cancel();
            delayedTransitionCancellation.Dispose();
            delayedTransitionCancellation = null;
        }
    }
}
