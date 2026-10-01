namespace DanroJump.Gameplay
{
    /// <summary>
    /// Контракт подбираемых игровых объектов, которые применяются к игроку при контакте.
    /// </summary>
    public interface IPickable
    {
        /// <summary>
        /// Пробует подобрать объект указанным игроком.
        /// </summary>
        bool TryPickUp(JumpPlayerController player);
    }
}
