namespace DanroJump.Gameplay
{
    /// <summary>
    /// Общий контракт подбираемого предмета.
    /// </summary>
    public interface ICollectible
    {
        /// <summary>
        /// True, если предмет уже был подобран текущим игроком.
        /// </summary>
        bool IsCollected { get; }

        /// <summary>
        /// Применяет эффект предмета к игроку.
        /// </summary>
        void Collect(JumpPlayerController player);
    }
}
