using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DanroJump.Settings
{
    /// <summary>
    /// Загружает базу сервисных настроек из прямой ссылки или через Addressables.
    /// </summary>
    public sealed class AddressableServiceSettingsDatabaseProvider : IServiceSettingsDatabaseProvider
    {
        private readonly string address;
        private readonly ServiceSettingsDatabase fallbackDatabase;
        private AsyncOperationHandle<ServiceSettingsDatabase>? loadedHandle;

        /// <summary>
        /// Создает provider для addressable-базы настроек.
        /// </summary>
        public AddressableServiceSettingsDatabaseProvider(
            string address,
            ServiceSettingsDatabase fallbackDatabase = null)
        {
            this.address = address;
            this.fallbackDatabase = fallbackDatabase;
        }

        /// <summary>
    /// Возвращает прямую fallback-базу без блокировки Addressables или загружает базу по адресу.
        /// </summary>
        public ServiceSettingsDatabase Load()
        {
            ReleaseLoadedHandle();

            if (fallbackDatabase != null)
            {
                return fallbackDatabase;
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            try
            {
                var handle = Addressables.LoadAssetAsync<ServiceSettingsDatabase>(address);
                var database = handle.WaitForCompletion();

                // Удерживаем handle только для успешно загруженного addressable-asset.
                if (handle.Status == AsyncOperationStatus.Succeeded && database != null)
                {
                    loadedHandle = handle;
                    return database;
                }

                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                Debug.LogWarning(
                    $"[{nameof(AddressableServiceSettingsDatabaseProvider)}] Failed to load settings database at address '{address}'. Fallback will be used.");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(
                    $"[{nameof(AddressableServiceSettingsDatabaseProvider)}] Failed to load settings database at address '{address}': {exception.Message}");
            }

            return null;
        }

        /// <summary>
        /// Освобождает addressable-handle, если база была загружена через Addressables.
        /// </summary>
        public void Dispose()
        {
            ReleaseLoadedHandle();
        }

        private void ReleaseLoadedHandle()
        {
            if (loadedHandle.HasValue && loadedHandle.Value.IsValid())
            {
                Addressables.Release(loadedHandle.Value);
            }

            loadedHandle = null;
        }
    }
}
