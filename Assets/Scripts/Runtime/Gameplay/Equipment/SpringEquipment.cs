using UnityEngine;
using DanroJump.Audio;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Простая пружина платформы, которая задает игроку фиксированный вертикальный отскок.
    /// </summary>
    public sealed class SpringEquipment : PlatformEquipmentBase, ILandingEquipment
    {
        [SerializeField] private float bounceVelocity = 22f;
        [SerializeField] private float landingVelocityThreshold = 0.05f;
        [SerializeField] private float landingTopTolerance = 0.2f;
        [SerializeField] private float activationCooldown = 0.08f;

        private Collider2D springCollider;
        private IGameAudioService audioService;
        private float lastActivationTime = -1f;

        /// <summary>
        /// Вертикальная скорость, которую пружина задает игроку.
        /// </summary>
        public float BounceVelocity => bounceVelocity;

        [Zenject.Inject]
        public void Construct([Zenject.InjectOptional] IGameAudioService injectedAudioService = null)
        {
            audioService = injectedAudioService;
        }

        /// <summary>
        /// Сбрасывает cooldown и кэширует trigger-коллайдер.
        /// </summary>
        public override void ResetEquipment()
        {
            lastActivationTime = -1f;
            EnsureTriggerCollider();
        }

        /// <summary>
        /// Отталкивает игрока вверх только при падении сверху.
        /// </summary>
        public override void HandlePlayerContact(JumpPlayerController player, Component source)
        {
            TryActivate(player);
        }

        /// <summary>
        /// Пробует активировать пружину от контакта игрока.
        /// </summary>
        public bool TryActivate(JumpPlayerController player)
        {
            if (player == null || player.IgnoresEnvironmentInteractions)
            {
                return false;
            }

            if (!CanActivate(player))
            {
                return false;
            }

            NotifyPlatformLandingHandlers(player);
            audioService?.Play(GameAudioEvent.SpringActivated, transform.position);
            player.BounceWithEnemyPassThrough(bounceVelocity);
            lastActivationTime = Time.time;
            return true;
        }

        public bool TryActivateFromLanding(JumpPlayerController player)
        {
            return TryActivate(player);
        }

        /// <summary>
        /// Проверяет направление движения, cooldown и высоту GroundCheck относительно верха пружины.
        /// </summary>
        private bool CanActivate(JumpPlayerController player)
        {
            if (Time.time - lastActivationTime < activationCooldown)
            {
                return false;
            }

            var isLanding = player.VerticalVelocity <= landingVelocityThreshold ||
                player.PreviousVerticalVelocity <= landingVelocityThreshold;
            if (!isLanding)
            {
                return false;
            }

            if (springCollider == null)
            {
                EnsureTriggerCollider();
            }

            if (springCollider == null)
            {
                return false;
            }

            var landingLineY = springCollider.bounds.max.y - landingTopTolerance;
            return player.GroundCheckY >= landingLineY || player.PreviousGroundCheckY >= landingLineY;
        }

        /// <summary>
        /// Гарантирует, что коллайдер пружины работает как trigger.
        /// </summary>
        private void EnsureTriggerCollider()
        {
            springCollider = GetComponent<Collider2D>();
            if (springCollider != null)
            {
                springCollider.isTrigger = true;
            }
        }
    }
}
