using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DanroJump.Settings
{
    /// <summary>
    /// JSON-репозиторий сервисных настроек в Application.persistentDataPath.
    /// </summary>
    public sealed class JsonServiceSettingsRepository : IServiceSettingsRepository
    {
        private const string DefaultFileName = "service-settings.json";

        /// <summary>
        /// Создает репозиторий с безопасным именем файла настроек.
        /// </summary>
        public JsonServiceSettingsRepository(string fileName)
        {
            var safeFileName = GetSafeFileName(fileName);
            Path = System.IO.Path.Combine(Application.persistentDataPath, safeFileName);
        }

        /// <summary>
        /// Полный путь к JSON-файлу настроек.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Показывает, существует ли файл настроек на диске.
        /// </summary>
        public bool Exists => File.Exists(Path);

        /// <summary>
        /// Загружает настройки из JSON и разворачивает compact-значения.
        /// </summary>
        public bool TryLoad(out Dictionary<string, ServiceSettingValue> values)
        {
            values = new Dictionary<string, ServiceSettingValue>(StringComparer.Ordinal);

            if (!Exists)
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(Path);
                var data = JsonUtility.FromJson<ServiceSettingsSaveData>(json);
                if (data?.entries == null)
                {
                    return false;
                }

                foreach (var entry in data.entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    {
                        continue;
                    }

                    values[entry.key] = ServiceSettingValue.FromCompact(entry.value);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{nameof(JsonServiceSettingsRepository)}] Failed to load settings from '{Path}': {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// Сохраняет настройки в JSON в компактном сериализуемом виде.
        /// </summary>
        public bool Save(IReadOnlyDictionary<string, ServiceSettingValue> values)
        {
            if (values == null)
            {
                Debug.LogWarning($"[{nameof(JsonServiceSettingsRepository)}] Settings snapshot is null. Save skipped.");
                return false;
            }

            try
            {
                var directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var data = new ServiceSettingsSaveData();
                foreach (var pair in values)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                    {
                        continue;
                    }

                    data.entries.Add(new ServiceSettingsSaveData.Entry
                    {
                        key = pair.Key,
                        value = pair.Value.ToCompact()
                    });
                }

                WriteAllTextSafely(Path, JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{nameof(JsonServiceSettingsRepository)}] Failed to save settings to '{Path}': {exception.Message}");
                return false;
            }
        }

        /// <summary>
        /// Пишет файл через временную копию, чтобы не оставить пустой JSON при аварийном завершении записи.
        /// </summary>
        private static void WriteAllTextSafely(string path, string contents)
        {
            var tempPath = path + ".tmp";
            var backupPath = path + ".bak";

            try
            {
                File.WriteAllText(tempPath, contents);

                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, backupPath, true);
                    return;
                }

                File.Move(tempPath, path);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        /// <summary>
        /// Оставляет только имя файла, чтобы настройки не могли выйти за persistentDataPath.
        /// </summary>
        private static string GetSafeFileName(string fileName)
        {
            var requestedFileName = string.IsNullOrWhiteSpace(fileName) ? DefaultFileName : fileName.Trim();
            var safeFileName = System.IO.Path.GetFileName(requestedFileName);

            if (string.IsNullOrWhiteSpace(safeFileName) ||
                safeFileName == "." ||
                safeFileName == ".." ||
                safeFileName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                Debug.LogWarning(
                    $"[{nameof(JsonServiceSettingsRepository)}] Unsafe settings file name '{fileName}' was replaced with '{DefaultFileName}'.");
                return DefaultFileName;
            }

            if (!string.Equals(requestedFileName, safeFileName, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"[{nameof(JsonServiceSettingsRepository)}] Settings file path '{fileName}' was reduced to safe file name '{safeFileName}'.");
            }

            return safeFileName;
        }
    }
}
