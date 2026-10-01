using System.Collections.Generic;
using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Один prefab-вариант с весом для случайного выбора.
    /// </summary>
    [System.Serializable]
    public sealed class WeightedPrefabOption
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] [Min(0f)] private float weight = 1f;

        /// <summary>
        /// Prefab, который может быть выбран генератором.
        /// </summary>
        public GameObject Prefab => prefab;

        /// <summary>
        /// Относительный вес prefab в случайном выборе.
        /// </summary>
        public float Weight => weight;

        /// <summary>
        /// Выбирает prefab из списка пропорционально весам.
        /// </summary>
        /// <returns>True, если найден валидный prefab с положительным весом.</returns>
        public static bool TrySelect(IReadOnlyList<WeightedPrefabOption> options, System.Random random, out GameObject prefab)
        {
            return TrySelect(options, random, null, out prefab);
        }

        /// <summary>
        /// Выбирает prefab среди вариантов, которые проходят дополнительный фильтр.
        /// </summary>
        public static bool TrySelect(
            IReadOnlyList<WeightedPrefabOption> options,
            System.Random random,
            System.Predicate<GameObject> prefabFilter,
            out GameObject prefab)
        {
            prefab = null;

            var totalWeight = 0f;
            if (options == null)
            {
                return false;
            }

            foreach (var option in options)
            {
                if (IsSelectable(option, prefabFilter))
                {
                    totalWeight += option.Weight;
                }
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            var roll = (float)((random != null ? random.NextDouble() : 0d) * totalWeight);
            foreach (var option in options)
            {
                if (!IsSelectable(option, prefabFilter))
                {
                    continue;
                }

                roll -= option.Weight;
                if (roll <= 0f)
                {
                    prefab = option.Prefab;
                    return true;
                }
            }

            return false;
        }

        private static bool IsSelectable(WeightedPrefabOption option, System.Predicate<GameObject> prefabFilter)
        {
            return option?.Prefab != null &&
                option.Weight > 0f &&
                (prefabFilter == null || prefabFilter(option.Prefab));
        }
    }
}
