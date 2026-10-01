using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanroJump.Settings
{
    /// <summary>
    /// Runtime-сервис операторских настроек: хранит значения, валидирует их по базе и сохраняет во внешний файл.
    /// </summary>
    public sealed class ServiceSettingsService : IServiceSettingsService
    {
        private readonly ServiceSettingsDatabase database;
        private readonly IServiceSettingsRepository repository;
        private readonly IServiceSettingApplier applier;
        private readonly Dictionary<string, ServiceSettingValue> values = new(StringComparer.Ordinal);

        /// <summary>
        /// Создает сервис поверх базы описаний, репозитория сохранения и опционального applier.
        /// </summary>
        public ServiceSettingsService(
            ServiceSettingsDatabase database,
            IServiceSettingsRepository repository,
            IServiceSettingApplier applier = null)
        {
            this.database = database;
            this.repository = repository;
            this.applier = applier ?? new NullServiceSettingApplier();
        }

        /// <summary>
        /// База описаний настроек, используемая сервисом.
        /// </summary>
        public ServiceSettingsDatabase Database => database;

        public event Action SettingsLoaded;
        public event Action SettingsSaved;
        public event Action<string, ServiceSettingValue> SettingChanged;

        /// <summary>
        /// Загружает сохраненные значения, дополняет дефолтами и применяет их.
        /// </summary>
        public void Load()
        {
            values.Clear();

            if (repository != null && repository.TryLoad(out var loadedValues))
            {
                foreach (var pair in loadedValues)
                {
                    if (TryGetDefinition(pair.Key, out var definition) && IsStoredValueDefinition(definition))
                    {
                        values[pair.Key] = definition.Sanitize(pair.Value);
                    }
                }
            }

            EnsureDefaults();
            ApplyAll();
            SettingsLoaded?.Invoke();
        }

        /// <summary>
        /// Сохраняет текущий snapshot настроек в репозиторий.
        /// </summary>
        public void Save()
        {
            if (repository == null || !repository.Save(values))
            {
                return;
            }

            SettingsSaved?.Invoke();
        }

        /// <summary>
        /// Перечитывает только значения, разрешенные для внешних изменений.
        /// </summary>
        public void ReloadExternalChanges()
        {
            if (repository == null || !repository.TryLoad(out var loadedValues))
            {
                return;
            }

            foreach (var pair in loadedValues)
            {
                SetExternal(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// Повторно применяет все значения через applier.
        /// </summary>
        public void ApplyAll()
        {
            if (database == null)
            {
                return;
            }

            foreach (var definition in database.Entries)
            {
                if (!IsStoredValueDefinition(definition))
                {
                    continue;
                }

                applier.Apply(definition, Get(definition.Key));
            }
        }

        /// <summary>
        /// Сбрасывает все настройки к дефолтам из базы и сохраняет результат.
        /// </summary>
        public void ResetAllToDefaults()
        {
            if (database == null)
            {
                return;
            }

            foreach (var definition in database.Entries)
            {
                if (!IsStoredValueDefinition(definition))
                {
                    continue;
                }

                Set(definition.Key, definition.DefaultValue);
            }

            Save();
        }

        /// <summary>
        /// Сбрасывает одну группу настроек к дефолтам.
        /// </summary>
        public void ResetGroupToDefaults(string groupName)
        {
            if (database == null || string.IsNullOrWhiteSpace(groupName))
            {
                return;
            }

            foreach (var definition in database.Entries)
            {
                if (!IsStoredValueDefinition(definition) ||
                    !string.Equals(definition.Group, groupName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Set(definition.Key, definition.DefaultValue);
            }

            Save();
        }

        /// <summary>
        /// Устанавливает значение настройки после проверки и sanitization.
        /// </summary>
        public bool Set(string key, ServiceSettingValue value)
        {
            if (!TryGetDefinition(key, out var definition))
            {
                Debug.LogWarning($"[{nameof(ServiceSettingsService)}] Unknown setting key '{key}'.");
                return false;
            }

            if (definition.ValueType == ServiceSettingValueType.Button)
            {
                Debug.LogWarning($"[{nameof(ServiceSettingsService)}] Button setting '{key}' does not store a value.");
                return false;
            }

            var sanitized = definition.Sanitize(value);
            if (values.TryGetValue(key, out var current) && current.Equals(sanitized))
            {
                return true;
            }

            // Значение применяем сразу, а Save отвечает только за запись на диск.
            values[key] = sanitized;
            applier.Apply(definition, sanitized);
            SettingChanged?.Invoke(key, sanitized);
            return true;
        }

        /// <summary>
        /// Устанавливает значение из внешнего файла только для разрешенных настроек.
        /// </summary>
        public bool SetExternal(string key, ServiceSettingValue value)
        {
            if (!TryGetDefinition(key, out var definition))
            {
                Debug.LogWarning($"[{nameof(ServiceSettingsService)}] Unknown external setting key '{key}'.");
                return false;
            }

            if (!definition.ExposedForExternalChanges)
            {
                Debug.LogWarning($"[{nameof(ServiceSettingsService)}] Setting '{key}' is not exposed for external changes.");
                return false;
            }

            return Set(key, value);
        }

        /// <summary>
        /// Устанавливает bool-настройку по ключу.
        /// </summary>
        public bool SetBool(string key, bool value)
        {
            return Set(key, ServiceSettingValue.Bool(value));
        }

        /// <summary>
        /// Устанавливает int-настройку по ключу.
        /// </summary>
        public bool SetInt(string key, int value)
        {
            return Set(key, ServiceSettingValue.Int(value));
        }

        /// <summary>
        /// Устанавливает float-настройку по ключу.
        /// </summary>
        public bool SetFloat(string key, float value)
        {
            return Set(key, ServiceSettingValue.Float(value));
        }

        /// <summary>
        /// Устанавливает string-настройку по ключу.
        /// </summary>
        public bool SetString(string key, string value)
        {
            return Set(key, ServiceSettingValue.String(value));
        }

        /// <summary>
        /// Устанавливает option-настройку по индексу варианта.
        /// </summary>
        public bool SetOptionIndex(string key, int optionIndex)
        {
            return Set(key, ServiceSettingValue.Option(optionIndex));
        }

        /// <summary>
        /// Пытается найти описание настройки по ключу.
        /// </summary>
        public bool TryGetDefinition(string key, out ServiceSettingDefinition definition)
        {
            definition = null;
            return !string.IsNullOrWhiteSpace(key) &&
                database != null &&
                database.TryFind(key, out definition);
        }

        /// <summary>
        /// Возвращает копию текущих значений для безопасного чтения снаружи.
        /// </summary>
        public IReadOnlyDictionary<string, ServiceSettingValue> Snapshot()
        {
            return new Dictionary<string, ServiceSettingValue>(values, StringComparer.Ordinal);
        }

        /// <summary>
        /// Возвращает текущее значение или дефолт из базы.
        /// </summary>
        public ServiceSettingValue Get(string key)
        {
            if (values.TryGetValue(key, out var value))
            {
                return value;
            }

            return TryGetDefinition(key, out var definition) ? definition.DefaultValue : default;
        }

        /// <summary>
        /// Возвращает bool-значение настройки.
        /// </summary>
        public bool GetBool(string key)
        {
            return Get(key).boolValue;
        }

        /// <summary>
        /// Возвращает int-значение настройки.
        /// </summary>
        public int GetInt(string key)
        {
            return Get(key).intValue;
        }

        /// <summary>
        /// Возвращает float-значение настройки.
        /// </summary>
        public float GetFloat(string key)
        {
            return Get(key).floatValue;
        }

        /// <summary>
        /// Возвращает string-значение настройки.
        /// </summary>
        public string GetString(string key)
        {
            return Get(key).stringValue ?? string.Empty;
        }

        /// <summary>
        /// Возвращает индекс выбранного option-варианта.
        /// </summary>
        public int GetOptionIndex(string key)
        {
            return Get(key).intValue;
        }

        /// <summary>
        /// Заполняет отсутствующие значения дефолтами и заново sanitizes загруженные данные.
        /// </summary>
        private void EnsureDefaults()
        {
            if (database == null)
            {
                return;
            }

            foreach (var definition in database.Entries)
            {
                if (!IsStoredValueDefinition(definition))
                {
                    continue;
                }

                values[definition.Key] = values.TryGetValue(definition.Key, out var existingValue)
                    ? definition.Sanitize(existingValue)
                    : definition.Sanitize(definition.DefaultValue);
            }
        }

        /// <summary>
        /// Проверяет, что описание настройки пригодно для работы сервиса.
        /// </summary>
        private static bool IsValidDefinition(ServiceSettingDefinition definition)
        {
            return definition != null && !string.IsNullOrWhiteSpace(definition.Key);
        }

        /// <summary>
        /// Проверяет, что настройка хранит значение. Кнопки являются действиями и не попадают в JSON.
        /// </summary>
        private static bool IsStoredValueDefinition(ServiceSettingDefinition definition)
        {
            return IsValidDefinition(definition) && definition.ValueType != ServiceSettingValueType.Button;
        }
    }
}
