using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Интерфейс управления игроком для ослабления связей и переиспользования логики сессий/камеры.
    /// </summary>
    public interface IPlayerController
    {
        event System.Action<IPlayerController> Died;
        event System.Action<IPlayerController> Respawned;
        event System.Action LandedOnNormalPlatform;

        Transform Transform { get; }
        float HighestY { get; }
        float DeathRespawnDelay { get; }

        void ResetRun();
        void BlockPendingRespawn();
        void AllowRespawn();
        void Respawn();
        void ScheduleRespawn(float delay);
    }
}
