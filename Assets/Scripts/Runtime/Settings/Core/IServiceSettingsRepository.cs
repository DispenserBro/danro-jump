using System.Collections.Generic;

namespace DanroJump.Settings
{
    /// <summary>
    /// Репозиторий сохраненных значений сервисных настроек.
    /// </summary>
    public interface IServiceSettingsRepository
    {
        /// <summary>
        /// Путь к хранилищу настроек.
        /// </summary>
        string Path { get; }

        /// <summary>
        /// True, если сохранение уже существует.
        /// </summary>
        bool Exists { get; }

        /// <summary>
        /// Загружает значения настроек из хранилища.
        /// </summary>
        bool TryLoad(out Dictionary<string, ServiceSettingValue> values);

        /// <summary>
        /// Сохраняет значения настроек в хранилище.
        /// </summary>
        bool Save(IReadOnlyDictionary<string, ServiceSettingValue> values);
    }
}
