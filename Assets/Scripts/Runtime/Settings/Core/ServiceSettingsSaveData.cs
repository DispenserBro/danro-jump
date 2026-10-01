using System;
using System.Collections.Generic;

namespace DanroJump.Settings
{
    /// <summary>
    /// DTO для сохранения сервисных настроек в JSON-файл.
    /// </summary>
    [Serializable]
    public sealed class ServiceSettingsSaveData
    {
        /// <summary>
        /// Версия формата сохранения настроек.
        /// </summary>
        public int version = 1;

        /// <summary>
        /// Список сохраненных значений настроек.
        /// </summary>
        public List<Entry> entries = new();

        /// <summary>
        /// Одна сохраненная настройка в формате ключ-значение.
        /// </summary>
        [Serializable]
        public sealed class Entry
        {
            /// <summary>
            /// Ключ настройки.
            /// </summary>
            public string key;

            /// <summary>
            /// Компактное значение настройки.
            /// </summary>
            public ServiceSettingValueCompact value;
        }
    }
}
