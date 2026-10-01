using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Комбинированное поведение платформы: движется по синусоиде до посадки игрока, затем ломается на части.
    /// </summary>
    public sealed class FragileMovingPlatformBehaviour : MonoBehaviour, IPlatformLandingHandler, IPlatformLifecycle
    {
        [Header("Movement")]
        [SerializeField] private Vector2 localMoveAxis = Vector2.right;
        [SerializeField] [Min(0f)] private float amplitude = 1.25f;
        [SerializeField] [Min(0.01f)] private float cycleDuration = 2.5f;
        [SerializeField] private bool useSpawnIndexOffset = true;

        [Header("Break")]
        [SerializeField] [Min(0f)] private float breakDelay = 0.08f;
        [SerializeField] [Min(0f)] private float breakAnimationDuration = 0.22f;
        [SerializeField] [Min(0f)] private float breakHorizontalOffset = 0.45f;
        [SerializeField] [Min(0f)] private float breakFallDistance = 0.45f;
        [SerializeField] [Min(0f)] private float breakRotation = 18f;
        [SerializeField] private bool suppressAutoJump;
        [SerializeField] private Behaviour[] disabledOnBreak;
        [SerializeField] private Collider2D[] colliders;
        [SerializeField] private Transform[] breakParts;
        [SerializeField] private SpriteRenderer[] hiddenOnBreak;

        private const float TwoPi = Mathf.PI * 2f;

        private readonly List<SpriteRenderer> rendererBuffer = new();
        private readonly List<Collider2D> colliderBuffer = new();
        private readonly List<Transform> stalePoseKeys = new();
        private readonly Dictionary<Transform, LocalPose> initialBreakPoses = new();

        private Transform cachedTransform;
        private Rigidbody2D body;
        private Vector3 basePosition;
        private Vector2 normalizedMoveAxis = Vector2.right;
        private float angularSpeed;
        private float phaseOffset;
        private bool isBroken;
        private CancellationTokenSource breakCancellation;

        private void Awake()
        {
            cachedTransform = transform;
            body = GetComponent<Rigidbody2D>();
            RefreshMoveAxis();
            CacheTargetsIfNeeded();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshMoveAxis();
        }
#endif

        private void Update()
        {
            if (isBroken || body != null)
            {
                return;
            }

            MoveTransform(Time.time);
        }

        private void FixedUpdate()
        {
            if (isBroken || body == null)
            {
                return;
            }

            body.MovePosition((Vector2)CalculatePosition(Time.fixedTime));
        }

        /// <summary>
        /// Запускает разрушение при посадке игрока сверху.
        /// </summary>
        public bool TryHandleLanding(JumpPlayerController player, PlatformLandingContext context)
        {
            if (breakCancellation == null && !isBroken)
            {
                breakCancellation = new CancellationTokenSource();
                BreakAfterDelayAsync(breakCancellation).Forget(Debug.LogException);
            }

            return suppressAutoJump;
        }

        /// <summary>
        /// Сбрасывает платформу при первом создании.
        /// </summary>
        public void Initialize(PlatformSpawnContext context)
        {
            ApplyContext(context);
            Restore();
        }

        /// <summary>
        /// Сбрасывает платформу при рецикле.
        /// </summary>
        public void Recycle(PlatformSpawnContext context)
        {
            ApplyContext(context);
            Restore();
        }

        /// <summary>
        /// Возвращает платформу к целой базовой позиции.
        /// </summary>
        public void ResetPlatform()
        {
            Restore();
        }

        private void MoveTransform(float time)
        {
            if (amplitude <= 0f || cycleDuration <= 0f)
            {
                return;
            }

            cachedTransform.position = CalculatePosition(time);
        }

        private Vector3 CalculatePosition(float time)
        {
            if (amplitude <= 0f || cycleDuration <= 0f)
            {
                return basePosition;
            }

            var phase = time * angularSpeed + phaseOffset * TwoPi;
            return basePosition + (Vector3)(normalizedMoveAxis * (Mathf.Sin(phase) * amplitude));
        }

        private void ApplyContext(PlatformSpawnContext context)
        {
            RefreshMoveAxis();
            basePosition = cachedTransform != null ? cachedTransform.position : transform.position;
            phaseOffset = useSpawnIndexOffset ? context.SpawnIndex * 0.173f : 0f;
        }

        private async UniTask BreakAfterDelayAsync(CancellationTokenSource cancellationSource)
        {
            try
            {
                var token = cancellationSource.Token;
                if (breakDelay > 0f)
                {
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(breakDelay),
                        DelayType.DeltaTime,
                        PlayerLoopTiming.Update,
                        token);
                }

                isBroken = true;
                RefreshBreakTargets();
                SetTargetsEnabled(false);
                await AnimateBreakVisualsAsync(token);
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                if (breakCancellation == cancellationSource)
                {
                    breakCancellation = null;
                    cancellationSource.Dispose();
                }
            }
        }

        private void OnDestroy()
        {
            CancelBreakTask();
        }

        private void Restore()
        {
            CancelBreakTask();

            isBroken = false;
            SetPlatformPosition(basePosition);
            CacheTargetsIfNeeded();
            RefreshBreakTargets();
            RestoreBreakVisuals();
            SetTargetsEnabled(true);
        }

        private void SetPlatformPosition(Vector3 position)
        {
            if (body != null)
            {
                body.position = position;
            }

            (cachedTransform != null ? cachedTransform : transform).position = position;
        }

        private void CacheTargetsIfNeeded()
        {
            if (colliders == null || colliders.Length == 0)
            {
                RefreshColliderTargets();
            }

            RefreshBreakTargets();
        }

        private void RefreshBreakTargets()
        {
            RefreshColliderTargets();

            rendererBuffer.Clear();
            GetComponentsInChildren(true, rendererBuffer);
            hiddenOnBreak = rendererBuffer.FindAll(IsPlatformBreakRenderer).ToArray();
            breakParts = BuildBreakPartsFromRenderers(hiddenOnBreak);
            RemoveStaleInitialPoses();

            for (var index = 0; index < breakParts.Length; index++)
            {
                var part = breakParts[index];
                if (part != null && !initialBreakPoses.ContainsKey(part))
                {
                    initialBreakPoses.Add(part, LocalPose.From(part));
                }
            }
        }

        private void RefreshColliderTargets()
        {
            colliderBuffer.Clear();
            GetComponentsInChildren(true, colliderBuffer);
            colliders = colliderBuffer.FindAll(IsPlatformBreakCollider).ToArray();
        }

        private void SetTargetsEnabled(bool enabled)
        {
            if (colliders != null)
            {
                foreach (var targetCollider in colliders)
                {
                    if (targetCollider != null)
                    {
                        targetCollider.enabled = enabled;
                    }
                }
            }

            if (disabledOnBreak == null)
            {
                return;
            }

            foreach (var behaviour in disabledOnBreak)
            {
                if (behaviour != null && behaviour != this)
                {
                    behaviour.enabled = enabled;
                }
            }
        }

        private async UniTask AnimateBreakVisualsAsync(CancellationToken cancellationToken)
        {
            if (breakParts == null || breakParts.Length == 0 || breakAnimationDuration <= 0f)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SetRenderersEnabled(false);
                return;
            }

            var elapsed = 0f;
            while (elapsed < breakAnimationDuration)
            {
                elapsed += Time.deltaTime;
                ApplyBreakVisualProgress(Mathf.Clamp01(elapsed / breakAnimationDuration));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            ApplyBreakVisualProgress(1f);
            SetRenderersEnabled(false);
        }

        private void CancelBreakTask()
        {
            if (breakCancellation == null)
            {
                return;
            }

            var cancellation = breakCancellation;
            breakCancellation = null;
            cancellation.Cancel();
            cancellation.Dispose();
        }

        private void RestoreBreakVisuals()
        {
            if (breakParts != null)
            {
                foreach (var part in breakParts)
                {
                    if (part == null)
                    {
                        continue;
                    }

                    if (!initialBreakPoses.TryGetValue(part, out var initialPose))
                    {
                        initialPose = LocalPose.From(part);
                        initialBreakPoses.Add(part, initialPose);
                    }

                    initialPose.ApplyTo(part);
                }
            }

            SetRenderersEnabled(true);
        }

        private void ApplyBreakVisualProgress(float progress)
        {
            if (breakParts == null)
            {
                return;
            }

            for (var index = 0; index < breakParts.Length; index++)
            {
                var part = breakParts[index];
                if (part == null)
                {
                    continue;
                }

                if (!initialBreakPoses.TryGetValue(part, out var initialPose))
                {
                    initialPose = LocalPose.From(part);
                    initialBreakPoses.Add(part, initialPose);
                }

                var side = GetBreakSide(index, breakParts.Length);
                var offset = new Vector3(side * breakHorizontalOffset, -breakFallDistance, 0f) * progress;
                part.localPosition = initialPose.LocalPosition + offset;
                part.localRotation = initialPose.LocalRotation * Quaternion.Euler(0f, 0f, -side * breakRotation * progress);
            }
        }

        private void RemoveStaleInitialPoses()
        {
            stalePoseKeys.Clear();
            foreach (var pair in initialBreakPoses)
            {
                if (pair.Key == null)
                {
                    stalePoseKeys.Add(pair.Key);
                }
            }

            foreach (var staleKey in stalePoseKeys)
            {
                initialBreakPoses.Remove(staleKey);
            }

            stalePoseKeys.Clear();
        }

        private void SetRenderersEnabled(bool enabled)
        {
            if (hiddenOnBreak == null)
            {
                return;
            }

            foreach (var spriteRenderer in hiddenOnBreak)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.enabled = enabled;
                }
            }
        }

        private void RefreshMoveAxis()
        {
            normalizedMoveAxis = localMoveAxis.sqrMagnitude > 0f ? localMoveAxis.normalized : Vector2.right;
            angularSpeed = cycleDuration > 0f ? TwoPi / cycleDuration : 0f;
        }

        private static Transform[] BuildBreakPartsFromRenderers(SpriteRenderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
            {
                return System.Array.Empty<Transform>();
            }

            var parts = new List<Transform>(renderers.Length);
            foreach (var spriteRenderer in renderers)
            {
                if (spriteRenderer == null || spriteRenderer.transform == null || parts.Contains(spriteRenderer.transform))
                {
                    continue;
                }

                parts.Add(spriteRenderer.transform);
            }

            parts.Sort((left, right) => left.localPosition.x.CompareTo(right.localPosition.x));
            return parts.ToArray();
        }

        private static bool IsPlatformBreakRenderer(SpriteRenderer spriteRenderer)
        {
            return spriteRenderer != null && spriteRenderer.GetComponentInParent<EnemyBase>(true) == null;
        }

        private static bool IsPlatformBreakCollider(Collider2D targetCollider)
        {
            return targetCollider != null && targetCollider.GetComponentInParent<EnemyBase>(true) == null;
        }

        private static float GetBreakSide(int index, int count)
        {
            if (count <= 1)
            {
                return 0f;
            }

            return index < count * 0.5f ? -1f : 1f;
        }

        private readonly struct LocalPose
        {
            public LocalPose(Vector3 localPosition, Quaternion localRotation)
            {
                LocalPosition = localPosition;
                LocalRotation = localRotation;
            }

            public Vector3 LocalPosition { get; }

            public Quaternion LocalRotation { get; }

            public static LocalPose From(Transform transform)
            {
                return new LocalPose(transform.localPosition, transform.localRotation);
            }

            public void ApplyTo(Transform transform)
            {
                transform.localPosition = LocalPosition;
                transform.localRotation = LocalRotation;
            }
        }
    }
}
