using System.Collections.Generic;
using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Runtime-представление платформы для спавна оборудования, врагов и collectibles.
    /// </summary>
    public interface IPlatformRuntime
    {
        /// <summary>
        /// Transform корня платформы.
        /// </summary>
        Transform Transform { get; }

        /// <summary>
        /// Точки спавна подбираемых предметов.
        /// </summary>
        IReadOnlyList<PlatformSpawnPoint> CollectibleSpawnPoints { get; }

        /// <summary>
        /// Точки спавна оборудования платформы.
        /// </summary>
        IReadOnlyList<PlatformSpawnPoint> EquipmentSpawnPoints { get; }

        /// <summary>
        /// Точки спавна противников.
        /// </summary>
        IReadOnlyList<PlatformSpawnPoint> EnemySpawnPoints { get; }
    }
}
