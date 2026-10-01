using System;

namespace DanroJump.SceneFlow
{
    /// <summary>
    /// Единая точка переходов между сценами приложения.
    /// </summary>
    public interface ISceneFlowService
    {
        void LoadScene(string sceneName);

        void OpenMainMenu();

        void StartGameplay(string sceneName);

        void OpenSettingsScene(string sceneName);

        void ReturnToMainMenuAfterGameOver(TimeSpan delay);

        void CancelGameOverReturn();

        bool SceneExists(string sceneName);
    }
}
