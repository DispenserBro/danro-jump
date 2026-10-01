using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Двигает платформу по синусоиде вокруг позиции спавна.
    /// </summary>
    public sealed class MovingPlatformBehaviour : MonoBehaviour, IPlatformLifecycle
    {
        [SerializeField] private Vector2 localMoveAxis = Vector2.right;
        [SerializeField] [Min(0f)] private float amplitude = 1.25f;
        [SerializeField] [Min(0.01f)] private float cycleDuration = 2.5f;
        [SerializeField] private bool useSpawnIndexOffset = true;

        private const float TwoPi = Mathf.PI * 2f;

        private Transform cachedTransform;
        private Rigidbody2D body;
        private Vector3 basePosition;
        private Vector2 normalizedMoveAxis = Vector2.right;
        private float angularSpeed;
        private float phaseOffset;

        private void Awake()
        {
            cachedTransform = transform;
            body = GetComponent<Rigidbody2D>();
            RefreshMoveAxis();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RefreshMoveAxis();
        }
#endif

        /// <summary>
        /// Обновляет позицию платформы вдоль локальной оси движения.
        /// </summary>
        private void Update()
        {
            if (body != null)
            {
                return;
            }

            MoveVisual(Time.time);
        }

        private void FixedUpdate()
        {
            if (body == null)
            {
                return;
            }

            body.MovePosition((Vector2)CalculatePosition(Time.fixedTime));
        }

        /// <summary>
        /// Двигает visual-only платформу через Transform.
        /// </summary>
        private void MoveVisual(float time)
        {
            if (amplitude <= 0f || cycleDuration <= 0f)
            {
                return;
            }

            cachedTransform.position = CalculatePosition(time);
        }

        /// <summary>
        /// Рассчитывает позицию платформы на указанном времени симуляции.
        /// </summary>
        private Vector3 CalculatePosition(float time)
        {
            if (amplitude <= 0f || cycleDuration <= 0f)
            {
                return basePosition;
            }

            var phase = time * angularSpeed + phaseOffset * TwoPi;
            return basePosition + (Vector3)(normalizedMoveAxis * (Mathf.Sin(phase) * amplitude));
        }

        /// <summary>
        /// Запоминает позицию спавна при первом создании.
        /// </summary>
        public void Initialize(PlatformSpawnContext context)
        {
            ApplyContext(context);
        }

        /// <summary>
        /// Запоминает новую позицию спавна при рецикле платформы.
        /// </summary>
        public void Recycle(PlatformSpawnContext context)
        {
            ApplyContext(context);
        }

        /// <summary>
        /// Возвращает платформу в базовую позицию.
        /// </summary>
        public void ResetPlatform()
        {
            if (body != null)
            {
                body.position = (Vector2)basePosition;
                return;
            }

            (cachedTransform != null ? cachedTransform : transform).position = basePosition;
        }

        /// <summary>
        /// Применяет контекст спавна и рассчитывает фазу движения.
        /// </summary>
        private void ApplyContext(PlatformSpawnContext context)
        {
            RefreshMoveAxis();
            basePosition = cachedTransform != null ? cachedTransform.position : transform.position;
            phaseOffset = useSpawnIndexOffset ? context.SpawnIndex * 0.173f : 0f;
        }

        private void RefreshMoveAxis()
        {
            normalizedMoveAxis = localMoveAxis.sqrMagnitude > 0f ? localMoveAxis.normalized : Vector2.right;
            angularSpeed = cycleDuration > 0f ? TwoPi / cycleDuration : 0f;
        }
    }
}
