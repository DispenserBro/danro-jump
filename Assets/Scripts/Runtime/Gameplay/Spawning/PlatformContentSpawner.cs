using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Создает collectibles, equipment и enemies на точках спавна платформы.
    /// </summary>
    public sealed class PlatformContentSpawner
    {
        private const string EnemyRootName = "Enemy Pool";
        private const string PoolRootName = "Platform Content Pool";

        private readonly DiContainer container;
        private readonly Dictionary<PlatformController, List<GameObject>> spawnedContentByPlatform = new();
        private readonly List<PlatformSpawnedContent> orphanMarkerBuffer = new();
        private readonly List<PlatformSpawnPoint> spawnPointCandidates = new();
        private readonly Dictionary<GameObject, List<GameObject>> inactivePool = new();
        private Transform enemyRoot;
        private Transform poolRoot;

        /// <summary>
        /// Получает DI-контейнер для создания prefab с injection.
        /// </summary>
        public PlatformContentSpawner(DiContainer container)
        {
            this.container = container;
        }

        private Transform GetPoolRoot()
        {
            if (poolRoot != null)
            {
                return poolRoot;
            }

            var rootObject = GameObject.Find(PoolRootName);
            if (rootObject == null)
            {
                rootObject = new GameObject(PoolRootName);
                rootObject.SetActive(false);
            }

            poolRoot = rootObject.transform;
            return poolRoot;
        }

        private void RecycleOrDestroy(GameObject content)
        {
            if (content == null)
            {
                return;
            }

            if (!content.scene.isLoaded)
            {
                Object.Destroy(content);
                return;
            }

            if (content.TryGetComponent(out PlatformSpawnedContent marker) && marker.SourcePrefab != null)
            {
                content.SetActive(false);
                content.transform.SetParent(GetPoolRoot(), false);

                var sourcePrefab = marker.SourcePrefab;
                if (!inactivePool.TryGetValue(sourcePrefab, out var list))
                {
                    list = new List<GameObject>();
                    inactivePool.Add(sourcePrefab, list);
                }
                list.Add(content);
            }
            else
            {
                Object.Destroy(content);
            }
        }

        /// <summary>
        /// Очищает старый контент платформы и спавнит новый по правилам источника.
        /// </summary>
        public void SpawnContent(
            PlatformController platform,
            IPlatformContentSource catalog,
            System.Random random)
        {
            ClearSpawnedContent(platform);

            if (platform == null || catalog == null)
            {
                return;
            }

            SpawnIntoPoints(platform, catalog, PlatformContentKind.Collectible, platform.CollectibleSpawnPoints, random);
            SpawnIntoPoints(platform, catalog, PlatformContentKind.Equipment, platform.EquipmentSpawnPoints, random);
            SpawnIntoPoints(platform, catalog, PlatformContentKind.Enemy, platform.EnemySpawnPoints, random);
        }

        /// <summary>
        /// Удаляет весь контент, созданный для указанной платформы.
        /// </summary>
        public void ClearSpawnedContent(PlatformController platform)
        {
            if (platform == null)
            {
                return;
            }

            if (!ClearTrackedContent(platform))
            {
                ClearOrphanSpawnedContent(platform);
            }
        }

        /// <summary>
        /// Очищает контент всех отслеживаемых платформ и полностью освобождает пул.
        /// </summary>
        public void ClearAll()
        {
            foreach (var pair in spawnedContentByPlatform)
            {
                ClearContentList(pair.Value);
            }

            spawnedContentByPlatform.Clear();

            foreach (var pair in inactivePool)
            {
                if (pair.Value != null)
                {
                    foreach (var obj in pair.Value)
                    {
                        if (obj != null)
                        {
                            Object.Destroy(obj);
                        }
                    }
                    pair.Value.Clear();
                }
            }
            inactivePool.Clear();

            if (poolRoot != null)
            {
                Object.Destroy(poolRoot.gameObject);
                poolRoot = null;
            }
        }

        /// <summary>
        /// Спавнит одну категорию контента в одну выбранную точку платформы.
        /// </summary>
        private void SpawnIntoPoints(
            PlatformController platform,
            IPlatformContentSource catalog,
            PlatformContentKind kind,
            IReadOnlyList<PlatformSpawnPoint> spawnPoints,
            System.Random random)
        {
            if (spawnPoints == null || !catalog.ShouldSpawn(kind, random) || !catalog.TryGetPrefab(kind, random, out var prefab))
            {
                return;
            }

            var selectedSpawnPoint = SelectSpawnPoint(spawnPoints, random);
            if (selectedSpawnPoint != null)
            {
                Spawn(prefab, selectedSpawnPoint.Anchor, platform);
            }
        }

        /// <summary>
        /// Создает конкретный prefab в точке спавна с поддержкой пулинга.
        /// </summary>
        private void Spawn(GameObject prefab, Transform parent, PlatformController platform)
        {
            var targetParent = parent != null ? parent : platform.transform;
            var isEnemy = EnemyPrefabRules.TryGetEnemy(prefab, out _);
            var instanceParent = isEnemy ? GetEnemyRoot(platform) : targetParent;

            GameObject instance = null;
            if (inactivePool.TryGetValue(prefab, out var list) && list.Count > 0)
            {
                var lastIndex = list.Count - 1;
                instance = list[lastIndex];
                list.RemoveAt(lastIndex);

                if (instance != null)
                {
                    instance.transform.SetParent(instanceParent, false);
                    instance.transform.SetPositionAndRotation(targetParent.position, targetParent.rotation);
                    instance.SetActive(true);
                }
            }

            if (instance == null)
            {
                instance = container.InstantiatePrefab(prefab, targetParent.position, targetParent.rotation, instanceParent);
            }

            if (isEnemy)
            {
                instance.transform.SetPositionAndRotation(targetParent.position, targetParent.rotation);
            }

            // Маркер нужен, чтобы при аварийной очистке найти даже неотслеженный дочерний контент.
            if (!instance.TryGetComponent(out PlatformSpawnedContent marker))
            {
                marker = instance.AddComponent<PlatformSpawnedContent>();
            }

            marker.SourcePrefab = prefab;
            marker.ResetForPlatformSpawn();

            if (instance.TryGetComponent(out IPlatformEquipment equipment))
            {
                equipment.Install(platform);
            }

            if (!spawnedContentByPlatform.TryGetValue(platform, out var spawnedContent))
            {
                spawnedContent = new List<GameObject>();
                spawnedContentByPlatform.Add(platform, spawnedContent);
            }

            spawnedContent.Add(instance);
        }

        /// <summary>
        /// Возвращает отдельный контейнер для врагов, чтобы платформы не управляли их визуалами и коллайдерами как своими дочерними объектами.
        /// </summary>
        private Transform GetEnemyRoot(PlatformController platform)
        {
            if (enemyRoot != null)
            {
                return enemyRoot;
            }

            var rootObject = GameObject.Find(EnemyRootName);
            if (rootObject == null)
            {
                rootObject = new GameObject(EnemyRootName);
            }

            enemyRoot = rootObject.transform;
            if (platform != null && platform.transform.parent != null && enemyRoot.parent == null)
            {
                enemyRoot.SetParent(platform.transform.parent, false);
            }

            return enemyRoot;
        }

        /// <summary>
        /// Удаляет контент, который был записан в словарь отслеживания.
        /// </summary>
        private bool ClearTrackedContent(PlatformController platform)
        {
            if (!spawnedContentByPlatform.TryGetValue(platform, out var spawnedContent))
            {
                return false;
            }

            ClearContentList(spawnedContent);
            spawnedContent.Clear();
            spawnedContentByPlatform.Remove(platform);
            return true;
        }

        /// <summary>
        /// Утилизирует или уничтожает объекты из уже выделенного списка без освобождения самого списка.
        /// </summary>
        private void ClearContentList(List<GameObject> spawnedContent)
        {
            for (var index = spawnedContent.Count - 1; index >= 0; index--)
            {
                var content = spawnedContent[index];
                if (content == null)
                {
                    spawnedContent.RemoveAt(index);
                    continue;
                }

                if (content.TryGetComponent(out PlatformSpawnedContent marker) && marker.IsDetachedFromPlatform)
                {
                    spawnedContent.RemoveAt(index);
                    continue;
                }

                RecycleOrDestroy(content);
                spawnedContent.RemoveAt(index);
            }
        }

        /// <summary>
        /// Fallback-очистка дочерних объектов с утилизацией в пул.
        /// </summary>
        private void ClearOrphanSpawnedContent(PlatformController platform)
        {
            platform.GetComponentsInChildren(true, orphanMarkerBuffer);

            for (var index = orphanMarkerBuffer.Count - 1; index >= 0; index--)
            {
                var marker = orphanMarkerBuffer[index];
                if (marker != null)
                {
                    if (marker.IsDetachedFromPlatform)
                    {
                        continue;
                    }

                    RecycleOrDestroy(marker.gameObject);
                }
            }

            orphanMarkerBuffer.Clear();
        }

        /// <summary>
        /// Проверяет, нужно ли пропустить optional-точку спавна по ее весу.
        /// </summary>
        private static bool ShouldSkipOptionalPoint(PlatformSpawnPoint spawnPoint, System.Random random)
        {
            if (!spawnPoint.Optional)
            {
                return false;
            }

            if (spawnPoint.Weight <= 0f)
            {
                return true;
            }

            if (spawnPoint.Weight >= 1f)
            {
                return false;
            }

            var roll = random != null ? random.NextDouble() : 0d;
            return roll > spawnPoint.Weight;
        }

        /// <summary>
        /// Выбирает одну подходящую точку по весам, чтобы плотность контента не зависела от числа маркеров на prefab.
        /// </summary>
        private PlatformSpawnPoint SelectSpawnPoint(
            IReadOnlyList<PlatformSpawnPoint> spawnPoints,
            System.Random random)
        {
            spawnPointCandidates.Clear();
            var totalWeight = 0f;
            for (var index = 0; index < spawnPoints.Count; index++)
            {
                var spawnPoint = spawnPoints[index];
                if (spawnPoint == null || ShouldSkipOptionalPoint(spawnPoint, random))
                {
                    continue;
                }

                spawnPointCandidates.Add(spawnPoint);
                totalWeight += Mathf.Max(0f, spawnPoint.Weight);
            }

            if (totalWeight <= 0f)
            {
                spawnPointCandidates.Clear();
                return null;
            }

            var roll = (float)((random != null ? random.NextDouble() : 0d) * totalWeight);
            for (var index = 0; index < spawnPointCandidates.Count; index++)
            {
                var spawnPoint = spawnPointCandidates[index];
                roll -= Mathf.Max(0f, spawnPoint.Weight);
                if (roll <= 0f)
                {
                    spawnPointCandidates.Clear();
                    return spawnPoint;
                }
            }

            spawnPointCandidates.Clear();
            return null;
        }
    }
}
