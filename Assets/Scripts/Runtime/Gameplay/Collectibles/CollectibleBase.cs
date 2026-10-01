using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Базовый класс collectible-предметов с trigger-контактом и защитой от повторного подбора.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public abstract class CollectibleBase : MonoBehaviour, ICollectible, IPlayerContactHandler
    {
        [SerializeField] private bool disableOnCollected = true;

        /// <summary>
        /// Показывает, был ли предмет уже подобран в текущем цикле жизни объекта.
        /// </summary>
        public bool IsCollected { get; private set; }

        /// <summary>
        /// В редакторе автоматически переводит коллайдер предмета в trigger.
        /// </summary>
        protected virtual void Reset()
        {
            if (TryGetComponent(out Collider2D collectibleCollider))
            {
                collectibleCollider.isTrigger = true;
            }
        }

        /// <summary>
        /// При переиспользовании prefab сбрасывает состояние подбора.
        /// </summary>
        protected virtual void OnEnable()
        {
            IsCollected = false;
        }

        /// <summary>
        /// Реагирует на вход игрока в trigger-зону предмета.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (PlayerContactUtility.TryGetAlivePlayer(other, out var player))
            {
                HandlePlayerContact(player, this);
            }
        }

        /// <summary>
        /// Обрабатывает контакт через общий интерфейс игровых объектов.
        /// </summary>
        public void HandlePlayerContact(JumpPlayerController player, Component source)
        {
            Collect(player);
        }

        /// <summary>
        /// Применяет эффект предмета и при необходимости выключает объект.
        /// </summary>
        public void Collect(JumpPlayerController player)
        {
            if (IsCollected || player == null || player.IsDead || player.IgnoresEnvironmentInteractions)
            {
                return;
            }

            IsCollected = true;
            OnCollected(player);

            if (disableOnCollected)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Точка расширения для конкретного эффекта collectible.
        /// </summary>
        protected virtual void OnCollected(JumpPlayerController player)
        {
        }
    }
}
