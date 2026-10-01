using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Поведение ломкой платформы: после приземления выключает коллайдеры и визуально разбивает платформу.
    /// </summary>
    public sealed class FragilePlatformBehaviour : MonoBehaviour, IPlatformLandingHandler, IPlatformLifecycle
    {
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

        private CancellationTokenSource breakCancellation;
        private readonly List<SpriteRenderer> rendererBuffer = new();
        private readonly List<Collider2D> colliderBuffer = new();
        private readonly List<Transform> stalePoseKeys = new();
        private readonly Dictionary<Transform, LocalPose> initialBreakPoses = new();

        /// <summary>
        /// Кэширует цели выключения при загрузке платформы.
        /// </summary>
        private void Awake()
        {
            CacheTargetsIfNeeded();
        }

        /// <summary>
        /// Запускает разрушение платформы после посадки игрока.
        /// </summary>
        /// <returns>True, если нужно подавить стандартный автопрыжок игрока.</returns>
        public bool TryHandleLanding(JumpPlayerController player, PlatformLandingContext context)
        {
            if (breakCancellation == null)
            {
                breakCancellation = new CancellationTokenSource();
                BreakAfterDelayAsync(breakCancellation).Forget(Debug.LogException);
            }

            return suppressAutoJump;
        }

        /// <summary>
        /// Восстанавливает платформу при первом создании.
        /// </summary>
        public void Initialize(PlatformSpawnContext context)
        {
            Restore();
        }

        /// <summary>
        /// Восстанавливает платформу при рецикле.
        /// </summary>
        public void Recycle(PlatformSpawnContext context)
        {
            Restore();
        }

        /// <summary>
        /// Восстанавливает платформу при ручном сбросе.
        /// </summary>
        public void ResetPlatform()
        {
            Restore();
        }

        /// <summary>
        /// Ждет задержку и выключает выбранные части платформы.
        /// </summary>
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

                RefreshBreakVisualTargets();
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

        /// <summary>
        /// Останавливает разрушение и возвращает цели в активное состояние.
        /// </summary>
        private void OnDestroy()
        {
            CancelBreakTask();
        }

        private void Restore()
        {
            CancelBreakTask();

            CacheTargetsIfNeeded();
            RefreshBreakVisualTargets();
            RestoreBreakVisuals();
            SetTargetsEnabled(true);
        }

        /// <summary>
        /// Автоматически собирает коллайдеры и визуальные части, если они не назначены в инспекторе.
        /// </summary>
        private void CacheTargetsIfNeeded()
        {
            if (colliders == null || colliders.Length == 0)
            {
                colliderBuffer.Clear();
                GetComponentsInChildren(true, colliderBuffer);
                colliders = colliderBuffer.FindAll(IsPlatformBreakCollider).ToArray();
            }

            RefreshBreakVisualTargets();
        }

        /// <summary>
        /// Обновляет список текущих визуальных объектов, включая runtime-контент на платформе.
        /// </summary>
        private void RefreshBreakVisualTargets()
        {
            rendererBuffer.Clear();
            GetComponentsInChildren(true, rendererBuffer);
            hiddenOnBreak = rendererBuffer.FindAll(IsPlatformBreakRenderer).ToArray();
            breakParts = BuildBreakPartsFromRenderers(hiddenOnBreak);
            RemoveStaleInitialPoses();

            for (var index = 0; index < breakParts.Length; index++)
            {
                var part = breakParts[index];
                if (part == null || initialBreakPoses.ContainsKey(part))
                {
                    continue;
                }

                initialBreakPoses.Add(part, LocalPose.From(part));
            }
        }

        /// <summary>
        /// Включает или выключает коллайдеры и дополнительные Behaviour-компоненты платформы.
        /// </summary>
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
                if (behaviour != null)
                {
                    behaviour.enabled = enabled;
                }
            }
        }

        /// <summary>
        /// Разводит части платформы в стороны; если частей нет, просто скрывает renderers.
        /// </summary>
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

        /// <summary>
        /// Возвращает визуальные части платформы в исходное состояние.
        /// </summary>
        private void RestoreBreakVisuals()
        {
            if (breakParts != null)
            {
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

                    initialPose.ApplyTo(part);
                }
            }

            SetRenderersEnabled(true);
        }

        /// <summary>
        /// Применяет текущий прогресс разрушения к каждой визуальной части.
        /// </summary>
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

        /// <summary>
        /// Убирает из кэша позы уничтоженных runtime-объектов.
        /// </summary>
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

        /// <summary>
        /// Включает или выключает визуальные renderers платформы.
        /// </summary>
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

        /// <summary>
        /// Строит список уникальных Transform по SpriteRenderer дочерних частей платформы.
        /// </summary>
        private static Transform[] BuildBreakPartsFromRenderers(SpriteRenderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
            {
                return System.Array.Empty<Transform>();
            }

            var parts = new System.Collections.Generic.List<Transform>(renderers.Length);
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

        /// <summary>
        /// Возвращает направление разлета части: левая часть идет влево, правая - вправо.
        /// </summary>
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
