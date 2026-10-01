using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Zenject;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Генерирует и переиспользует платформы выше камеры с учетом текущего этапа сложности.
    /// </summary>
    public sealed class PlatformSpawner : MonoBehaviour
    {
        [Min(1)]
        [SerializeField] private int initialPlatformCount = 34;
        [Min(0f)]
        [SerializeField] private float horizontalPadding = 0.85f;
        [FormerlySerializedAs("recycleDistanceBelowCamera")]
        [SerializeField]
        [Min(0f)]
        [Tooltip("How far below the camera a platform must fall before it is recycled. Increase this to make platforms disappear lower.")]
        private float recycleDistanceBelowCameraBottom = 12f;
        [SerializeField] private bool useHardwareRandomSeed = true;
        [SerializeField] private int randomSeed = 1207;
        [SerializeField] private ProceduralEnemySpawner proceduralEnemySpawner;

        private readonly List<PlatformController> platforms = new();
        private readonly Dictionary<PlatformController, GameObject> prefabByPlatform = new();
        private readonly Dictionary<GameObject, float> platformHalfWidthByPrefab = new();
        private DiContainer container;
        private Camera gameplayCamera;
        private IGameplayDifficultyProgression difficultyProgression;
        private PlatformContentSpawner contentSpawner;
        private JumpPlayerController playerController;
        private System.Random random;
        private int currentRandomSeed;
        private float highestPlatformY;
        private int nextSpawnIndex;

        /// <summary>
        /// Seed, который реально использует генератор платформ в текущем запуске.
        /// </summary>
        public int CurrentRandomSeed => currentRandomSeed;

        /// <summary>
        /// Самая высокая Y-координата, до которой уже сгенерированы платформы.
        /// </summary>
        public float CurrentGenerationY => highestPlatformY;

        /// <summary>
        /// Имя этапа, по которому сейчас генерируются платформы.
        /// </summary>
        public string CurrentGenerationStage => GetStageName(highestPlatformY);

        /// <summary>
        /// Прогресс генерации внутри текущего этапа.
        /// </summary>
        public float CurrentGenerationStageProgress => ProgressionForDiagnostics != null ? ProgressionForDiagnostics.GetStageProgress(highestPlatformY) : 0f;

        /// <summary>
        /// Текущее распределение платформ по ширине уровня.
        /// </summary>
        public float CurrentPlatformDistribution => ProgressionForDiagnostics != null ? ProgressionForDiagnostics.GetHorizontalDistribution(highestPlatformY) : 0f;

        private IGameplayDifficultyProgression ProgressionForDiagnostics => difficultyProgression;

        /// <summary>
        /// Получает зависимости через SceneContext: контейнер, игрока, камеру, контент-спавнер и прогрессию.
        /// </summary>
        [Inject]
        public void Construct(
            DiContainer injectedContainer,
            JumpPlayerController playerController,
            Camera injectedGameplayCamera,
            PlatformContentSpawner injectedContentSpawner,
            IGameplayDifficultyProgression injectedDifficultyProgression)
        {
            container = injectedContainer;
            this.playerController = playerController;
            gameplayCamera = injectedGameplayCamera;
            contentSpawner = injectedContentSpawner;
            difficultyProgression = injectedDifficultyProgression;
        }

        /// <summary>
        /// Инициализирует генератор случайных чисел и выводит выбранный seed в консоль.
        /// </summary>
        private void Awake()
        {
            currentRandomSeed = useHardwareRandomSeed ? CreateHardwareRandomSeed() : randomSeed;
            random = new System.Random(currentRandomSeed);
            Debug.Log($"{nameof(PlatformSpawner)} random seed: {currentRandomSeed} ({(useHardwareRandomSeed ? "hardware" : "manual")}).", this);
        }

        /// <summary>
        /// Проверяет runtime-зависимости и создает стартовый набор платформ.
        /// </summary>
        private void Start()
        {
            if (!ValidateRuntimeState())
            {
                return;
            }

            ResolveProceduralEnemySpawner();
            proceduralEnemySpawner?.Initialize(container, gameplayCamera, difficultyProgression, currentRandomSeed);
            GenerateInitialPlatforms();
        }

        /// <summary>
        /// Каждый кадр переносит платформы, которые ушли ниже камеры, наверх уровня.
        /// </summary>
        private void Update()
        {
            RecyclePlatformsBelowCamera();
        }

        private void OnDestroy()
        {
            if (contentSpawner != null)
            {
                // Перед уничтожением сцены очищаем дочерний контент платформ, чтобы не оставлять связи на prefab.
                foreach (var platform in platforms)
                {
                    contentSpawner.ClearSpawnedContent(platform);
                }
            }

            prefabByPlatform.Clear();
            proceduralEnemySpawner?.CleanupBelow(float.PositiveInfinity);
        }

        /// <summary>
        /// Создает стартовую площадку и начальную вертикальную цепочку платформ.
        /// </summary>
        private void GenerateInitialPlatforms()
        {
            highestPlatformY = difficultyProgression.InitialSpawnY;
            nextSpawnIndex = 0;
            SpawnPlatform(GetPlatformPrefab(highestPlatformY), new Vector2(GetCameraCenterX(), highestPlatformY), true);

            for (var index = 1; index < initialPlatformCount; index++)
            {
                var previousGenerationY = highestPlatformY;
                highestPlatformY += RandomVerticalSpacing();
                var selectedPlatformPrefab = GetPlatformPrefab(highestPlatformY);
                SpawnPlatform(selectedPlatformPrefab, new Vector2(RandomHorizontalPosition(highestPlatformY, selectedPlatformPrefab), highestPlatformY), false);
                proceduralEnemySpawner?.SpawnForGeneratedRange(previousGenerationY, highestPlatformY);
            }
        }

        /// <summary>
        /// Находит платформы ниже камеры и переносит их выше текущей генерации.
        /// </summary>
        private void RecyclePlatformsBelowCamera()
        {
            if (gameplayCamera == null || platforms.Count == 0)
            {
                return;
            }

            var bottomLimit = gameplayCamera.transform.position.y - recycleDistanceBelowCameraBottom;
            proceduralEnemySpawner?.CleanupBelow(bottomLimit);

            // Вместо постоянного Instantiate/Destroy переиспользуем платформы ниже видимой зоны.
            for (var index = 0; index < platforms.Count; index++)
            {
                var platform = platforms[index];
                if (platform == null || platform.transform.position.y >= bottomLimit)
                {
                    continue;
                }

                var previousGenerationY = highestPlatformY;
                highestPlatformY += RandomVerticalSpacing();
                var selectedPlatformPrefab = GetPlatformPrefab(highestPlatformY);
                var position = new Vector2(RandomHorizontalPosition(highestPlatformY, selectedPlatformPrefab), highestPlatformY);
                platforms[index] = RecyclePlatform(platform, selectedPlatformPrefab, position);
                proceduralEnemySpawner?.SpawnForGeneratedRange(previousGenerationY, highestPlatformY);
            }
        }

        /// <summary>
        /// Создает новую платформу и инициализирует ее контекстом спавна.
        /// </summary>
        private void SpawnPlatform(GameObject selectedPlatformPrefab, Vector2 position, bool isStartPlatform)
        {
            var platform = CreatePlatform(selectedPlatformPrefab, position, isStartPlatform);
            if (platform == null)
            {
                return;
            }

            platforms.Add(platform);
            InitializePlatform(platform, CreateContext(platform, isStartPlatform));
        }

        /// <summary>
        /// Переиспользует платформу или заменяет ее, если активный этап выбрал другой prefab.
        /// </summary>
        private PlatformController RecyclePlatform(PlatformController platform, GameObject selectedPlatformPrefab, Vector2 position)
        {
            if (ShouldReplacePlatform(platform, selectedPlatformPrefab))
            {
                // Если новый этап требует другой prefab, безопаснее заменить объект, чем мутировать старый.
                return ReplacePlatform(platform, selectedPlatformPrefab, position);
            }

            contentSpawner.ClearSpawnedContent(platform);
            platform.gameObject.SetActive(true);
            platform.transform.position = new Vector3(position.x, position.y, platform.transform.position.z);
            RecyclePlatform(platform, CreateContext(platform, false));
            return platform;
        }

        /// <summary>
        /// Создает экземпляр платформы через DI-контейнер, чтобы дочерние компоненты получили injection.
        /// </summary>
        private PlatformController CreatePlatform(GameObject prefab, Vector2 position, bool isStartPlatform)
        {
            if (prefab == null)
            {
                Debug.LogError($"{nameof(PlatformSpawner)} cannot spawn a platform because no platform prefab is configured.", this);
                return null;
            }

            var platformObject = container.InstantiatePrefab(prefab, new Vector3(position.x, position.y, 0f), Quaternion.identity, transform);
            platformObject.name = isStartPlatform ? "Start Platform" : "Jump Platform";
            platformObject.SetActive(true);

            if (!platformObject.TryGetComponent(out PlatformController platform))
            {
                Debug.LogError($"{nameof(PlatformSpawner)} spawned platform without {nameof(PlatformController)}.", platformObject);
                Destroy(platformObject);
                return null;
            }

            prefabByPlatform[platform] = prefab;
            return platform;
        }

        /// <summary>
        /// Удаляет старый экземпляр платформы и создает новый через DI-контейнер.
        /// </summary>
        private PlatformController ReplacePlatform(PlatformController platform, GameObject selectedPlatformPrefab, Vector2 position)
        {
            contentSpawner.ClearSpawnedContent(platform);
            prefabByPlatform.Remove(platform);
            Destroy(platform.gameObject);

            var replacement = CreatePlatform(selectedPlatformPrefab, position, false);
            if (replacement == null)
            {
                return null;
            }

            InitializePlatform(replacement, CreateContext(replacement, false));
            return replacement;
        }

        /// <summary>
        /// Проверяет, нужно ли заменить платформу из-за смены prefab активного этапа.
        /// </summary>
        private bool ShouldReplacePlatform(PlatformController platform, GameObject selectedPlatformPrefab)
        {
            if (platform == null || selectedPlatformPrefab == null)
            {
                return false;
            }

            return prefabByPlatform.TryGetValue(platform, out var currentPrefab) && currentPrefab != selectedPlatformPrefab;
        }

        /// <summary>
        /// Выполняет первичную инициализацию платформы и спавнит ее содержимое.
        /// </summary>
        private void InitializePlatform(PlatformController platform, PlatformSpawnContext context)
        {
            platform.Initialize(context);
            contentSpawner.SpawnContent(platform, GetContentSource(context.WorldY), random);
        }

        /// <summary>
        /// Передает платформе новый контекст recycle и пересоздает контент.
        /// </summary>
        private void RecyclePlatform(PlatformController platform, PlatformSpawnContext context)
        {
            platform.Recycle(context);
            contentSpawner.SpawnContent(platform, GetContentSource(context.WorldY), random);
        }

        /// <summary>
        /// Создает контекст, который платформа и ее содержимое используют при инициализации.
        /// </summary>
        private PlatformSpawnContext CreateContext(PlatformController platform, bool isStartPlatform)
        {
            return new PlatformSpawnContext(container, playerController, gameplayCamera, nextSpawnIndex++, isStartPlatform, platform.transform.position.y);
        }

        /// <summary>
        /// Проверяет, что SceneContext передал все зависимости, необходимые для генерации.
        /// </summary>
        private bool ValidateRuntimeState()
        {
            var isValid = true;

            if (container == null)
            {
                Debug.LogError($"{nameof(PlatformSpawner)} was not injected. Check SceneContext and {nameof(GameplayInstaller)} setup.", this);
                isValid = false;
            }

            if (playerController == null)
            {
                Debug.LogError($"{nameof(PlatformSpawner)} has no player reference.", this);
                isValid = false;
            }

            if (gameplayCamera == null)
            {
                Debug.LogError($"{nameof(PlatformSpawner)} has no gameplay camera reference.", this);
                isValid = false;
            }

            if (contentSpawner == null)
            {
                Debug.LogError($"{nameof(PlatformSpawner)} has no platform content spawner.", this);
                isValid = false;
            }

            if (difficultyProgression == null)
            {
                Debug.LogError($"{nameof(PlatformSpawner)} has no difficulty progression reference.", this);
                isValid = false;
            }
            else if (!difficultyProgression.HasStages)
            {
                Debug.LogError($"{nameof(PlatformSpawner)} has no difficulty stages configured.", this);
                isValid = false;
            }

            return isValid;
        }

        /// <summary>
        /// Находит procedural enemy spawner рядом с генератором платформ, если ссылка не задана в инспекторе.
        /// </summary>
        private void ResolveProceduralEnemySpawner()
        {
            if (proceduralEnemySpawner == null)
            {
                proceduralEnemySpawner = GetComponentInChildren<ProceduralEnemySpawner>(true);
            }
        }

        /// <summary>
        /// Выбирает X-позицию платформы так, чтобы ее края оставались внутри границ камеры.
        /// </summary>
        private float RandomHorizontalPosition(float worldY, GameObject platformPrefab)
        {
            var halfWidth = gameplayCamera != null ? gameplayCamera.orthographicSize * gameplayCamera.aspect : 3.75f;
            var platformHalfWidth = GetPlatformHalfWidth(platformPrefab);
            var usableHalfWidth = Mathf.Max(0f, halfWidth - horizontalPadding - platformHalfWidth);
            var centerX = GetCameraCenterX();

            if (usableHalfWidth <= 0f)
            {
                return centerX;
            }

            var distribution = difficultyProgression != null
                ? difficultyProgression.GetHorizontalDistribution(worldY)
                : 0f;

            var signed = RandomRange(-1f, 1f);
            var sign = signed < 0f ? -1f : 1f;
            var magnitude = Mathf.Abs(signed);

            // distribution -1 сжимает позиции к центру, +1 вытягивает их к краям.
            var normalizedMagnitude = distribution < 0f
                ? magnitude * (distribution + 1f)
                : Mathf.Lerp(magnitude, 1f, distribution);

            return centerX + sign * normalizedMagnitude * usableHalfWidth;
        }

        private float GetCameraCenterX()
        {
            return gameplayCamera != null ? gameplayCamera.transform.position.x : 0f;
        }

        private float GetPlatformHalfWidth(GameObject platformPrefab)
        {
            if (platformPrefab == null)
            {
                return 0f;
            }

            if (platformHalfWidthByPrefab.TryGetValue(platformPrefab, out var cachedHalfWidth))
            {
                return cachedHalfWidth;
            }

            var rootX = platformPrefab.transform.position.x;
            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;

            var renderers = platformPrefab.GetComponentsInChildren<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                EncapsulateBounds(renderers[index].bounds, ref minX, ref maxX);
            }

            var colliders = platformPrefab.GetComponentsInChildren<Collider2D>(true);
            for (var index = 0; index < colliders.Length; index++)
            {
                EncapsulateBounds(colliders[index].bounds, ref minX, ref maxX);
            }

            var halfWidth = float.IsInfinity(minX) || float.IsInfinity(maxX)
                ? 0f
                : Mathf.Max(Mathf.Abs(minX - rootX), Mathf.Abs(maxX - rootX));

            platformHalfWidthByPrefab[platformPrefab] = halfWidth;
            return halfWidth;
        }

        private static void EncapsulateBounds(Bounds bounds, ref float minX, ref float maxX)
        {
            if (bounds.size.x <= 0f)
            {
                return;
            }

            minX = Mathf.Min(minX, bounds.min.x);
            maxX = Mathf.Max(maxX, bounds.max.x);
        }

        private float RandomRange(float min, float max)
        {
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }

        /// <summary>
        /// Генерирует seed из времени и характеристик текущего устройства.
        /// </summary>
        private static int CreateHardwareRandomSeed()
        {
            var unixTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var seedSource = string.Concat(
                unixTime,
                "|",
                SystemInfo.processorType,
                "|",
                SystemInfo.processorCount,
                "|",
                SystemInfo.graphicsDeviceName);

            return StableHash(seedSource);
        }

        /// <summary>
        /// Стабильно превращает строку источников entropy в положительный int seed.
        /// </summary>
        private static int StableHash(string value)
        {
            unchecked
            {
                const int offsetBasis = -2128831035;
                const int prime = 16777619;
                var hash = offsetBasis;

                for (var index = 0; index < value.Length; index++)
                {
                    hash ^= value[index];
                    hash *= prime;
                }

                return hash == int.MinValue ? int.MaxValue : Mathf.Abs(hash);
            }
        }

        /// <summary>
        /// Возвращает вертикальный шаг до следующей платформы из текущего этапа.
        /// </summary>
        private float RandomVerticalSpacing()
        {
            if (difficultyProgression != null)
            {
                return difficultyProgression.GetVerticalSpacing(highestPlatformY);
            }

            return 0f;
        }

        /// <summary>
        /// Выбирает prefab платформы для указанной высоты.
        /// </summary>
        private GameObject GetPlatformPrefab(float worldY)
        {
            return difficultyProgression != null
                ? difficultyProgression.GetPlatformPrefab(worldY, random)
                : null;
        }

        /// <summary>
        /// Создает источник контента для указанной высоты генерации.
        /// </summary>
        private IPlatformContentSource GetContentSource(float worldY)
        {
            return difficultyProgression != null
                ? difficultyProgression.CreateContentSource(worldY)
                : null;
        }

        /// <summary>
        /// Возвращает имя этапа для diagnostics overlay.
        /// </summary>
        private string GetStageName(float worldY)
        {
            return ProgressionForDiagnostics != null ? ProgressionForDiagnostics.GetStageName(worldY) : string.Empty;
        }
    }
}
