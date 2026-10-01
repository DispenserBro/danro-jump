namespace DanroJump.Gameplay
{
    /// <summary>
    /// Контракт оборудования, которое устанавливается на платформу.
    /// </summary>
    public interface IPlatformEquipment
    {
        /// <summary>
        /// Привязывает оборудование к runtime-платформе.
        /// </summary>
        void Install(IPlatformRuntime platform);

        /// <summary>
        /// Сбрасывает состояние оборудования при спавне или переиспользовании.
        /// </summary>
        void ResetEquipment();
    }
}
