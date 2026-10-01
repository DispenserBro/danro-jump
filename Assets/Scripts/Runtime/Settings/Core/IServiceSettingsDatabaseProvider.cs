using System;

namespace DanroJump.Settings
{
    /// <summary>
    /// Источник базы сервисных настроек.
    /// </summary>
    public interface IServiceSettingsDatabaseProvider : IDisposable
    {
        /// <summary>
        /// Загружает базу настроек.
        /// </summary>
        ServiceSettingsDatabase Load();
    }
}
