using UnityEngine;
using Zenject;
using DanroJump.SceneFlow;

namespace DanroJump.Settings
{
    /// <summary>
    /// MonoBehaviour-фасад для вызова действий приложения из UI/Unity Events.
    /// </summary>
    public sealed class AppUIFacade : MonoBehaviour
    {
        private IServiceSettingsService settings;
        private ISceneFlowService sceneFlow;

        /// <summary>
        /// Получает сервис настроек через Extenject.
        /// </summary>
        [Inject]
        public void Construct(
            IServiceSettingsService injectedSettings,
            [InjectOptional] ISceneFlowService injectedSceneFlow = null)
        {
            settings = injectedSettings;
            sceneFlow = injectedSceneFlow;
        }

        /// <summary>
        /// Вручную назначает сервис настроек, если объект используется вне DI.
        /// </summary>
        public void Configure(IServiceSettingsService service, ISceneFlowService injectedSceneFlow = null)
        {
            settings = service;
            sceneFlow = injectedSceneFlow ?? sceneFlow;
        }

        /// <summary>
        /// Сохраняет настройки.
        /// </summary>
        public void Save()
        {
            settings?.Save();
        }

        /// <summary>
        /// Перечитывает настройки, разрешенные для изменения через внешний JSON.
        /// </summary>
        public void ReloadExternalChanges()
        {
            settings?.ReloadExternalChanges();
        }

        /// <summary>
        /// Повторно применяет текущие значения настроек.
        /// </summary>
        public void ApplyAll()
        {
            settings?.ApplyAll();
        }

        /// <summary>
        /// Сбрасывает все настройки к значениям по умолчанию.
        /// </summary>
        public void ResetAllToDefaults()
        {
            settings?.ResetAllToDefaults();
        }

        /// <summary>
        /// Сбрасывает одну группу настроек к значениям по умолчанию.
        /// </summary>
        public void ResetGroupToDefaults(string groupName)
        {
            settings?.ResetGroupToDefaults(groupName);
        }

        /// <summary>
        /// Открывает папку persistentDataPath с внешним JSON настроек.
        /// </summary>
        public void OpenPersistentDataFolder()
        {
            Application.OpenURL($"file://{Application.persistentDataPath}");
        }

        /// <summary>
        /// Загружает сцену по имени.
        /// </summary>
        public void LoadScene(string sceneName)
        {
            var resolvedSceneFlow = GetSceneFlow();
            if (!string.IsNullOrWhiteSpace(sceneName) && resolvedSceneFlow != null)
            {
                resolvedSceneFlow.LoadScene(sceneName);
            }
        }

        /// <summary>
        /// Возвращает приложение в главное меню.
        /// </summary>
        public void LoadMainMenu()
        {
            GetSceneFlow()?.OpenMainMenu();
        }

        private ISceneFlowService GetSceneFlow()
        {
            if (sceneFlow == null)
            {
                Debug.LogWarning($"{nameof(AppUIFacade)} requires {nameof(ISceneFlowService)} from DI.", this);
            }

            return sceneFlow;
        }

        /// <summary>
        /// Завершает приложение или останавливает Play Mode в редакторе.
        /// </summary>
        public void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
