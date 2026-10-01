using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Контракт платформы или дочернего компонента, который может обработать приземление игрока.
    /// </summary>
    public interface IPlatformLandingHandler
    {
        /// <summary>
        /// Пробует обработать посадку игрока.
        /// </summary>
        /// <returns>True, если стандартный автопрыжок игрока нужно подавить.</returns>
        bool TryHandleLanding(JumpPlayerController player, PlatformLandingContext context);
    }
}
