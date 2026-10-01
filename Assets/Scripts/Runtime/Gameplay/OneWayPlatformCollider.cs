using UnityEngine;
using Zenject;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Кастомная one-way логика платформы.
    /// Игнорирует столкновение, если нижняя точка игрока ниже верхней границы платформы.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class OneWayPlatformCollider : MonoBehaviour
    {
        [SerializeField] private JumpPlayerController player;
        [SerializeField] private float topBoundaryPadding;
        [SerializeField] private float landingVelocityThreshold = 0.05f;

        private Collider2D platformCollider;
        private Collider2D playerCollider;
        private float cachedTopY;
        private Vector3 lastPosition;
        private bool isIgnoringPlayerCollision;

        /// <summary>
        /// Y-координата верхней границы коллайдера платформы (кэшированная).
        /// </summary>
        public float TopY => cachedTopY;

        /// <summary>
        /// Вычисляет и сохраняет Y-координату верхней границы платформы.
        /// </summary>
        public void UpdateCachedTopY()
        {
            cachedTopY = platformCollider != null ? platformCollider.bounds.max.y : transform.position.y;
        }

        /// <summary>
        /// Получает игрока через DI, чтобы не искать его по сцене на каждой платформе.
        /// </summary>
        [Inject]
        public void Construct(JumpPlayerController injectedPlayer)
        {
            player = injectedPlayer;
            ResolvePlayerCollider();
        }

        /// <summary>
        /// Подготавливает Collider2D и отключает стандартный PlatformEffector2D, если он остался на prefab.
        /// </summary>
        private void Awake()
        {
            platformCollider = GetComponent<Collider2D>();
            platformCollider.usedByEffector = false;

            if (TryGetComponent(out PlatformEffector2D platformEffector))
            {
                platformEffector.enabled = false;
            }

            UpdateCachedTopY();
            lastPosition = transform.position;
            RefreshColliderState();
        }

        /// <summary>
        /// После повторного включения сразу синхронизирует IgnoreCollision, не ожидая следующего FixedUpdate.
        /// </summary>
        private void OnEnable()
        {
            UpdateCachedTopY();
            lastPosition = transform.position;
            RefreshColliderState();
        }

        /// <summary>
        /// При выключении платформы обязательно возвращает столкновение с игроком.
        /// </summary>
        private void OnDisable()
        {
            SetPlayerCollisionIgnored(false);
        }

        /// <summary>
        /// Синхронизирует состояние IgnoreCollision с текущей позицией GroundCheck игрока.
        /// </summary>
        private void FixedUpdate()
        {
            if (player == null && !ResolvePlayer())
            {
                return;
            }

            // Если платформа переместилась (например, была рециклирована), принудительно обновляем кэш
            if (transform.position != lastPosition)
            {
                UpdateCachedTopY();
                lastPosition = transform.position;
            }

            // Оптимизация: если игрок взлетает вверх, коллизия гарантированно игнорируется.
            // Это избавляет от выполнения сложных вычислений расстояний для всех платформ.
            var isFalling = player.VerticalVelocity <= landingVelocityThreshold ||
                player.PreviousVerticalVelocity <= landingVelocityThreshold;

            if (!isFalling)
            {
                SetPlayerCollisionIgnored(true);
                return;
            }

            RefreshColliderState();
        }

        /// <summary>
        /// Обновляет режим столкновения между платформой и игроком.
        /// </summary>
        private void RefreshColliderState()
        {
            if (playerCollider == null && !ResolvePlayerCollider())
            {
                return;
            }

            SetPlayerCollisionIgnored(!ShouldCollideWith(player));
        }

        /// <summary>
        /// Проверяет, должен ли игрок столкнуться с платформой в текущем физическом шаге.
        /// </summary>
        /// <param name="targetPlayer">Игрок, для которого проверяется столкновение.</param>
        /// <returns>True, если нижняя точка игрока не ниже верхней границы платформы.</returns>
        public bool ShouldCollideWith(JumpPlayerController targetPlayer)
        {
            if (targetPlayer == null)
            {
                return false;
            }

            var topY = cachedTopY + Mathf.Max(0f, topBoundaryPadding);
            var playerLowestY = playerCollider != null ? playerCollider.bounds.min.y : targetPlayer.GroundCheckY;

            return playerLowestY >= topY;
        }

        /// <summary>
        /// Возвращает true, если ссылка на игрока уже получена.
        /// </summary>
        private bool ResolvePlayer()
        {
            return player != null;
        }

        /// <summary>
        /// Кэширует коллайдер игрока для Physics2D.IgnoreCollision.
        /// </summary>
        private bool ResolvePlayerCollider()
        {
            if (player == null)
            {
                return false;
            }

            return player.TryGetComponent(out playerCollider);
        }

        /// <summary>
        /// Включает или выключает физическое столкновение пары игрок-платформа.
        /// </summary>
        private void SetPlayerCollisionIgnored(bool shouldIgnore)
        {
            if (platformCollider == null || playerCollider == null || isIgnoringPlayerCollision == shouldIgnore)
            {
                return;
            }

            Physics2D.IgnoreCollision(platformCollider, playerCollider, shouldIgnore);
            isIgnoringPlayerCollision = shouldIgnore;
        }

    }
}
