using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Проверяет, в каких контекстах разрешено создавать enemy-prefab.
    /// </summary>
    public static class EnemyPrefabRules
    {
        /// <summary>
        /// True, если prefab можно создать на платформенной точке.
        /// </summary>
        public static bool CanSpawnOnPlatform(GameObject prefab)
        {
            return TryGetEnemy(prefab, out var enemy) && enemy.CanSpawnOnPlatform;
        }

        /// <summary>
        /// True, если prefab можно создать процедурно в свободном поле.
        /// </summary>
        public static bool CanSpawnProcedurally(GameObject prefab)
        {
            return TryGetEnemy(prefab, out var enemy) && enemy.CanSpawnProcedurally;
        }

        /// <summary>
        /// Получает базовый enemy-компонент из prefab.
        /// </summary>
        public static bool TryGetEnemy(GameObject prefab, out EnemyBase enemy)
        {
            enemy = prefab != null ? prefab.GetComponentInChildren<EnemyBase>(true) : null;
            return enemy != null;
        }
    }
}
