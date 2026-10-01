using UnityEngine;

namespace DanroJump.Settings
{
    /// <summary>
    /// Автономный bootstrap-компонент сервисных настроек для сцен без ProjectContext installer.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class ServiceSettingsBehaviour : MonoBehaviour
    {
        [SerializeField] private string databaseAddress = ServiceSettingsAddressKeys.Database;
        [SerializeField] private ServiceSettingsDatabase fallbackDatabase;
        [SerializeField] private string fileName = "service-settings.json";
        [SerializeField] private bool loadOnAwake = true;
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool saveOnApplicationQuit = true;

        private static ServiceSettingsBehaviour instance;
        private IServiceSettingsDatabaseProvider databaseProvider;
        /// <summary>
        /// Созданный runtime-сервис настроек.
        /// </summary>
        public IServiceSettingsService Service { get; private set; }

        /// <summary>
        /// Создает сервис настроек и при необходимости загружает сохраненные значения.
        /// </summary>
        private void Awake()
        {
            if (dontDestroyOnLoad)
            {
                if (instance != null && instance != this)
                {
                    Debug.LogWarning(
                        $"[{nameof(ServiceSettingsBehaviour)}] Another persistent instance already exists. Destroying duplicate.",
                        this);
                    Destroy(gameObject);
                    return;
                }

                instance = this;
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }

            Service = CreateService();

            if (loadOnAwake)
            {
                Service?.Load();
            }
        }

        /// <summary>
        /// Сохраняет настройки при выходе из приложения.
        /// </summary>
        private void OnApplicationQuit()
        {
            if (saveOnApplicationQuit)
            {
                Service?.Save();
            }
        }

        /// <summary>
        /// Освобождает provider базы настроек.
        /// </summary>
        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            databaseProvider?.Dispose();
            databaseProvider = null;
        }

        /// <summary>
        /// Создает сервис настроек из Addressables-базы и JSON-репозитория.
        /// </summary>
        public IServiceSettingsService CreateService()
        {
            databaseProvider = new AddressableServiceSettingsDatabaseProvider(databaseAddress, fallbackDatabase);
            var database = databaseProvider.Load();

            if (database == null)
            {
                Debug.LogError($"[{nameof(ServiceSettingsBehaviour)}] Service settings database was not loaded.", this);
                return null;
            }

            return new ServiceSettingsService(
                database,
                new JsonServiceSettingsRepository(fileName));
        }
    }
}
