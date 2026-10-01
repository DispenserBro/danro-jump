namespace DanroJump.Settings
{
    /// <summary>
    /// Пустой applier настроек для случаев, когда значения нужно хранить без runtime-побочных эффектов.
    /// </summary>
    public sealed class NullServiceSettingApplier : IServiceSettingApplier
    {
        /// <summary>
        /// Намеренно ничего не применяет.
        /// </summary>
        public void Apply(ServiceSettingDefinition definition, ServiceSettingValue value)
        {
        }
    }
}
