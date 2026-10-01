using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Базовый класс оборудования платформы с trigger-контактом игрока.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public abstract class PlatformEquipmentBase : MonoBehaviour, IPlatformEquipment, IPlayerContactHandler
    {
        private readonly System.Collections.Generic.List<IPlatformLandingHandler> landingHandlerBuffer = new();
        private Collider2D equipmentCollider;

        /// <summary>
        /// Платформа, на которую установлено оборудование.
        /// </summary>
        protected IPlatformRuntime Platform { get; private set; }

        /// <summary>
        /// В редакторе переводит коллайдер оборудования в trigger.
        /// </summary>
        protected virtual void Reset()
        {
            equipmentCollider = GetComponent<Collider2D>();
            if (equipmentCollider != null)
            {
                equipmentCollider.isTrigger = true;
            }
        }

        /// <summary>
        /// Сбрасывает состояние при включении, чтобы объект можно было переиспользовать.
        /// </summary>
        private void OnEnable()
        {
            ResetEquipment();
        }

        /// <summary>
        /// Передает контакт с игроком в конкретную реализацию оборудования.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (PlayerContactUtility.TryGetAlivePlayer(other, out var player))
            {
                HandlePlayerContact(player, this);
            }
        }

        /// <summary>
        /// Повторно проверяет игрока внутри trigger, чтобы pickup не терялся при спавне/движении в уже пересеченной зоне.
        /// </summary>
        private void OnTriggerStay2D(Collider2D other)
        {
            if (PlayerContactUtility.TryGetAlivePlayer(other, out var player))
            {
                HandlePlayerContact(player, this);
            }
        }

        /// <summary>
        /// Привязывает оборудование к платформе.
        /// </summary>
        public virtual void Install(IPlatformRuntime platform)
        {
            Platform = platform;
        }

        /// <summary>
        /// Точка расширения для сброса внутреннего состояния оборудования.
        /// </summary>
        public virtual void ResetEquipment()
        {
        }

        /// <summary>
        /// Уведомляет родительскую платформу о landing-событии, если оборудование само обработало посадку игрока.
        /// </summary>
        protected void NotifyPlatformLandingHandlers(JumpPlayerController player)
        {
            if (player == null)
            {
                return;
            }

            equipmentCollider ??= GetComponent<Collider2D>();
            var context = new PlatformLandingContext(
                equipmentCollider,
                null,
                player.GroundCheckPosition,
                Vector2.up,
                false);

            GetComponentsInParent(false, landingHandlerBuffer);
            foreach (var landingHandler in landingHandlerBuffer)
            {
                landingHandler.TryHandleLanding(player, context);
            }

            landingHandlerBuffer.Clear();
        }

        /// <summary>
        /// Обрабатывает контакт игрока с оборудованием.
        /// </summary>
        public abstract void HandlePlayerContact(JumpPlayerController player, Component source);
    }
}
