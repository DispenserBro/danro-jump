using System.Collections.Generic;
using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Runtime-контроллер платформы: хранит точки спавна контента и рассылает lifecycle-события.
    /// </summary>
    public sealed class PlatformController : MonoBehaviour, IPlatformRuntime, IPlatformLifecycle
    {
        [SerializeField] private List<PlatformSpawnPoint> collectibleSpawnPoints = new();
        [SerializeField] private List<PlatformSpawnPoint> equipmentSpawnPoints = new();
        [SerializeField] private List<PlatformSpawnPoint> enemySpawnPoints = new();

        /// <summary>
        /// Transform платформы для систем генерации и контента.
        /// </summary>
        public Transform Transform => transform;

        /// <summary>
        /// Точки, где на платформе можно размещать collectible-предметы.
        /// </summary>
        public IReadOnlyList<PlatformSpawnPoint> CollectibleSpawnPoints => collectibleSpawnPoints;

        /// <summary>
        /// Точки, где на платформе можно размещать специальное оборудование.
        /// </summary>
        public IReadOnlyList<PlatformSpawnPoint> EquipmentSpawnPoints => equipmentSpawnPoints;

        /// <summary>
        /// Точки, где на платформе можно размещать противников.
        /// </summary>
        public IReadOnlyList<PlatformSpawnPoint> EnemySpawnPoints => enemySpawnPoints;

        /// <summary>
        /// Порядковый номер спавна платформы в текущей генерации.
        /// </summary>
        public int SpawnIndex { get; private set; }

        /// <summary>
        /// Показывает, является ли платформа стартовой.
        /// </summary>
        public bool IsStartPlatform { get; private set; }

        /// <summary>
        /// Y-координата платформы в момент последней генерации или recycle.
        /// </summary>
        public float WorldY { get; private set; }

        /// <summary>
        /// Собирает точки спавна при загрузке платформы.
        /// </summary>
        private void Awake()
        {
            CollectSpawnPoints();
        }

        /// <summary>
        /// Обновляет списки точек спавна при изменениях prefab в редакторе.
        /// </summary>
        private void OnValidate()
        {
            CollectSpawnPoints();
        }

        /// <summary>
        /// Инициализирует платформу после первого создания.
        /// </summary>
        public void Initialize(PlatformSpawnContext context)
        {
            ApplyContext(context);
            NotifyChildrenInitialize(context);
        }

        /// <summary>
        /// Переиспользует платформу с новым контекстом генерации.
        /// </summary>
        public void Recycle(PlatformSpawnContext context)
        {
            ApplyContext(context);
            NotifyChildrenRecycle(context);
        }

        /// <summary>
        /// Сбрасывает дочерние lifecycle-компоненты платформы.
        /// </summary>
        public void ResetPlatform()
        {
            NotifyChildrenReset();
        }

        /// <summary>
        /// Запоминает данные текущего спавна платформы.
        /// </summary>
        private void ApplyContext(PlatformSpawnContext context)
        {
            SpawnIndex = context.SpawnIndex;
            IsStartPlatform = context.IsStartPlatform;
            WorldY = context.WorldY;
        }

        /// <summary>
        /// Собирает дочерние PlatformSpawnPoint по категориям контента.
        /// </summary>
        private void CollectSpawnPoints()
        {
            collectibleSpawnPoints.Clear();
            equipmentSpawnPoints.Clear();
            enemySpawnPoints.Clear();

            GetComponentsInChildren(true, collectibleBuffer);

            foreach (var spawnPoint in collectibleBuffer)
            {
                if (spawnPoint == null)
                {
                    continue;
                }

                GetSpawnPointList(spawnPoint.Kind).Add(spawnPoint);
            }

            collectibleBuffer.Clear();
        }

        /// <summary>
        /// Возвращает список точек спавна нужной категории.
        /// </summary>
        private List<PlatformSpawnPoint> GetSpawnPointList(PlatformContentKind kind)
        {
            switch (kind)
            {
                case PlatformContentKind.Collectible:
                    return collectibleSpawnPoints;
                case PlatformContentKind.Equipment:
                    return equipmentSpawnPoints;
                case PlatformContentKind.Enemy:
                    return enemySpawnPoints;
                default:
                    return collectibleSpawnPoints;
            }
        }

        /// <summary>
        /// Сообщает дочерним компонентам о первом создании платформы.
        /// </summary>
        private void NotifyChildrenInitialize(PlatformSpawnContext context)
        {
            GetComponentsInChildren(true, lifecycleBuffer);

            foreach (var behaviour in lifecycleBuffer)
            {
                if (behaviour is IPlatformLifecycle lifecycle && !ReferenceEquals(lifecycle, this))
                {
                    lifecycle.Initialize(context);
                }
            }

            lifecycleBuffer.Clear();
        }

        /// <summary>
        /// Сообщает дочерним компонентам о рецикле платформы.
        /// </summary>
        private void NotifyChildrenRecycle(PlatformSpawnContext context)
        {
            GetComponentsInChildren(true, lifecycleBuffer);

            foreach (var behaviour in lifecycleBuffer)
            {
                if (behaviour is IPlatformLifecycle lifecycle && !ReferenceEquals(lifecycle, this))
                {
                    lifecycle.Recycle(context);
                }
            }

            lifecycleBuffer.Clear();
        }

        /// <summary>
        /// Сообщает дочерним компонентам о сбросе платформы.
        /// </summary>
        private void NotifyChildrenReset()
        {
            GetComponentsInChildren(true, lifecycleBuffer);

            foreach (var behaviour in lifecycleBuffer)
            {
                if (behaviour is IPlatformLifecycle lifecycle && !ReferenceEquals(lifecycle, this))
                {
                    lifecycle.ResetPlatform();
                }
            }

            lifecycleBuffer.Clear();
        }

        private readonly List<PlatformSpawnPoint> collectibleBuffer = new();
        private readonly List<MonoBehaviour> lifecycleBuffer = new();
    }
}
