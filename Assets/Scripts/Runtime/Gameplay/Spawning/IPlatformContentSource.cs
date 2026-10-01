using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Источник правил спавна контента для платформы.
    /// </summary>
    public interface IPlatformContentSource
    {
        /// <summary>
        /// Проверяет, нужно ли спавнить контент указанного типа.
        /// </summary>
        bool ShouldSpawn(PlatformContentKind kind, System.Random random);

        /// <summary>
        /// Выбирает prefab указанной категории по весам.
        /// </summary>
        bool TryGetPrefab(PlatformContentKind kind, System.Random random, out GameObject prefab);
    }
}
