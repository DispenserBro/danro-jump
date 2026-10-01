using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Опасное оборудование платформы, которое убивает игрока при контакте.
    /// </summary>
    public sealed class HazardEquipment : PlatformEquipmentBase
    {
        [SerializeField] [Min(0f)] private float activationCooldown = 0.2f;

        private float lastActivationTime = -999f;

        /// <summary>
        /// Сбрасывает cooldown при переиспользовании оборудования.
        /// </summary>
        public override void ResetEquipment()
        {
            lastActivationTime = -999f;
        }

        /// <summary>
        /// Убивает игрока, если контакт не находится внутри cooldown.
        /// </summary>
        public override void HandlePlayerContact(JumpPlayerController player, Component source)
        {
            if (player == null || player.IgnoresEnvironmentInteractions || Time.time - lastActivationTime < activationCooldown)
            {
                return;
            }

            lastActivationTime = Time.time;
            player.Kill();
        }
    }
}
