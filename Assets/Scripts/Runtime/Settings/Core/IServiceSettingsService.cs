using System;
using System.Collections.Generic;

namespace DanroJump.Settings
{
    /// <summary>
    /// Read-only API сервисных настроек для UI и gameplay-систем.
    /// </summary>
    public interface IReadOnlyServiceSettings
    {
        ServiceSettingsDatabase Database { get; }
        event Action SettingsLoaded;
        event Action SettingsSaved;
        event Action<string, ServiceSettingValue> SettingChanged;

        bool TryGetDefinition(string key, out ServiceSettingDefinition definition);
        IReadOnlyDictionary<string, ServiceSettingValue> Snapshot();
        ServiceSettingValue Get(string key);
        bool GetBool(string key);
        int GetInt(string key);
        float GetFloat(string key);
        string GetString(string key);
        int GetOptionIndex(string key);
    }

    /// <summary>
    /// Полный API сервисных настроек с загрузкой, сохранением и изменением значений.
    /// </summary>
    public interface IServiceSettingsService : IReadOnlyServiceSettings
    {
        void Load();
        void Save();
        void ReloadExternalChanges();
        void ApplyAll();
        void ResetAllToDefaults();
        void ResetGroupToDefaults(string groupName);
        bool Set(string key, ServiceSettingValue value);
        bool SetExternal(string key, ServiceSettingValue value);
        bool SetBool(string key, bool value);
        bool SetInt(string key, int value);
        bool SetFloat(string key, float value);
        bool SetString(string key, string value);
        bool SetOptionIndex(string key, int optionIndex);
    }
}
