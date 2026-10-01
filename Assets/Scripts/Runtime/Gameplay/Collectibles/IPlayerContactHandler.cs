using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Контракт объектов, которые реагируют на контакт с игроком.
    /// </summary>
    public interface IPlayerContactHandler
    {
        /// <summary>
        /// Обрабатывает контакт игрока с источником события.
        /// </summary>
        void HandlePlayerContact(JumpPlayerController player, Component source);
    }
}
