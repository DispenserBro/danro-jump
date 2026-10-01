using UnityEngine;
using Zenject;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Контекст создания или рецикла платформы.
    /// Передается платформе и дочерним lifecycle-компонентам.
    /// </summary>
    public readonly struct PlatformSpawnContext
    {
        /// <summary>
        /// Создает контекст спавна платформы.
        /// </summary>
        public PlatformSpawnContext(
            DiContainer container,
            JumpPlayerController player,
            Camera gameplayCamera,
            int spawnIndex,
            bool isStartPlatform,
            float worldY)
        {
            Container = container;
            Player = player;
            GameplayCamera = gameplayCamera;
            SpawnIndex = spawnIndex;
            IsStartPlatform = isStartPlatform;
            WorldY = worldY;
        }

        /// <summary>
        /// DI-контейнер сцены для создания дочернего контента.
        /// </summary>
        public DiContainer Container { get; }

        /// <summary>
        /// Игрок текущей gameplay-сцены.
        /// </summary>
        public JumpPlayerController Player { get; }

        /// <summary>
        /// Камера gameplay-сцены.
        /// </summary>
        public Camera GameplayCamera { get; }

        /// <summary>
        /// Порядковый номер платформы в генерации.
        /// </summary>
        public int SpawnIndex { get; }

        /// <summary>
        /// True для стартовой платформы забега.
        /// </summary>
        public bool IsStartPlatform { get; }

        /// <summary>
        /// Y-координата платформы в мире.
        /// </summary>
        public float WorldY { get; }
    }
}
