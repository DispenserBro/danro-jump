namespace DanroJump.Gameplay
{
    /// <summary>
    /// Контракт оборудования, которое должно срабатывать от посадки игрока сверху.
    /// </summary>
    public interface ILandingEquipment
    {
        /// <summary>
        /// Пробует активировать оборудование из общего landing flow игрока.
        /// </summary>
        bool TryActivateFromLanding(JumpPlayerController player);
    }
}
