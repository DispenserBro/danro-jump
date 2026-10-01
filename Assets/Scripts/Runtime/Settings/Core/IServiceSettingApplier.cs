namespace DanroJump.Settings
{
    /// <summary>
    /// Применяет изменение настройки к runtime-системам проекта.
    /// </summary>
    public interface IServiceSettingApplier
    {
        /// <summary>
        /// Вызывается после изменения или загрузки настройки.
        /// </summary>
        void Apply(ServiceSettingDefinition definition, ServiceSettingValue value);
    }
}
