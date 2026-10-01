using System.Collections.Generic;
using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Immutable-профиль контента платформы, рассчитанный из текущего этапа сложности.
    /// </summary>
    public sealed class PlatformContentProfile : IPlatformContentSource
    {
        private readonly IReadOnlyList<WeightedPrefabOption> collectiblePrefabs;
        private readonly IReadOnlyList<WeightedPrefabOption> equipmentPrefabs;
        private readonly IReadOnlyList<WeightedPrefabOption> enemyPrefabs;
        private readonly float collectibleSpawnChance;
        private readonly float equipmentSpawnChance;
        private readonly float enemySpawnChance;

        /// <summary>
        /// Создает профиль вероятностей и весов для трех категорий контента.
        /// </summary>
        public PlatformContentProfile(
            IReadOnlyList<WeightedPrefabOption> collectiblePrefabs,
            float collectibleSpawnChance,
            IReadOnlyList<WeightedPrefabOption> equipmentPrefabs,
            float equipmentSpawnChance,
            IReadOnlyList<WeightedPrefabOption> enemyPrefabs,
            float enemySpawnChance)
        {
            this.collectiblePrefabs = collectiblePrefabs;
            this.collectibleSpawnChance = Mathf.Clamp01(collectibleSpawnChance);
            this.equipmentPrefabs = equipmentPrefabs;
            this.equipmentSpawnChance = Mathf.Clamp01(equipmentSpawnChance);
            this.enemyPrefabs = enemyPrefabs;
            this.enemySpawnChance = Mathf.Clamp01(enemySpawnChance);
        }

        /// <summary>
        /// Делает бросок вероятности для выбранной категории контента.
        /// </summary>
        public bool ShouldSpawn(PlatformContentKind kind, System.Random random)
        {
            var chance = GetSpawnChance(kind);
            if (chance <= 0f)
            {
                return false;
            }

            if (chance >= 1f)
            {
                return true;
            }

            return (random != null ? random.NextDouble() : 0d) <= chance;
        }

        /// <summary>
        /// Выбирает prefab категории по весам.
        /// </summary>
        public bool TryGetPrefab(PlatformContentKind kind, System.Random random, out GameObject prefab)
        {
            System.Predicate<GameObject> prefabFilter = kind == PlatformContentKind.Enemy
                ? EnemyPrefabRules.CanSpawnOnPlatform
                : null;
            if (WeightedPrefabOption.TrySelect(GetPrefabs(kind), random, prefabFilter, out prefab))
            {
                return true;
            }

            prefab = null;
            return false;
        }

        /// <summary>
        /// Возвращает шанс спавна для категории контента.
        /// </summary>
        private float GetSpawnChance(PlatformContentKind kind)
        {
            switch (kind)
            {
                case PlatformContentKind.Collectible:
                    return collectibleSpawnChance;
                case PlatformContentKind.Equipment:
                    return equipmentSpawnChance;
                case PlatformContentKind.Enemy:
                    return enemySpawnChance;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// Возвращает список prefab-вариантов для категории контента.
        /// </summary>
        private IReadOnlyList<WeightedPrefabOption> GetPrefabs(PlatformContentKind kind)
        {
            switch (kind)
            {
                case PlatformContentKind.Collectible:
                    return collectiblePrefabs;
                case PlatformContentKind.Equipment:
                    return equipmentPrefabs;
                case PlatformContentKind.Enemy:
                    return enemyPrefabs;
                default:
                    return collectiblePrefabs;
            }
        }
    }
}
