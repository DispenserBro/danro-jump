using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Выбирает активный этап сложности по максимальной высоте игрока или Y-координате генерации.
    /// </summary>
    public sealed class GameplayDifficultyProgression : MonoBehaviour, IGameplayDifficultyProgression
    {
        [SerializeField] private JumpPlayerController player;
        [SerializeField] private bool progressionEnabled = true;
        [SerializeField] private List<GameplayDifficultyStage> stages = new();

        /// <summary>
        /// Показывает, есть ли хотя бы один настроенный этап сложности.
        /// </summary>
        public bool HasStages => progressionEnabled && stages != null && stages.Exists(stage => stage != null);

        /// <summary>
        /// Начальная Y-координата генерации по первому этапу.
        /// </summary>
        public float InitialSpawnY => GetInitialSpawnY();

        /// <summary>
        /// Прогресс игрока внутри текущего этапа по максимальной высоте забега.
        /// </summary>
        public float CurrentProgress => CalculateProgress(player != null ? player.HighestY : 0f);

        /// <summary>
        /// Имя текущего этапа игрока для HUD и editor overlay.
        /// </summary>
        public string CurrentStageName => GetStage(player != null ? player.HighestY : 0f)?.StageName ?? string.Empty;

        /// <summary>
        /// Получает игрока из SceneContext, чтобы UI и диагностические окна видели текущий этап забега.
        /// </summary>
        [Inject]
        public void Construct(JumpPlayerController injectedPlayer)
        {
            player = injectedPlayer;
        }

        /// <summary>
        /// Выбирает prefab платформы для указанной высоты по настройкам активного этапа.
        /// </summary>
        public GameObject GetPlatformPrefab(float worldY, System.Random random)
        {
            var stage = GetStage(worldY);
            return stage != null ? stage.GetPlatformPrefab(random) : null;
        }

        /// <summary>
        /// Возвращает расстояние между платформами для указанной высоты.
        /// </summary>
        public float GetVerticalSpacing(float worldY)
        {
            var stage = GetStage(worldY);
            if (stage == null)
            {
                return 0f;
            }

            return stage.GetVerticalSpacing(worldY);
        }

        /// <summary>
        /// Возвращает распределение платформ по ширине уровня для указанной высоты.
        /// </summary>
        public float GetHorizontalDistribution(float worldY)
        {
            var stage = GetStage(worldY);
            return stage != null ? stage.GetHorizontalDistribution() : 0f;
        }

        /// <summary>
        /// Создает профиль контента активного этапа: collectibles, equipment и enemies.
        /// </summary>
        public IPlatformContentSource CreateContentSource(float worldY)
        {
            var stage = GetStage(worldY);
            if (stage == null)
            {
                return null;
            }

            return stage.CreateContentSource(worldY);
        }

        /// <summary>
        /// Пробует выбрать процедурного врага для указанной высоты из активного ScriptableObject-этапа.
        /// </summary>
        public bool TryGetProceduralEnemyPrefab(float worldY, System.Random random, out GameObject prefab)
        {
            var stage = GetStage(worldY);
            if (stage == null)
            {
                prefab = null;
                return false;
            }

            return stage.TryGetProceduralEnemyPrefab(worldY, random, out prefab);
        }

        /// <summary>
        /// Возвращает имя этапа для диагностики и overlay-окна.
        /// </summary>
        public string GetStageName(float worldY)
        {
            return GetStage(worldY)?.StageName ?? string.Empty;
        }

        /// <summary>
        /// Возвращает прогресс внутри текущего этапа в диапазоне 0..1.
        /// </summary>
        public float GetStageProgress(float worldY)
        {
            var stage = GetStage(worldY);
            return stage != null ? stage.CalculateProgress(worldY) : 0f;
        }

        private float CalculateProgress(float worldY)
        {
            var stage = GetStage(worldY);
            return stage != null ? stage.CalculateProgress(worldY) : 0f;
        }

        /// <summary>
        /// Возвращает стартовую высоту генерации из первого доступного этапа.
        /// </summary>
        private float GetInitialSpawnY()
        {
            var firstStage = GetFirstStage();
            return firstStage != null ? firstStage.StartY : 0f;
        }

        /// <summary>
        /// Находит первый этап по минимальной стартовой высоте.
        /// </summary>
        private GameplayDifficultyStage GetFirstStage()
        {
            if (stages == null || stages.Count == 0)
            {
                return null;
            }

            GameplayDifficultyStage firstStage = null;
            foreach (var stage in stages)
            {
                if (stage == null)
                {
                    continue;
                }

                if (firstStage == null || stage.StartY < firstStage.StartY)
                {
                    firstStage = stage;
                }
            }

            return firstStage;
        }

        /// <summary>
        /// Находит последний этап, стартовая высота которого уже пройдена.
        /// </summary>
        private GameplayDifficultyStage GetStage(float worldY)
        {
            if (!progressionEnabled || stages == null || stages.Count == 0)
            {
                return null;
            }

            GameplayDifficultyStage currentStage = null;
            foreach (var stage in stages)
            {
                if (stage == null || worldY < stage.StartY)
                {
                    continue;
                }

                if (currentStage == null || stage.StartY >= currentStage.StartY)
                {
                    currentStage = stage;
                }
            }

            // Последний достигнутый этап намеренно продолжается бесконечно.
            // verticalLength управляет только прогрессом кривых, а не выключением этапа.
            return currentStage;
        }
    }

    /// <summary>
    /// Интерфейс чтения прогрессии сложности для gameplay-систем и диагностических окон.
    /// </summary>
    public interface IGameplayDifficultyProgression
    {
        float CurrentProgress { get; }
        string CurrentStageName { get; }
        bool HasStages { get; }
        float InitialSpawnY { get; }
        GameObject GetPlatformPrefab(float worldY, System.Random random);
        float GetVerticalSpacing(float worldY);
        float GetHorizontalDistribution(float worldY);
        IPlatformContentSource CreateContentSource(float worldY);
        bool TryGetProceduralEnemyPrefab(float worldY, System.Random random, out GameObject prefab);
        string GetStageName(float worldY);
        float GetStageProgress(float worldY);
    }
}
