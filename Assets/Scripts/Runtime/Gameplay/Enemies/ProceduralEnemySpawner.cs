using UnityEngine;
using Zenject;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Создает летающих и универсальных врагов в диапазонах высоты, которые подготовил PlatformSpawner.
    /// Перед созданием проверяет, что область врага не пересекает платформенные коллайдеры.
    /// </summary>
    public sealed class ProceduralEnemySpawner : MonoBehaviour
    {
        [Header("Spawn cadence")]
        [SerializeField] [Min(0f)] private float initialSpawnY = 8f;
        [SerializeField] [Min(0.1f)] private float verticalStep = 12f;
        [SerializeField] [Min(0)] private int maxAliveEnemies = 8;
        [SerializeField] [Min(1)] private int spawnAttempts = 12;

        [Header("Spawn area")]
        [SerializeField] [Min(0f)] private float horizontalPadding = 0.85f;
        [SerializeField] [Min(0f)] private float verticalJitter = 1.5f;
        [SerializeField] private Vector2 fallbackEnemyHalfExtents = new(0.55f, 0.55f);
        [SerializeField] private Vector2 platformClearance = new(0.15f, 0.15f);
        [SerializeField] private LayerMask blockingMask = ~0;

        private const string PoolRootName = "Procedural Enemy Pool";
        private readonly Collider2D[] overlapBuffer = new Collider2D[16];
        private readonly System.Collections.Generic.List<GameObject> aliveEnemies = new();
        private readonly System.Collections.Generic.Dictionary<GameObject, System.Collections.Generic.List<GameObject>> inactivePool = new();
        private DiContainer container;
        private Camera gameplayCamera;
        private IGameplayDifficultyProgression difficultyProgression;
        private System.Random random;
        private Transform poolRoot;
        private float nextSpawnY;
        private bool isInitialized;

        /// <summary>
        /// Инициализирует спавнер из PlatformSpawner, чтобы seed и генерация были синхронизированы.
        /// </summary>
        public void Initialize(
            DiContainer injectedContainer,
            Camera injectedGameplayCamera,
            IGameplayDifficultyProgression injectedDifficultyProgression,
            int seed)
        {
            container = injectedContainer;
            gameplayCamera = injectedGameplayCamera;
            difficultyProgression = injectedDifficultyProgression;
            random = new System.Random(seed);
            nextSpawnY = initialSpawnY;
            isInitialized = true;
        }

        /// <summary>
        /// Создает врагов в новом вертикальном диапазоне, который только что сгенерировал PlatformSpawner.
        /// </summary>
        public void SpawnForGeneratedRange(float fromY, float toY)
        {
            CleanupDestroyedEnemies();
            if (!CanTrySpawn())
            {
                return;
            }

            var minY = Mathf.Min(fromY, toY);
            var maxY = Mathf.Max(fromY, toY);
            while (nextSpawnY < minY)
            {
                nextSpawnY += verticalStep;
            }

            while (nextSpawnY <= maxY && CanTrySpawn())
            {
                TrySpawnEnemyAtNextStep(minY, maxY);
                nextSpawnY += verticalStep;
            }
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

        private void Recycle(GameObject enemy)
        {
            if (enemy == null)
            {
                return;
            }

            if (enemy.TryGetComponent(out PlatformSpawnedContent marker) && marker.SourcePrefab != null)
            {
                enemy.SetActive(false);
                enemy.transform.SetParent(GetPoolRoot(), false);

                var sourcePrefab = marker.SourcePrefab;
                if (!inactivePool.TryGetValue(sourcePrefab, out var list))
                {
                    list = new System.Collections.Generic.List<GameObject>();
                    inactivePool.Add(sourcePrefab, list);
                }
                list.Add(enemy);
            }
            else
            {
                Destroy(enemy);
            }
        }

        /// <summary>
        /// Удаляет процедурных врагов ниже линии рецикла платформ с утилизацией в пул.
        /// </summary>
        public void CleanupBelow(float worldY)
        {
            for (var index = aliveEnemies.Count - 1; index >= 0; index--)
            {
                var enemy = aliveEnemies[index];
                if (enemy == null)
                {
                    aliveEnemies.RemoveAt(index);
                    continue;
                }

                if (enemy.transform.position.y < worldY)
                {
                    Recycle(enemy);
                    aliveEnemies.RemoveAt(index);
                }
            }
        }

        private bool CanTrySpawn()
        {
            return isInitialized &&
                gameplayCamera != null &&
                container != null &&
                difficultyProgression != null &&
                (maxAliveEnemies <= 0 || aliveEnemies.Count < maxAliveEnemies);
        }

        private void TrySpawnEnemyAtNextStep(float minGeneratedY, float maxGeneratedY)
        {
            if (!difficultyProgression.TryGetProceduralEnemyPrefab(nextSpawnY, random, out var prefab))
            {
                return;
            }

            var halfExtents = GetEnemyHalfExtents(prefab);
            for (var attempt = 0; attempt < spawnAttempts; attempt++)
            {
                var position = CreateSpawnPosition(minGeneratedY, maxGeneratedY, halfExtents);
                if (IntersectsBlockingCollider(position, halfExtents + platformClearance))
                {
                    continue;
                }

                Spawn(prefab, position);
                return;
            }
        }

        private Vector3 CreateSpawnPosition(float minGeneratedY, float maxGeneratedY, Vector2 halfExtents)
        {
            var cameraHalfWidth = gameplayCamera.orthographicSize * gameplayCamera.aspect;
            var minX = gameplayCamera.transform.position.x - cameraHalfWidth + horizontalPadding + halfExtents.x;
            var maxX = gameplayCamera.transform.position.x + cameraHalfWidth - horizontalPadding - halfExtents.x;
            var minY = minGeneratedY + halfExtents.y;
            var maxY = Mathf.Max(minY, maxGeneratedY - halfExtents.y);
            var jitteredY = nextSpawnY + RandomRange(-verticalJitter, verticalJitter);

            return new Vector3(
                RandomRange(minX, maxX),
                Mathf.Clamp(jitteredY, minY, maxY),
                0f);
        }

        private void Spawn(GameObject prefab, Vector3 position)
        {
            GameObject instance = null;
            if (inactivePool.TryGetValue(prefab, out var list) && list.Count > 0)
            {
                var lastIndex = list.Count - 1;
                instance = list[lastIndex];
                list.RemoveAt(lastIndex);

                if (instance != null)
                {
                    instance.transform.SetParent(transform, false);
                    instance.transform.position = position;
                    instance.SetActive(true);
                }
            }

            if (instance == null)
            {
                instance = container.InstantiatePrefab(prefab, position, Quaternion.identity, transform);
            }

            if (!instance.TryGetComponent(out PlatformSpawnedContent marker))
            {
                marker = instance.AddComponent<PlatformSpawnedContent>();
            }

            marker.SourcePrefab = prefab;
            marker.ResetForPlatformSpawn();

            aliveEnemies.Add(instance);
        }

        private bool IntersectsBlockingCollider(Vector3 position, Vector2 halfExtents)
        {
            var contactFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = blockingMask,
                useTriggers = false
            };

            var count = Physics2D.OverlapBox(position, halfExtents * 2f, 0f, contactFilter, overlapBuffer);
            for (var index = 0; index < count; index++)
            {
                var hit = overlapBuffer[index];
                if (hit != null && !hit.GetComponentInParent<EnemyBase>())
                {
                    return true;
                }
            }

            return false;
        }

        private Vector2 GetEnemyHalfExtents(GameObject prefab)
        {
            var collider = prefab != null ? prefab.GetComponentInChildren<Collider2D>(true) : null;
            if (collider == null)
            {
                return fallbackEnemyHalfExtents;
            }

            return new Vector2(
                Mathf.Max(fallbackEnemyHalfExtents.x, collider.bounds.extents.x),
                Mathf.Max(fallbackEnemyHalfExtents.y, collider.bounds.extents.y));
        }

        private void CleanupDestroyedEnemies()
        {
            for (var index = aliveEnemies.Count - 1; index >= 0; index--)
            {
                var enemy = aliveEnemies[index];
                if (enemy == null)
                {
                    aliveEnemies.RemoveAt(index);
                    continue;
                }

                if (!enemy.activeSelf || !enemy.activeInHierarchy)
                {
                    Recycle(enemy);
                    aliveEnemies.RemoveAt(index);
                }
            }
        }

        private float RandomRange(float min, float max)
        {
            if (max <= min)
            {
                return min;
            }

            return Mathf.Lerp(min, max, (float)(random?.NextDouble() ?? 0d));
        }

        private void OnDestroy()
        {
            foreach (var pair in inactivePool)
            {
                if (pair.Value != null)
                {
                    foreach (var obj in pair.Value)
                    {
                        if (obj != null)
                        {
                            Destroy(obj);
                        }
                    }
                    pair.Value.Clear();
                }
            }
            inactivePool.Clear();

            if (poolRoot != null)
            {
                Destroy(poolRoot.gameObject);
                poolRoot = null;
            }
        }
    }
}
