using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// ScriptableObject-описание одного вертикального этапа сложности.
    /// Хранит веса платформ, кривые расстояний и вероятности спавна контента.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameplayDifficultyStage",
        menuName = "Danro Jump/Gameplay/Difficulty Stage",
        order = 10)]
    public sealed class GameplayDifficultyStage : ScriptableObject
    {
        /// <summary>
        /// Настройки платформ этапа: какие prefab доступны и как меняется вертикальный шаг.
        /// </summary>
        [System.Serializable]
        private sealed class StagePlatformSettings
        {
            [SerializeField] private List<WeightedPrefabOption> platforms = new();
            [SerializeField] private AnimationCurve verticalSpacingCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            [SerializeField] [Min(0.1f)] private float initialVerticalSpacing = 1.15f;
            [SerializeField] [Min(0.1f)] private float maximumVerticalSpacing = 1.95f;

            /// <summary>
            /// Выбирает prefab платформы по весам.
            /// </summary>
            public GameObject GetPlatformPrefab(System.Random random)
            {
                return WeightedPrefabOption.TrySelect(platforms, random, out var prefab) ? prefab : null;
            }

            /// <summary>
            /// Вычисляет расстояние до следующей платформы по прогрессу этапа.
            /// </summary>
            public float EvaluateVerticalSpacing(float progress)
            {
                var spacingProgress = EvaluateCurve01(verticalSpacingCurve, progress);
                var maxSpacing = Mathf.Max(initialVerticalSpacing, maximumVerticalSpacing);
                return Mathf.Lerp(initialVerticalSpacing, maxSpacing, spacingProgress);
            }

        }

        /// <summary>
        /// Настройки одной категории спавнимого контента этапа.
        /// </summary>
        [System.Serializable]
        private sealed class StageContentSettings
        {
            [SerializeField] private List<WeightedPrefabOption> prefabs = new();
            [SerializeField] private AnimationCurve spawnChanceCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            [SerializeField] [Range(0f, 1f)] private float initialSpawnChance;
            [SerializeField] [Range(0f, 1f)] private float maximumSpawnChance;

            /// <summary>
            /// Взвешенный список prefab этой категории контента.
            /// </summary>
            public IReadOnlyList<WeightedPrefabOption> Prefabs => prefabs;

            /// <summary>
            /// Вычисляет шанс появления контента по прогрессу этапа.
            /// </summary>
            public float EvaluateSpawnChance(float progress)
            {
                if (!HasValidPrefab())
                {
                    return 0f;
                }

                var chanceProgress = EvaluateCurve01(spawnChanceCurve, progress);
                var maxChance = Mathf.Max(initialSpawnChance, maximumSpawnChance);
                return Mathf.Lerp(initialSpawnChance, maxChance, chanceProgress);
            }

            /// <summary>
            /// Выбирает prefab из этой категории с учетом дополнительного фильтра.
            /// </summary>
            public bool TryGetPrefab(
                System.Random random,
                System.Predicate<GameObject> prefabFilter,
                out GameObject prefab)
            {
                return WeightedPrefabOption.TrySelect(prefabs, random, prefabFilter, out prefab);
            }

            private bool HasValidPrefab()
            {
                if (prefabs == null)
                {
                    return false;
                }

                foreach (var option in prefabs)
                {
                    if (option?.Prefab != null && option.Weight > 0f)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        [Header("Общие настройки этапа")]
        [SerializeField] private string stageName = "Stage";
        [SerializeField] private float startY;
        [SerializeField] [Min(1f)] private float verticalLength = 160f;
        [FormerlySerializedAs("platforms.horizontalDistribution")]
        [SerializeField] [Range(-1f, 1f)] private float horizontalDistribution;

        [Header("Платформы")]
        [SerializeField] private StagePlatformSettings platforms = new();

        [Header("Оборудование")]
        [SerializeField] private StageContentSettings equipment = new();

        [Header("Противники")]
        [SerializeField] private StageContentSettings enemies = new();

        [Header("Подбираемые предметы")]
        [SerializeField] private StageContentSettings collectibles = new();

        /// <summary>
        /// Отображаемое имя этапа для инспектора, overlay и диагностики.
        /// </summary>
        public string StageName => stageName;

        /// <summary>
        /// Y-координата, с которой этап начинает действовать.
        /// </summary>
        public float StartY => startY;

        /// <summary>
        /// Выбирает платформу этапа по весам.
        /// </summary>
        public GameObject GetPlatformPrefab(System.Random random)
        {
            return platforms.GetPlatformPrefab(random);
        }

        /// <summary>
        /// Возвращает вертикальное расстояние до следующей платформы на указанной высоте.
        /// </summary>
        public float GetVerticalSpacing(float worldY)
        {
            return platforms.EvaluateVerticalSpacing(CalculateProgress(worldY));
        }

        /// <summary>
        /// Возвращает распределение платформ по горизонтали: -1 центр, 0 вся ширина, 1 края.
        /// </summary>
        public float GetHorizontalDistribution()
        {
            return Mathf.Clamp(horizontalDistribution, -1f, 1f);
        }

        /// <summary>
        /// Создает источник контента с вероятностями, актуальными для указанной высоты.
        /// </summary>
        public IPlatformContentSource CreateContentSource(float worldY)
        {
            var progress = CalculateProgress(worldY);
            return new PlatformContentProfile(
                collectibles.Prefabs,
                collectibles.EvaluateSpawnChance(progress),
                equipment.Prefabs,
                equipment.EvaluateSpawnChance(progress),
                enemies.Prefabs,
                enemies.EvaluateSpawnChance(progress));
        }

        /// <summary>
        /// Пробует выбрать процедурного врага из настроек противников текущего этапа.
        /// </summary>
        public bool TryGetProceduralEnemyPrefab(float worldY, System.Random random, out GameObject prefab)
        {
            var progress = CalculateProgress(worldY);
            var spawnChance = enemies.EvaluateSpawnChance(progress);
            if (spawnChance <= 0f)
            {
                prefab = null;
                return false;
            }

            if (spawnChance < 1f && (random != null ? random.NextDouble() : 0d) > spawnChance)
            {
                prefab = null;
                return false;
            }

            return enemies.TryGetPrefab(random, EnemyPrefabRules.CanSpawnProcedurally, out prefab);
        }

        /// <summary>
        /// Считает прогресс внутри этапа в диапазоне 0..1.
        /// </summary>
        public float CalculateProgress(float worldY)
        {
            if (verticalLength <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01((worldY - startY) / verticalLength);
        }

        /// <summary>
        /// Безопасно вычисляет значение кривой и ограничивает результат диапазоном 0..1.
        /// </summary>
        private static float EvaluateCurve01(AnimationCurve curve, float progress)
        {
            if (curve == null || curve.length == 0)
            {
                return Mathf.Clamp01(progress);
            }

            return Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(progress)));
        }
    }
}
