using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Противник, который патрулирует локальный отрезок и может быть побежден прыжком сверху.
    /// </summary>
    public sealed class PatrolEnemy : EnemyBase
    {
        private enum FacingScaleAxis
        {
            X = 0,
            Y = 1
        }

        private enum FacingFlipMode
        {
            ScaleAxis = 0,
            SpriteRendererAndCollider = 1
        }

        [SerializeField] private Vector2 localPatrolAxis = Vector2.right;
        [SerializeField] [Min(0f)] private float patrolDistance = 1.2f;
        [SerializeField] [Min(0f)] private float patrolSpeed = 1.4f;
        [SerializeField] private bool flipWhenMovingBack = true;
        [SerializeField] private bool visualFacesPositiveDirection = true;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private FacingScaleAxis facingScaleAxis = FacingScaleAxis.X;
        [SerializeField] private FacingFlipMode facingFlipMode = FacingFlipMode.ScaleAxis;
        [SerializeField] private bool mirrorColliderWithScaleFacing;

        private Transform cachedTransform;
        private Vector3 visualRootInitialScale = Vector3.one;
        private SpriteRenderer[] facingRenderers;
        private FlippableCollider[] flippableColliders;
        private Vector3 basePosition;
        private Vector2 normalizedPatrolAxis = Vector2.right;
        private Vector3 patrolAxis = Vector3.right;
        private int direction = 1;
        private int appliedFacingDirection = int.MinValue;
        private CancellationTokenSource patrolCancellation;
        private const PlayerLoopTiming PatrolPlayerLoopTiming = PlayerLoopTiming.Update;

        /// <summary>
        /// Запоминает локальную стартовую позицию патруля.
        /// </summary>
        private void Awake()
        {
            cachedTransform = transform;
            ResolveVisualRoot();
            CacheFacingTargets();
            RefreshPatrolAxis();
            basePosition = cachedTransform.localPosition;
            ApplyFacing();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshPatrolAxis();
        }
#endif

        public override void ActivateEnemy()
        {
            base.ActivateEnemy();
            StartPatrolTask();
        }

        public override void DeactivateEnemy()
        {
            StopPatrolTask();
            base.DeactivateEnemy();
        }

        private void OnDestroy()
        {
            StopPatrolTask();
        }

        /// <summary>
        /// Возвращает противника в начало патруля при рецикле платформы.
        /// </summary>
        public override void ResetEnemy()
        {
            StopPatrolTask();
            base.ActivateEnemy();
            (cachedTransform != null ? cachedTransform : transform).localPosition = basePosition;
            direction = 1;
            appliedFacingDirection = int.MinValue;
            ApplyFacing();
            StartPatrolTask();
        }

        private void RefreshPatrolAxis()
        {
            normalizedPatrolAxis = localPatrolAxis.sqrMagnitude > 0f ? localPatrolAxis.normalized : Vector2.right;
            patrolAxis = normalizedPatrolAxis;
        }

        /// <summary>
        /// Находит визуальный корень, который можно отражать без изменения коллайдера.
        /// </summary>
        private void ResolveVisualRoot()
        {
            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            visualRootInitialScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        }

        /// <summary>
        /// Разворачивает визуал при движении назад, если это включено в инспекторе.
        /// </summary>
        private void ApplyFacing()
        {
            if (visualRoot == null)
            {
                return;
            }

            if (appliedFacingDirection == direction)
            {
                return;
            }

            var shouldFlip = flipWhenMovingBack && direction < 0;
            if (!visualFacesPositiveDirection)
            {
                shouldFlip = !shouldFlip;
            }

            var scale = visualRootInitialScale;
            if (flipWhenMovingBack)
            {
                if (facingFlipMode == FacingFlipMode.SpriteRendererAndCollider)
                {
                    ApplyRendererAndColliderFacing(shouldFlip);
                    appliedFacingDirection = direction;
                    return;
                }

                var sign = shouldFlip ? -1f : 1f;
                if (facingScaleAxis == FacingScaleAxis.Y)
                {
                    scale.y = Mathf.Abs(scale.y) * sign;
                }
                else
                {
                    scale.x = Mathf.Abs(scale.x) * sign;
                }
            }

            visualRoot.localScale = scale;
            if (facingFlipMode == FacingFlipMode.ScaleAxis && mirrorColliderWithScaleFacing)
            {
                ApplyColliderFacing(shouldFlip);
            }

            appliedFacingDirection = direction;
        }

        private void CacheFacingTargets()
        {
            var targetRoot = visualRoot != null ? visualRoot : transform;
            facingRenderers = targetRoot.GetComponentsInChildren<SpriteRenderer>(true);
            var colliders = GetComponentsInChildren<Collider2D>(true);
            flippableColliders = new FlippableCollider[colliders.Length];

            for (var index = 0; index < colliders.Length; index++)
            {
                flippableColliders[index] = FlippableCollider.From(colliders[index]);
            }
        }

        private void ApplyRendererAndColliderFacing(bool shouldFlip)
        {
            visualRoot.localScale = visualRootInitialScale;
            var flipX = facingScaleAxis == FacingScaleAxis.X && shouldFlip;
            var flipY = facingScaleAxis == FacingScaleAxis.Y && shouldFlip;

            if (facingRenderers != null)
            {
                foreach (var spriteRenderer in facingRenderers)
                {
                    if (spriteRenderer == null)
                    {
                        continue;
                    }

                    spriteRenderer.flipX = flipX;
                    spriteRenderer.flipY = flipY;
                }
            }

            ApplyColliderFacing(shouldFlip);
        }

        private void ApplyColliderFacing(bool shouldFlip)
        {
            if (flippableColliders == null)
            {
                return;
            }

            foreach (var flippableCollider in flippableColliders)
            {
                flippableCollider.Apply(facingScaleAxis, shouldFlip);
            }
        }

        private void StartPatrolTask()
        {
            StopPatrolTask();
            patrolCancellation = new CancellationTokenSource();
            PatrolLoopAsync(patrolCancellation).Forget(Debug.LogException);
        }

        private void StopPatrolTask()
        {
            if (patrolCancellation == null)
            {
                return;
            }

            patrolCancellation.Cancel();
            patrolCancellation.Dispose();
            patrolCancellation = null;
        }

        private async UniTask PatrolLoopAsync(CancellationTokenSource cancellationSource)
        {
            var token = cancellationSource.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Yield(PatrolPlayerLoopTiming, token);
                    if (token.IsCancellationRequested || !IsActive || patrolDistance <= 0f || patrolSpeed <= 0f)
                    {
                        continue;
                    }

                    MovePatrolStep(Time.deltaTime);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
        }

        private void MovePatrolStep(float deltaTime)
        {
            var targetTransform = cachedTransform != null ? cachedTransform : transform;
            var nextPosition = targetTransform.localPosition + (patrolAxis * (direction * patrolSpeed * deltaTime));
            var offset = Vector2.Dot((Vector2)(nextPosition - basePosition), normalizedPatrolAxis);

            if (Mathf.Abs(offset) >= patrolDistance)
            {
                direction *= -1;
                nextPosition = basePosition + (patrolAxis * Mathf.Clamp(offset, -patrolDistance, patrolDistance));
                ApplyFacing();
            }

            targetTransform.localPosition = nextPosition;
        }

        private readonly struct FlippableCollider
        {
            private readonly Collider2D target;
            private readonly Vector2 initialOffset;
            private readonly Vector2[] polygonPoints;
            private readonly Vector2[][] polygonPaths;

            private FlippableCollider(Collider2D target, Vector2 initialOffset, Vector2[] polygonPoints, Vector2[][] polygonPaths)
            {
                this.target = target;
                this.initialOffset = initialOffset;
                this.polygonPoints = polygonPoints;
                this.polygonPaths = polygonPaths;
            }

            public static FlippableCollider From(Collider2D target)
            {
                if (target == null)
                {
                    return default;
                }

                var paths = default(Vector2[][]);
                if (target is PolygonCollider2D polygonCollider)
                {
                    paths = new Vector2[polygonCollider.pathCount][];
                    for (var pathIndex = 0; pathIndex < polygonCollider.pathCount; pathIndex++)
                    {
                        paths[pathIndex] = polygonCollider.GetPath(pathIndex);
                    }
                }

                return new FlippableCollider(
                    target,
                    target.offset,
                    target is EdgeCollider2D edgeCollider ? edgeCollider.points : null,
                    paths);
            }

            public void Apply(FacingScaleAxis axis, bool shouldFlip)
            {
                if (target == null)
                {
                    return;
                }

                target.offset = Flip(initialOffset, axis, shouldFlip);

                if (target is EdgeCollider2D edgeCollider && polygonPoints != null)
                {
                    edgeCollider.points = Flip(polygonPoints, axis, shouldFlip);
                    return;
                }

                if (target is PolygonCollider2D polygonCollider && polygonPaths != null)
                {
                    for (var pathIndex = 0; pathIndex < polygonPaths.Length; pathIndex++)
                    {
                        polygonCollider.SetPath(pathIndex, Flip(polygonPaths[pathIndex], axis, shouldFlip));
                    }
                }
            }

            private static Vector2[] Flip(Vector2[] points, FacingScaleAxis axis, bool shouldFlip)
            {
                var result = new Vector2[points.Length];
                for (var index = 0; index < points.Length; index++)
                {
                    result[index] = Flip(points[index], axis, shouldFlip);
                }

                return result;
            }

            private static Vector2 Flip(Vector2 value, FacingScaleAxis axis, bool shouldFlip)
            {
                if (!shouldFlip)
                {
                    return value;
                }

                if (axis == FacingScaleAxis.Y)
                {
                    value.y = -value.y;
                }
                else
                {
                    value.x = -value.x;
                }

                return value;
            }
        }
    }
}
