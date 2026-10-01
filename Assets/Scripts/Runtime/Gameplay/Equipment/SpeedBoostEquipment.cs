using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Оборудование платформы, которое временно ускоряет горизонтальное движение игрока.
    /// </summary>
    public sealed class SpeedBoostEquipment : PlatformEquipmentBase
    {
        [SerializeField] [Min(0.01f)] private float moveSpeedMultiplier = 1.45f;
        [SerializeField] [Min(0.01f)] private float duration = 3f;
        [SerializeField] [Min(0f)] private float bounceVelocity;
        [SerializeField] [Min(0f)] private float activationCooldown = 0.25f;

        private float lastActivationTime = -999f;

        /// <summary>
        /// Сбрасывает cooldown при переиспользовании оборудования.
        /// </summary>
        public override void ResetEquipment()
        {
            lastActivationTime = -999f;
        }

        /// <summary>
        /// Применяет ускорение и опциональный отскок при контакте с игроком.
        /// </summary>
        public override void HandlePlayerContact(JumpPlayerController player, Component source)
        {
            if (player == null || player.IgnoresEnvironmentInteractions || Time.time - lastActivationTime < activationCooldown)
            {
                return;
            }

            player.ApplyMoveSpeedMultiplier(moveSpeedMultiplier, duration);

            // Bounce опционален: при нуле оборудование влияет только на скорость.
            if (bounceVelocity > 0f)
            {
                player.Bounce(bounceVelocity);
            }

            lastActivationTime = Time.time;
        }
    }
}
