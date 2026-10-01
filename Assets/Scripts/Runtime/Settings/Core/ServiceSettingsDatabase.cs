using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanroJump.Settings
{
    /// <summary>
    /// ScriptableObject-база всех операторских настроек проекта.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ServiceSettingsDatabase",
        menuName = "Danro Jump/Settings/Service Settings Database")]
    public sealed class ServiceSettingsDatabase : ScriptableObject
    {
        [SerializeField] private List<ServiceSettingDefinition> entries = new();

        /// <summary>
        /// Все описания сервисных настроек в порядке отображения.
        /// </summary>
        public IReadOnlyList<ServiceSettingDefinition> Entries => entries;

        /// <summary>
        /// Находит описание настройки по ключу.
        /// </summary>
        public ServiceSettingDefinition Find(string key)
        {
            return entries.Find(entry =>
                entry != null &&
                string.Equals(entry.Key, key, StringComparison.Ordinal));
        }

        /// <summary>
        /// Безопасно пытается найти настройку по ключу.
        /// </summary>
        public bool TryFind(string key, out ServiceSettingDefinition definition)
        {
            definition = Find(key);
            return definition != null;
        }

        /// <summary>
        /// Проверяет пустые и дублирующиеся ключи в редакторе.
        /// </summary>
        private void OnValidate()
        {
            var keys = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    Debug.LogWarning($"[{nameof(ServiceSettingsDatabase)}] Empty key in {name}.", this);
                    continue;
                }

                if (!keys.Add(entry.Key))
                {
                    Debug.LogWarning($"[{nameof(ServiceSettingsDatabase)}] Duplicate key '{entry.Key}' in {name}.", this);
                }
            }
        }
    }
}
