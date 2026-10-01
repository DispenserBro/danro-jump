using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DanroJump.Audio;
using DanroJump.Vfx;
using UnityEngine;
using Zenject;
#if ENABLE_INPUT_SYSTEM
using DanroJump.Hardware.Com.Input;
using UnityEngine.InputSystem;
#endif

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Управляет игроком вертикального платформера: вводом, автопрыжками, wrap-переходом, смертью и анимациями.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class JumpPlayerController : MonoBehaviour, IPlayerController
    {
        [SerializeField] private float moveSpeed = 7.5f;
        [SerializeField] private float initialJumpVelocity = 15f;
        [SerializeField] private float autoJumpVelocity = 15f;
        [SerializeField] private float autoJumpCooldown = 0.08f;
        [SerializeField] [Range(0f, 1f)] private float groundContactNormalY = 0.45f;
        [SerializeField] private float groundContactTolerance = 0.12f;
        [SerializeField] private float groundProbeStartOffset = 0.08f;
        [SerializeField] private float groundProbeDistance = 0.34f;
        [SerializeField] private Vector2 equipmentProbeHalfExtents = new(0.5f, 0.35f);
        [SerializeField] private Vector2 sweptLandingProbeHalfExtents = new(0.48f, 0.08f);
        [SerializeField] private float sweptLandingExtraDistance = 0.18f;
        [SerializeField] private CollisionDetectionMode2D collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        [SerializeField] private float horizontalWrapPadding = 0.65f;
        [SerializeField] private float fallResetDistance = 0.5f;
        [SerializeField] private bool fallResetFromCameraBottom = true;
        [SerializeField] [Range(0f, 1f)] private float respawnViewportY = 0.28f;
        [SerializeField] private bool centerRespawnX = true;
        [SerializeField] private float deathRespawnDelay = 0.35f;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private string visualRootName = "Body";
        [SerializeField] private Transform groundCheck;
        [SerializeField] private string groundCheckName = "GroundCheck";
        [SerializeField] private float jumpAnimationPulseDuration = 0.12f;
        [SerializeField] private bool showWrappedVisualPreview = true;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private InputActionReference moveActionReference;
        [SerializeField] private InputActionAsset inputActionsAsset;
        [SerializeField] private string moveActionPath = "Player/Move";
#endif

        private Rigidbody2D body;
        private Collider2D playerCollider;
        private Camera gameplayCamera;
        private IGameAudioService audioService;
        private IVfxService vfxService;
        private Vector3 startPosition;
        private Vector3 visualRootInitialScale;
        private Quaternion visualRootInitialRotation;
        private float horizontalInput;
        private float moveSpeedMultiplier = 1f;
        private float heightLiftTargetY;
        private float heightLiftSpeed;
        private float jumpAnimationPulseUntil;
        private float respawnAtTime = -1f;
        private float lastAutoJumpTime = -1f;
        private CancellationTokenSource moveSpeedBoostCancellation;
        private CancellationTokenSource heightLiftCancellation;
        private bool respawnBlocked;
        private bool enemyPassThroughWhileRising;
        private bool hasSpeedParameter;
        private bool hasVerticalSpeedParameter;
        private bool hasGroundedParameter;
        private bool hasJumpParameter;
        private bool hasDeathParameter;
        private AnimatorControllerParameterType deathParameterType;
#if ENABLE_INPUT_SYSTEM
        private InputAction moveAction;
        private bool moveActionWasEnabled;
#endif
        private Transform wrapVisualCloneRoot;
        private readonly List<SpriteRenderer> sourceRenderers = new();
        private readonly List<SpriteRenderer> wrapRendererClones = new();
        private readonly List<IPlatformLandingHandler> landingHandlerBuffer = new();
        private readonly List<ILandingEquipment> landingEquipmentBuffer = new();
        private readonly List<IPickable> pickableBuffer = new();
        private readonly List<Collider2D> ignoredEnemyColliders = new();
        private readonly RaycastHit2D[] groundProbeHits = new RaycastHit2D[8];
        private readonly RaycastHit2D[] sweptGroundProbeHits = new RaycastHit2D[12];
        private readonly RaycastHit2D[] sweptEquipmentProbeHits = new RaycastHit2D[12];
        private readonly RaycastHit2D[] sweptPickableProbeHits = new RaycastHit2D[16];
        private readonly Collider2D[] equipmentProbeHits = new Collider2D[8];
        private readonly Collider2D[] pickableProbeHits = new Collider2D[16];
        private readonly ContactFilter2D solidProbeFilter = new()
        {
            useTriggers = false
        };
        private readonly ContactFilter2D triggerProbeFilter = new()
        {
            useTriggers = true
        };
        private Bounds cachedVisualBounds;
        private int cachedVisualBoundsFrame = -1;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("VSpeed");
        private static readonly int GroundedHash = Animator.StringToHash("Grounded");
        private static readonly int JumpHash = Animator.StringToHash("Jump");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private const int BaseAnimatorLayerIndex = 0;
        private const string JumpMidAirStateName = "Jump_Mid-Air";
        private const string JumpMidAirStatePath = "Base Layer." + JumpMidAirStateName;
        private static readonly int JumpMidAirStateHash = Animator.StringToHash(JumpMidAirStateName);
        private static readonly int JumpMidAirStatePathHash = Animator.StringToHash(JumpMidAirStatePath);

        /// <summary>
        /// Максимальная высота текущего забега.
        /// </summary>
        public float HighestY { get; private set; }

        /// <summary>
        /// Текущая вертикальная скорость Rigidbody2D.
        /// </summary>
        public float VerticalVelocity => body != null ? body.linearVelocity.y : 0f;

        /// <summary>
        /// Позиция GroundCheck на предыдущем физическом шаге.
        /// </summary>
        public Vector2 PreviousGroundCheckPosition { get; private set; }

        /// <summary>
        /// Y-координата GroundCheck на предыдущем физическом шаге.
        /// </summary>
        public float PreviousGroundCheckY { get; private set; }

        /// <summary>
        /// Вертикальная скорость игрока на предыдущем физическом шаге.
        /// </summary>
        public float PreviousVerticalVelocity { get; private set; }

        /// <summary>
        /// Текущая мировая позиция GroundCheck.
        /// </summary>
        public Vector2 GroundCheckPosition => groundCheck != null ? groundCheck.position : transform.position;

        /// <summary>
        /// Текущая Y-координата GroundCheck.
        /// </summary>
        public float GroundCheckY => groundCheck != null ? groundCheck.position.y : transform.position.y;

        /// <summary>
        /// Базовая скорость автоматического прыжка игрока.
        /// </summary>
        public float AutoJumpVelocity => autoJumpVelocity;

        /// <summary>
        /// Задержка перед respawn после смерти игрока.
        /// </summary>
        public float DeathRespawnDelay => deathRespawnDelay;

        /// <summary>
        /// True, если игрок сейчас должен проходить сквозь врагов после усиленного подъема.
        /// </summary>
        public bool IgnoresEnemyContacts => enemyPassThroughWhileRising && VerticalVelocity > 0f && !IsDead;

        /// <summary>
        /// True, пока активен принудительный подъем от джетпака или другого lift-оборудования.
        /// </summary>
        public bool IsHeightLiftActive => heightLiftCancellation != null;

        /// <summary>
        /// True, пока активный lift должен отключать игровые контакты с окружением.
        /// </summary>
        public bool IgnoresEnvironmentInteractions => IsHeightLiftActive && !IsDead;

        /// <summary>
        /// Текущая целевая высота активного принудительного подъема.
        /// </summary>
        public float HeightLiftTargetY => heightLiftTargetY;

        /// <summary>
        /// Показывает, находится ли игрок в состоянии смерти/respawn.
        /// </summary>
        public bool IsDead { get; private set; }

        public event System.Action<IPlayerController> Died;
        public event System.Action<IPlayerController> Respawned;
        public event System.Action LandedOnNormalPlatform;

        public Transform Transform => transform;

        /// <summary>
        /// Получает gameplay-камеру через SceneContext.
        /// </summary>
        [Inject]
        public void Construct(
            Camera injectedGameplayCamera,
            [InjectOptional] IGameAudioService injectedAudioService = null,
            [InjectOptional] IVfxService injectedVfxService = null)
        {
            gameplayCamera = injectedGameplayCamera;
            audioService = injectedAudioService;
            vfxService = injectedVfxService;
        }

        /// <summary>
        /// Кэширует физику, визуальные ссылки и параметры Animator.
        /// </summary>
        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            playerCollider = GetComponent<Collider2D>();
            startPosition = transform.position;
            HighestY = transform.position.y;
            body.collisionDetectionMode = collisionDetectionMode;

            ResolveVisualReferences();
            RebuildWrapVisuals();
            CacheAnimatorParameters();
            CachePhysicsStateForNextStep();
        }

        /// <summary>
        /// Запускает первый прыжок сразу после старта сцены.
        /// </summary>
        private void Start()
        {
            Bounce(initialJumpVelocity);
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            RegisterMoveAction();
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            UnregisterMoveAction();
#endif
        }

        /// <summary>
        /// Обрабатывает ввод, wrap, смерть от падения и обновление Animator.
        /// </summary>
        private void Update()
        {
            if (IsDead)
            {
                TryFinishDeathRespawn();
                UpdateAnimatorParameters();
                return;
            }

            horizontalInput = ReadHorizontalInput();
            HighestY = Mathf.Max(HighestY, transform.position.y);

            WrapAroundCameraBounds();
            ResetWhenTooLow();
            UpdateFacing();
            UpdateAnimatorParameters();
        }

        /// <summary>
        /// Двигает Rigidbody2D и проверяет автопрыжок в физическом шаге.
        /// </summary>
        private void FixedUpdate()
        {
            if (IsDead)
            {
                body.linearVelocity = Vector2.zero;
                EndEnemyPassThrough();
                CachePhysicsStateForNextStep();
                return;
            }

            body.linearVelocity = new Vector2(horizontalInput * moveSpeed * moveSpeedMultiplier, body.linearVelocity.y);
            if (!IgnoresEnvironmentInteractions)
            {
                TryAutoJumpFromGroundProbe();
                TryPickUpOverlappingPickables();
            }

            UpdateEnemyPassThroughState();
            CachePhysicsStateForNextStep();
        }

        /// <summary>
        /// Обновляет визуальный клон игрока на противоположной стороне экрана.
        /// </summary>
        private void LateUpdate()
        {
            UpdateWrappedVisualPreview();
        }

        /// <summary>
        /// Уничтожает служебный визуальный клон при удалении игрока.
        /// </summary>
        private void OnDestroy()
        {
            EndEnemyPassThrough();
            CancelMoveSpeedBoost();
            CancelHeightLift();
#if ENABLE_INPUT_SYSTEM
            UnregisterMoveAction();
#endif

            if (wrapVisualCloneRoot != null)
            {
                Destroy(wrapVisualCloneRoot.gameObject);
            }
        }

        /// <summary>
        /// Временно изменяет горизонтальную скорость игрока.
        /// </summary>
        public void ApplyMoveSpeedMultiplier(float multiplier, float duration)
        {
            if (multiplier <= 0f || duration <= 0f)
            {
                return;
            }

            CancelMoveSpeedBoost();
            moveSpeedBoostCancellation = new CancellationTokenSource();
            ApplyMoveSpeedMultiplierAsync(multiplier, duration, moveSpeedBoostCancellation).Forget(Debug.LogException);
        }

        /// <summary>
        /// Поднимает игрока вверх с заданной скоростью на заданную высоту.
        /// </summary>
        public void ApplyHeightLift(float liftSpeed, float height)
        {
            if (liftSpeed <= 0f || height <= 0f)
            {
                return;
            }

            heightLiftSpeed = liftSpeed;
            jumpAnimationPulseUntil = -1f;
            PlayJumpMidAirAnimationIfNeeded(liftSpeed);

            if (heightLiftCancellation == null)
            {
                heightLiftTargetY = transform.position.y + height;
                heightLiftCancellation = new CancellationTokenSource();
                ApplyHeightLiftAsync(heightLiftCancellation).Forget(Debug.LogException);
            }

            BeginEnemyPassThroughWhileRising();
        }

        /// <summary>
        /// Проверяет автопрыжок при первом контакте с платформой.
        /// </summary>
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (IsDead)
            {
                return;
            }

            if (TryIgnoreEnemyCollision(collision))
            {
                return;
            }

            if (IgnoresEnvironmentInteractions)
            {
                return;
            }

            TryAutoJump(collision);
        }

        /// <summary>
        /// Повторно проверяет автопрыжок, если игрок уже находится на платформе.
        /// </summary>
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (IsDead)
            {
                return;
            }

            if (TryIgnoreEnemyCollision(collision))
            {
                return;
            }

            if (IgnoresEnvironmentInteractions)
            {
                return;
            }

            TryAutoJump(collision);
        }

        /// <summary>
        /// Задает вертикальный импульс прыжка, не уменьшая уже существующую скорость вверх.
        /// </summary>
        public void Bounce(float velocity)
        {
            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (body != null)
            {
                body.linearVelocity = new Vector2(body.linearVelocity.x, Mathf.Max(body.linearVelocity.y, velocity));
            }

            jumpAnimationPulseUntil = Time.time + jumpAnimationPulseDuration;
            audioService?.Play(GameAudioEvent.PlayerJump, transform.position);
            vfxService?.SpawnJumpEffect(transform.position);
        }

        /// <summary>
        /// Задает усиленный вертикальный импульс и временно отключает столкновения с врагами на подъеме.
        /// </summary>
        public void BounceWithEnemyPassThrough(float velocity)
        {
            Bounce(velocity);
            BeginEnemyPassThroughWhileRising();
        }

        /// <summary>
        /// Принудительно включает mid-air прыжок, если Animator еще не находится в этом состоянии.
        /// </summary>
        public void PlayJumpMidAirAnimationIfNeeded()
        {
            var verticalSpeed = body != null ? Mathf.Max(body.linearVelocity.y, heightLiftSpeed) : heightLiftSpeed;
            PlayJumpMidAirAnimationIfNeeded(verticalSpeed);
        }

        private void PlayJumpMidAirAnimationIfNeeded(float verticalSpeed)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            {
                return;
            }

            var currentState = animator.GetCurrentAnimatorStateInfo(BaseAnimatorLayerIndex);
            if (!animator.IsInTransition(BaseAnimatorLayerIndex) && IsJumpMidAirState(currentState))
            {
                return;
            }

            ApplyForcedAirborneAnimatorParameters(verticalSpeed);
            animator.Play(JumpMidAirStatePathHash, BaseAnimatorLayerIndex, 0f);
            animator.Update(0f);
        }

        private static bool IsJumpMidAirState(AnimatorStateInfo state)
        {
            return state.shortNameHash == JumpMidAirStateHash || state.fullPathHash == JumpMidAirStatePathHash;
        }

        private void ApplyForcedAirborneAnimatorParameters(float verticalSpeed)
        {
            if (hasVerticalSpeedParameter)
            {
                animator.SetFloat(VerticalSpeedHash, Mathf.Max(verticalSpeed, 0.01f));
            }

            if (hasGroundedParameter)
            {
                animator.SetBool(GroundedHash, false);
            }

            if (hasJumpParameter)
            {
                animator.SetBool(JumpHash, false);
            }
        }

        /// <summary>
        /// Обновляет кэш визуальных renderer-ов после runtime-крепления или снятия экипировки.
        /// </summary>
        public void RefreshVisualRenderers()
        {
            cachedVisualBoundsFrame = -1;
            RebuildWrapVisuals();
        }

        /// <summary>
        /// Отключает столкновение с указанным enemy-коллайдером, пока игрок поднимается после усиления.
        /// </summary>
        public bool TryIgnoreEnemyCollider(Collider2D enemyCollider)
        {
            if (!IgnoresEnemyContacts || enemyCollider == null || playerCollider == null)
            {
                return false;
            }

            var enemyRoot = enemyCollider.GetComponentInParent<EnemyBase>();
            if (enemyRoot != null)
            {
                return TryIgnoreEnemy(enemyRoot);
            }

            IgnoreEnemyCollider(enemyCollider);
            return true;
        }

        /// <summary>
        /// Отключает столкновения со всеми коллайдерами врага, пока игрок поднимается после усиления.
        /// </summary>
        public bool TryIgnoreEnemy(EnemyBase enemy)
        {
            if (!IgnoresEnemyContacts || enemy == null || playerCollider == null)
            {
                return false;
            }

            var enemyColliders = enemy.GetComponentsInChildren<Collider2D>(true);
            for (var index = 0; index < enemyColliders.Length; index++)
            {
                IgnoreEnemyCollider(enemyColliders[index]);
            }

            return true;
        }

        private void IgnoreEnemyCollider(Collider2D enemyCollider)
        {
            if (enemyCollider == null || enemyCollider == playerCollider)
            {
                return;
            }

            Physics2D.IgnoreCollision(playerCollider, enemyCollider, true);
            if (!ignoredEnemyColliders.Contains(enemyCollider))
            {
                ignoredEnemyColliders.Add(enemyCollider);
            }
        }

        /// <summary>
        /// Отключает столкновение с врагом, если игрок поднимается после усиленного прыжка.
        /// </summary>
        private bool TryIgnoreEnemyCollision(Collision2D collision)
        {
            if (!IgnoresEnemyContacts || collision.collider == null)
            {
                return false;
            }

            var enemy = collision.collider.GetComponentInParent<EnemyBase>();
            return enemy != null && TryIgnoreEnemy(enemy);
        }

        /// <summary>
        /// Убивает игрока внешним игровым объектом или системой.
        /// </summary>
        public void Kill()
        {
            Die();
        }

        /// <summary>
        /// Полностью начинает новый забег со стартовой позиции.
        /// </summary>
        public void ResetRun()
        {
            ResetPlayerState(startPosition, true);
        }

        /// <summary>
        /// Блокирует отложенный respawn после окончательного GameOver.
        /// </summary>
        public void BlockPendingRespawn()
        {
            respawnBlocked = true;
            respawnAtTime = -1f;
        }

        /// <summary>
        /// Разрешает ручной respawn после подтвержденного продолжения игры.
        /// </summary>
        public void AllowRespawn()
        {
            respawnBlocked = false;
        }

        /// <summary>
        /// Разрешает отложенный respawn после того, как сессия подтвердила наличие жизней.
        /// </summary>
        public void ScheduleRespawn(float delay)
        {
            if (!IsDead || respawnBlocked)
            {
                return;
            }

            respawnAtTime = Time.time + Mathf.Max(0f, delay);
        }

        /// <summary>
        /// Возвращает игрока в поле зрения камеры без сброса прогресса текущей сессии.
        /// </summary>
        public void Respawn()
        {
            if (respawnBlocked)
            {
                return;
            }

            ResetPlayerState(GetRespawnPosition(), false);
        }

        /// <summary>
        /// Возвращает игрока к заданной позиции и сбрасывает временные эффекты.
        /// </summary>
        private void ResetPlayerState(Vector3 position, bool resetRunProgress)
        {
            IsDead = false;
            respawnBlocked = false;
            respawnAtTime = -1f;
            ClearDeathAnimationFlag();
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            HighestY = resetRunProgress ? transform.position.y : Mathf.Max(HighestY, transform.position.y);
            lastAutoJumpTime = -1f;
            ResetMoveSpeedMultiplier();
            ResetHeightLift();
            EndEnemyPassThrough();
            CachePhysicsStateForNextStep();
            Bounce(initialJumpVelocity);
            Respawned?.Invoke(this);
        }

        /// <summary>
        /// Animation Event для клипа прыжка.
        /// </summary>
        public void OnJump()
        {
        }

        /// <summary>
        /// Animation Event для шага или вторичных звуков движения.
        /// </summary>
        public void OnStep()
        {
        }

        /// <summary>
        /// Animation Event начала смерти.
        /// </summary>
        public void OnDeathBegin()
        {
        }

        /// <summary>
        /// Animation Event завершения смерти. Если игрок еще мертв, запускает respawn.
        /// </summary>
        public void OnDeathEnd()
        {
            if (IsDead && !respawnBlocked && respawnAtTime >= 0f && Time.time >= respawnAtTime)
            {
                Respawn();
            }
        }

        /// <summary>
        /// Совместимый Animation Event для старых клипов, которые вызывают Jump.
        /// </summary>
        public void Jump()
        {
            OnJump();
        }

        /// <summary>
        /// Совместимый Animation Event для старых клипов, которые вызывают Step.
        /// </summary>
        public void Step()
        {
            OnStep();
        }

        private void TryAutoJump(Collision2D collision)
        {
            if (!CanAutoJumpNow())
            {
                return;
            }

            if (!HasGroundContact(collision))
            {
                return;
            }

            if (!IsValidLandingCollider(collision.collider))
            {
                return;
            }

            var suppressAutoJump = TryHandlePlatformLanding(collision);

            // Оборудование платформы имеет приоритет над обычным автопрыжком.
            if (!TryActivateLandingEquipmentAtGround(Vector2.zero) && !TryActivateLandingEquipmentFromSweptProbe(Vector2.zero))
            {
                if (!suppressAutoJump)
                {
                    Bounce(autoJumpVelocity);
                }
            }

            lastAutoJumpTime = Time.time;
        }

        /// <summary>
        /// Проверяет, что collision действительно похож на приземление ногами, а не на удар боком или головой.
        /// </summary>
        private bool HasGroundContact(Collision2D collision)
        {
            for (var index = 0; index < collision.contactCount; index++)
            {
                var contact = collision.GetContact(index);
                if (Mathf.Abs(contact.normal.y) < groundContactNormalY)
                {
                    continue;
                }

                // GroundCheck защищает от прыжка при контакте головой или боком.
                if (contact.point.y <= GroundCheckY + groundContactTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Передает landing-событие платформенным обработчикам, если они есть на объекте столкновения.
        /// </summary>
        private bool TryHandlePlatformLanding(Collision2D collision)
        {
            var contactPoint = GroundCheckPosition;
            var contactNormal = Vector2.up;
            if (collision.contactCount > 0)
            {
                var contact = collision.GetContact(0);
                contactPoint = contact.point;
                contactNormal = contact.normal;
            }

            return TryHandlePlatformLanding(
                new PlatformLandingContext(collision.collider, collision, contactPoint, contactNormal, false));
        }

        /// <summary>
        /// Передает landing-событие платформенным обработчикам, найденным по collider.
        /// </summary>
        private bool TryHandlePlatformLanding(PlatformLandingContext context)
        {
            var platformCollider = context.PlatformCollider;
            if (platformCollider == null)
            {
                return false;
            }

            var handled = false;
            platformCollider.GetComponentsInParent(false, landingHandlerBuffer);
            foreach (var landingHandler in landingHandlerBuffer)
            {
                handled |= landingHandler.TryHandleLanding(this, context);
            }

            landingHandlerBuffer.Clear();
            return handled;
        }

        /// <summary>
        /// Применяет временный множитель скорости, затем возвращает нормальное значение.
        /// </summary>
        private async UniTask ApplyMoveSpeedMultiplierAsync(
            float multiplier,
            float duration,
            CancellationTokenSource cancellationSource)
        {
            try
            {
                moveSpeedMultiplier = multiplier;
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(duration),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    cancellationSource.Token);
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                if (moveSpeedBoostCancellation == cancellationSource)
                {
                    moveSpeedBoostCancellation = null;
                    moveSpeedMultiplier = 1f;
                    cancellationSource.Dispose();
                }
            }
        }

        /// <summary>
        /// Управляет подъемом игрока от lift-collectible без телепортации.
        /// </summary>
        private async UniTask ApplyHeightLiftAsync(CancellationTokenSource cancellationSource)
        {
            try
            {
                var token = cancellationSource.Token;
                lastAutoJumpTime = Time.time;

                while (!IsDead && transform.position.y < heightLiftTargetY && !token.IsCancellationRequested)
                {
                    body.linearVelocity = new Vector2(body.linearVelocity.x, heightLiftSpeed);
                    HighestY = Mathf.Max(HighestY, transform.position.y);
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, token);
                }

                if (!IsDead && !token.IsCancellationRequested)
                {
                    body.linearVelocity = new Vector2(body.linearVelocity.x, Mathf.Max(body.linearVelocity.y, 0f));
                }
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                if (heightLiftCancellation == cancellationSource)
                {
                    heightLiftCancellation = null;
                    heightLiftSpeed = 0f;
                    cancellationSource.Dispose();
                }
            }
        }

        /// <summary>
        /// Сбрасывает временный множитель горизонтальной скорости.
        /// </summary>
        private void ResetMoveSpeedMultiplier()
        {
            CancelMoveSpeedBoost();
            moveSpeedMultiplier = 1f;
        }

        /// <summary>
        /// Останавливает активный принудительный подъем игрока.
        /// </summary>
        private void ResetHeightLift()
        {
            CancelHeightLift();
            heightLiftSpeed = 0f;
        }

        private void CancelMoveSpeedBoost()
        {
            if (moveSpeedBoostCancellation == null)
            {
                return;
            }

            var cancellation = moveSpeedBoostCancellation;
            moveSpeedBoostCancellation = null;
            cancellation.Cancel();
            cancellation.Dispose();
        }

        private void CancelHeightLift()
        {
            if (heightLiftCancellation == null)
            {
                return;
            }

            var cancellation = heightLiftCancellation;
            heightLiftCancellation = null;
            cancellation.Cancel();
            cancellation.Dispose();
        }

        /// <summary>
        /// Проверяет приземление через GroundCheck-пробы, включая ситуацию на другой стороне экрана при wrap.
        /// </summary>
        private void TryAutoJumpFromGroundProbe()
        {
            if (!CanAutoJumpNow())
            {
                return;
            }

            if (TryResolveAutoJumpAtOffset(Vector2.zero, false))
            {
                return;
            }

            if (TryGetWrapProbeOffset(out var wrapOffset))
            {
                // Если игрок физически еще не перенесен, проверяем платформу на другой стороне экрана.
                TryResolveAutoJumpAtOffset(wrapOffset, true);
            }
        }

        /// <summary>
        /// Проверяет cooldown и направление движения перед автопрыжком.
        /// </summary>
        private bool CanAutoJumpNow()
        {
            return !IsDead &&
                !IgnoresEnvironmentInteractions &&
                body.linearVelocity.y <= 0.05f &&
                Time.time - lastAutoJumpTime >= autoJumpCooldown;
        }

        /// <summary>
        /// Проверяет посадку обычным и swept-пробником, затем активирует JumpPad или автопрыжок.
        /// </summary>
        private bool TryResolveAutoJumpAtOffset(Vector2 probeOffset, bool commitWrapOnHit)
        {
            if (!CanAutoJumpNow())
            {
                return false;
            }

            if (!TryGetGroundProbeHit(probeOffset, out var groundCollider) &&
                !TryGetSweptGroundProbeHit(probeOffset, out groundCollider))
            {
                return false;
            }

            if (commitWrapOnHit)
            {
                // Физическое тело переносится только когда найден реальный контакт после wrap.
                CommitWrapOffset(probeOffset);
                probeOffset = Vector2.zero;
            }

            var suppressAutoJump = TryHandlePlatformLanding(
                new PlatformLandingContext(groundCollider, null, GroundCheckPosition + probeOffset, Vector2.up, true));
            if (!TryActivateLandingEquipmentAtGround(probeOffset) && !TryActivateLandingEquipmentFromSweptProbe(probeOffset))
            {
                if (!suppressAutoJump)
                {
                    Bounce(autoJumpVelocity);
                    LandedOnNormalPlatform?.Invoke();
                }
            }

            lastAutoJumpTime = Time.time;
            return true;
        }

        /// <summary>
        /// Выполняет короткий raycast вниз от GroundCheck и ищет валидную платформу под игроком.
        /// </summary>
        private bool TryGetGroundProbeHit(Vector2 probeOffset, out Collider2D hitCollider)
        {
            hitCollider = null;
            var origin = GroundCheckPosition + probeOffset + Vector2.up * groundProbeStartOffset;
            var hitCount = Physics2D.Raycast(origin, Vector2.down, solidProbeFilter, groundProbeHits, groundProbeStartOffset + groundProbeDistance);

            for (var index = 0; index < hitCount; index++)
            {
                if (IsValidGroundProbeHit(groundProbeHits[index], false))
                {
                    hitCollider = groundProbeHits[index].collider;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Выполняет swept-проверку между предыдущей и текущей позицией GroundCheck, чтобы не пролетать платформы.
        /// </summary>
        private bool TryGetSweptGroundProbeHit(Vector2 probeOffset, out Collider2D hitCollider)
        {
            hitCollider = null;
            if (!TryGetSweptLandingProbe(probeOffset, out var origin, out var direction, out var distance))
            {
                return false;
            }

            var hitCount = Physics2D.BoxCast(
                origin,
                sweptLandingProbeHalfExtents * 2f,
                0f,
                direction,
                solidProbeFilter,
                sweptGroundProbeHits,
                distance + sweptLandingExtraDistance);

            for (var index = 0; index < hitCount; index++)
            {
                if (IsValidGroundProbeHit(sweptGroundProbeHits[index], true))
                {
                    hitCollider = sweptGroundProbeHits[index].collider;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Отбрасывает неподходящие probe-попадания: самого игрока, trigger, верхние препятствия и закрытые one-way платформы.
        /// </summary>
        private bool IsValidGroundProbeHit(RaycastHit2D hit, bool isSweptProbe)
        {
            var hitCollider = hit.collider;
            if (hitCollider == null || hitCollider == playerCollider || hitCollider.isTrigger)
            {
                return false;
            }

            var allowedTopY = isSweptProbe
                ? Mathf.Max(PreviousGroundCheckY, GroundCheckY) + groundContactTolerance
                : GroundCheckY + groundContactTolerance;
            if (hit.point.y > allowedTopY)
            {
                return false;
            }

            if (hitCollider.TryGetComponent(out OneWayPlatformCollider oneWayPlatform))
            {
                return oneWayPlatform.ShouldCollideWith(this);
            }

            return IsValidLandingCollider(hitCollider);
        }

        /// <summary>
        /// Разрешает автопрыжок только от платформенных коллайдеров, а не от произвольных solid-объектов.
        /// </summary>
        private static bool IsValidLandingCollider(Collider2D hitCollider)
        {
            if (hitCollider == null)
            {
                return false;
            }

            return hitCollider.TryGetComponent<JumpPlatform>(out _) || hitCollider.TryGetComponent<PlatformController>(out _);
        }

        /// <summary>
        /// Запоминает состояние GroundCheck и вертикальной скорости для следующего физического шага.
        /// </summary>
        private void CachePhysicsStateForNextStep()
        {
            PreviousGroundCheckPosition = GroundCheckPosition;
            PreviousGroundCheckY = GroundCheckY;
            PreviousVerticalVelocity = VerticalVelocity;
        }

        /// <summary>
        /// Ищет landing-оборудование рядом с GroundCheck и активирует его вместо обычного прыжка.
        /// </summary>
        private bool TryActivateLandingEquipmentAtGround(Vector2 probeOffset)
        {
            if (IgnoresEnvironmentInteractions)
            {
                return false;
            }

            var origin = GroundCheckPosition + probeOffset;
            var hitCount = Physics2D.OverlapBox(
                origin,
                equipmentProbeHalfExtents * 2f,
                0f,
                triggerProbeFilter,
                equipmentProbeHits);

            for (var index = 0; index < hitCount; index++)
            {
                var hitCollider = equipmentProbeHits[index];
                if (hitCollider == null || hitCollider == playerCollider || !hitCollider.isTrigger)
                {
                    continue;
                }

                if (TryActivateLandingEquipment(hitCollider))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Ищет landing-оборудование в swept-зоне между предыдущей и текущей позицией GroundCheck.
        /// </summary>
        private bool TryActivateLandingEquipmentFromSweptProbe(Vector2 probeOffset)
        {
            if (IgnoresEnvironmentInteractions)
            {
                return false;
            }

            if (!TryGetSweptLandingProbe(probeOffset, out var origin, out var direction, out var distance))
            {
                return false;
            }

            var hitCount = Physics2D.BoxCast(
                origin,
                equipmentProbeHalfExtents * 2f,
                0f,
                direction,
                triggerProbeFilter,
                sweptEquipmentProbeHits,
                distance + sweptLandingExtraDistance);

            for (var index = 0; index < hitCount; index++)
            {
                var hitCollider = sweptEquipmentProbeHits[index].collider;
                if (hitCollider == null || hitCollider == playerCollider || !hitCollider.isTrigger)
                {
                    continue;
                }

                if (TryActivateLandingEquipment(hitCollider))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryActivateLandingEquipment(Collider2D hitCollider)
        {
            if (IgnoresEnvironmentInteractions)
            {
                return false;
            }

            hitCollider.GetComponentsInParent(false, landingEquipmentBuffer);
            foreach (var landingEquipment in landingEquipmentBuffer)
            {
                if (landingEquipment.TryActivateFromLanding(this))
                {
                    landingEquipmentBuffer.Clear();
                    return true;
                }
            }

            landingEquipmentBuffer.Clear();
            return false;
        }

        /// <summary>
        /// Подбирает IPickable-объекты, которые пересекаются с телом игрока, независимо от landing-пробы.
        /// </summary>
        private void TryPickUpOverlappingPickables()
        {
            if (IgnoresEnvironmentInteractions || playerCollider == null)
            {
                return;
            }

            var bounds = playerCollider.bounds;
            var hitCount = Physics2D.OverlapBox(
                bounds.center,
                bounds.size,
                0f,
                triggerProbeFilter,
                pickableProbeHits);

            for (var index = 0; index < hitCount; index++)
            {
                var hitCollider = pickableProbeHits[index];
                if (hitCollider == null || hitCollider == playerCollider || !hitCollider.isTrigger)
                {
                    continue;
                }

                TryPickUpFromCollider(hitCollider);
            }

            TryPickUpSweptPickables(bounds);
        }

        private void TryPickUpSweptPickables(Bounds currentBounds)
        {
            if (IgnoresEnvironmentInteractions)
            {
                return;
            }

            var delta = GroundCheckPosition - PreviousGroundCheckPosition;
            var distance = delta.magnitude;
            if (distance <= 0.001f)
            {
                return;
            }

            var direction = delta / distance;
            var origin = (Vector2)currentBounds.center - delta;
            var hitCount = Physics2D.BoxCast(
                origin,
                (Vector2)currentBounds.size,
                0f,
                direction,
                triggerProbeFilter,
                sweptPickableProbeHits,
                distance);

            for (var index = 0; index < hitCount; index++)
            {
                var hitCollider = sweptPickableProbeHits[index].collider;
                if (hitCollider == null || hitCollider == playerCollider || !hitCollider.isTrigger)
                {
                    continue;
                }

                TryPickUpFromCollider(hitCollider);
            }
        }

        private bool TryPickUpFromCollider(Collider2D hitCollider)
        {
            if (IgnoresEnvironmentInteractions)
            {
                return false;
            }

            hitCollider.GetComponentsInParent(false, pickableBuffer);
            foreach (var pickable in pickableBuffer)
            {
                if (pickable.TryPickUp(this))
                {
                    pickableBuffer.Clear();
                    return true;
                }
            }

            pickableBuffer.Clear();
            return false;
        }

        /// <summary>
        /// Вычисляет направление и дистанцию swept-пробы посадки между физическими шагами.
        /// </summary>
        private bool TryGetSweptLandingProbe(Vector2 probeOffset, out Vector2 origin, out Vector2 direction, out float distance)
        {
            origin = PreviousGroundCheckPosition + probeOffset;
            var currentPosition = GroundCheckPosition + probeOffset;
            var delta = currentPosition - origin;
            distance = delta.magnitude;
            direction = distance > 0f ? delta / distance : Vector2.down;

            return PreviousVerticalVelocity <= 0.05f &&
                currentPosition.y < PreviousGroundCheckPosition.y &&
                distance > 0.001f;
        }

        /// <summary>
        /// Переносит игрока на противоположную сторону камеры, когда весь визуал ушел за край.
        /// </summary>
        private void WrapAroundCameraBounds()
        {
            if (gameplayCamera == null)
            {
                return;
            }

            var cameraHalfWidth = gameplayCamera.orthographicSize * gameplayCamera.aspect;
            var leftEdge = gameplayCamera.transform.position.x - cameraHalfWidth;
            var rightEdge = gameplayCamera.transform.position.x + cameraHalfWidth;
            var screenWidth = rightEdge - leftEdge;
            var visualBounds = GetPlayerVisualBounds();
            var wrapOffset = Vector2.zero;

            if (visualBounds.max.x < leftEdge - horizontalWrapPadding)
            {
                wrapOffset.x = screenWidth;
            }
            else if (visualBounds.min.x > rightEdge + horizontalWrapPadding)
            {
                wrapOffset.x = -screenWidth;
            }

            CommitWrapOffset(wrapOffset);
        }

        /// <summary>
        /// Возвращает временный offset для проверки посадки на противоположной стороне экрана.
        /// </summary>
        private bool TryGetWrapProbeOffset(out Vector2 wrapOffset)
        {
            wrapOffset = Vector2.zero;
            if (gameplayCamera == null)
            {
                return false;
            }

            var cameraHalfWidth = gameplayCamera.orthographicSize * gameplayCamera.aspect;
            var leftEdge = gameplayCamera.transform.position.x - cameraHalfWidth;
            var rightEdge = gameplayCamera.transform.position.x + cameraHalfWidth;
            var screenWidth = rightEdge - leftEdge;
            var visualBounds = GetPlayerVisualBounds();

            if (visualBounds.min.x < leftEdge)
            {
                wrapOffset.x = screenWidth;
            }
            else if (visualBounds.max.x > rightEdge)
            {
                wrapOffset.x = -screenWidth;
            }

            return !Mathf.Approximately(wrapOffset.x, 0f);
        }

        /// <summary>
        /// Физически применяет wrap-offset к Rigidbody2D и синхронизирует Transform.
        /// </summary>
        private void CommitWrapOffset(Vector2 wrapOffset)
        {
            if (wrapOffset.sqrMagnitude <= 0f)
            {
                return;
            }

            var position = transform.position + (Vector3)wrapOffset;
            if (body != null)
            {
                body.position = new Vector2(position.x, position.y);
                transform.position = new Vector3(body.position.x, body.position.y, position.z);
            }
            else
            {
                transform.position = position;
            }

            Physics2D.SyncTransforms();
            PreviousGroundCheckPosition += wrapOffset;
        }

        /// <summary>
        /// Запускает смерть, если игрок упал ниже допустимой линии камеры.
        /// </summary>
        private void ResetWhenTooLow()
        {
            if (gameplayCamera == null)
            {
                return;
            }

            var resetLineY = GetFallResetLineY();
            if (GroundCheckY < resetLineY)
            {
                Die();
            }
        }

        /// <summary>
        /// Переводит игрока в состояние смерти и запускает delayed respawn.
        /// </summary>
        private void Die()
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;
            respawnBlocked = false;
            respawnAtTime = -1f;

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            ResetMoveSpeedMultiplier();
            ResetHeightLift();
            EndEnemyPassThrough();
            PlayDeathAnimation();
            vfxService?.SpawnPlayerDeathEffect(transform.position);
            Died?.Invoke(this);
        }

        /// <summary>
        /// Запускает режим прохождения сквозь врагов до начала падения.
        /// </summary>
        private void BeginEnemyPassThroughWhileRising()
        {
            enemyPassThroughWhileRising = true;
        }

        /// <summary>
        /// Выключает режим прохождения сквозь врагов, когда усиленный подъем закончился.
        /// </summary>
        private void UpdateEnemyPassThroughState()
        {
            if (enemyPassThroughWhileRising && (IsDead || VerticalVelocity <= 0f))
            {
                EndEnemyPassThrough();
            }
        }

        /// <summary>
        /// Возвращает физические столкновения игрока с ранее проигнорированными врагами.
        /// </summary>
        private void EndEnemyPassThrough()
        {
            enemyPassThroughWhileRising = false;

            if (playerCollider != null)
            {
                for (var index = ignoredEnemyColliders.Count - 1; index >= 0; index--)
                {
                    var enemyCollider = ignoredEnemyColliders[index];
                    if (enemyCollider != null)
                    {
                        Physics2D.IgnoreCollision(playerCollider, enemyCollider, false);
                    }
                }
            }

            ignoredEnemyColliders.Clear();
        }

        /// <summary>
        /// Завершает задержку смерти и перезапускает забег.
        /// </summary>
        private void TryFinishDeathRespawn()
        {
            if (!respawnBlocked && respawnAtTime >= 0f && Time.time >= respawnAtTime)
            {
                Respawn();
            }
        }

        /// <summary>
        /// Устанавливает параметр смерти в Animator, если он есть в текущем controller.
        /// </summary>
        private void PlayDeathAnimation()
        {
            if (!hasDeathParameter || animator == null)
            {
                return;
            }

            if (deathParameterType == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(DeathHash);
            }
            else if (deathParameterType == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(DeathHash, true);
            }
        }

        /// <summary>
        /// Сбрасывает bool-параметр смерти после respawn.
        /// </summary>
        private void ClearDeathAnimationFlag()
        {
            if (hasDeathParameter && animator != null && deathParameterType == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(DeathHash, false);
            }
        }

        /// <summary>
        /// Возвращает Y-линию, ниже которой игрок считается упавшим.
        /// </summary>
        private float GetFallResetLineY()
        {
            if (fallResetFromCameraBottom)
            {
                return gameplayCamera.ViewportToWorldPoint(new Vector3(0.5f, 0f, 0f)).y - fallResetDistance;
            }

            return gameplayCamera.transform.position.y - fallResetDistance;
        }

        /// <summary>
        /// Вычисляет позицию respawn внутри текущего поля зрения камеры.
        /// </summary>
        private Vector3 GetRespawnPosition()
        {
            if (gameplayCamera == null)
            {
                return startPosition;
            }

            var cameraHalfHeight = gameplayCamera.orthographicSize;
            var cameraHalfWidth = cameraHalfHeight * gameplayCamera.aspect;
            var cameraPosition = gameplayCamera.transform.position;
            var position = transform.position;

            position.x = centerRespawnX ? cameraPosition.x : Mathf.Clamp(position.x, cameraPosition.x - cameraHalfWidth, cameraPosition.x + cameraHalfWidth);
            position.y = cameraPosition.y - cameraHalfHeight + cameraHalfHeight * 2f * respawnViewportY;

            return position;
        }

        /// <summary>
        /// Поворачивает визуальную часть игрока без изменения физического объекта.
        /// </summary>
        private void UpdateFacing()
        {
            if (Mathf.Abs(horizontalInput) < 0.01f)
            {
                return;
            }

            if (visualRoot == null)
            {
                return;
            }

            var scale = visualRootInitialScale;
            scale.y = Mathf.Abs(scale.y) * (Mathf.Sign(horizontalInput) < 0f ? -1f : 1f);
            visualRoot.localRotation = visualRootInitialRotation;
            visualRoot.localScale = scale;
        }

        /// <summary>
        /// Находит Animator, visualRoot и GroundCheck, если они не назначены в инспекторе.
        /// </summary>
        private void ResolveVisualReferences()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (visualRoot == null)
            {
                visualRoot = transform.Find(visualRootName);
            }

            if (visualRoot == null)
            {
                visualRoot = GetComponentInChildren<SpriteRenderer>()?.transform;
            }

            if (groundCheck == null)
            {
                groundCheck = transform.Find(groundCheckName);
            }

            if (visualRoot != null)
            {
                visualRootInitialScale = visualRoot.localScale;
                visualRootInitialRotation = visualRoot.localRotation;
            }
        }

        /// <summary>
        /// Создает набор SpriteRenderer-клонов для визуального предпросмотра wrap-перехода.
        /// </summary>
        private void RebuildWrapVisuals()
        {
            for (var index = wrapRendererClones.Count - 1; index >= 0; index--)
            {
                var clone = wrapRendererClones[index];
                if (clone != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(clone.gameObject);
                    }
                    else
                    {
                        DestroyImmediate(clone.gameObject);
                    }
                }
            }

            sourceRenderers.Clear();
            wrapRendererClones.Clear();

            if (visualRoot != null)
            {
                sourceRenderers.AddRange(visualRoot.GetComponentsInChildren<SpriteRenderer>(true));
            }
            else
            {
                sourceRenderers.AddRange(GetComponentsInChildren<SpriteRenderer>(true));
            }

            if (sourceRenderers.Count == 0)
            {
                return;
            }

            if (wrapVisualCloneRoot == null)
            {
                wrapVisualCloneRoot = new GameObject($"{name} Wrap Visuals").transform;
                wrapVisualCloneRoot.gameObject.SetActive(false);
            }

            for (var index = sourceRenderers.Count - 1; index >= 0; index--)
            {
                var sourceRenderer = sourceRenderers[index];
                if (sourceRenderer == null)
                {
                    sourceRenderers.RemoveAt(index);
                    continue;
                }

                var clone = new GameObject($"{sourceRenderer.name} Wrap Clone").AddComponent<SpriteRenderer>();
                clone.transform.SetParent(wrapVisualCloneRoot, false);
                wrapRendererClones.Insert(0, clone);
            }
        }

        /// <summary>
        /// Показывает копию визуала игрока на противоположном краю экрана перед фактическим wrap.
        /// </summary>
        private void UpdateWrappedVisualPreview()
        {
            if (!showWrappedVisualPreview || gameplayCamera == null || sourceRenderers.Count == 0)
            {
                SetWrappedVisualsActive(false);
                return;
            }

            var cameraHalfWidth = gameplayCamera.orthographicSize * gameplayCamera.aspect;
            var leftEdge = gameplayCamera.transform.position.x - cameraHalfWidth;
            var rightEdge = gameplayCamera.transform.position.x + cameraHalfWidth;
            var screenWidth = rightEdge - leftEdge;
            var visualBounds = GetPlayerVisualBounds();
            var wrapOffsetX = 0f;

            if (visualBounds.min.x < leftEdge)
            {
                wrapOffsetX = screenWidth;
            }
            else if (visualBounds.max.x > rightEdge)
            {
                wrapOffsetX = -screenWidth;
            }

            if (Mathf.Approximately(wrapOffsetX, 0f))
            {
                SetWrappedVisualsActive(false);
                return;
            }

            SetWrappedVisualsActive(true);
            CopyWrappedVisuals(new Vector3(wrapOffsetX, 0f, 0f));
        }

        /// <summary>
        /// Копирует состояние исходных SpriteRenderer в wrap-клоны.
        /// </summary>
        private void CopyWrappedVisuals(Vector3 offset)
        {
            for (var index = 0; index < sourceRenderers.Count; index++)
            {
                var source = sourceRenderers[index];
                var clone = index < wrapRendererClones.Count ? wrapRendererClones[index] : null;
                if (source == null || clone == null)
                {
                    if (clone != null)
                    {
                        clone.gameObject.SetActive(false);
                    }

                    cachedVisualBoundsFrame = -1;
                    continue;
                }

                try
                {
                    clone.gameObject.SetActive(source.gameObject.activeInHierarchy);
                    clone.enabled = source.enabled;
                    clone.sprite = source.sprite;
                    clone.color = source.color;
                    clone.flipX = source.flipX;
                    clone.flipY = source.flipY;
                    clone.drawMode = source.drawMode;
                    clone.size = source.size;
                    clone.maskInteraction = source.maskInteraction;
                    clone.sortingLayerID = source.sortingLayerID;
                    clone.sortingOrder = source.sortingOrder;
                    clone.sharedMaterial = source.sharedMaterial;
                    clone.transform.SetPositionAndRotation(source.transform.position + offset, source.transform.rotation);
                    clone.transform.localScale = source.transform.lossyScale;
                    clone.gameObject.layer = source.gameObject.layer;
                }
                catch (MissingReferenceException)
                {
                    sourceRenderers[index] = null;
                    clone.gameObject.SetActive(false);
                    cachedVisualBoundsFrame = -1;
                }
            }
        }

        /// <summary>
        /// Включает или выключает корень визуальных wrap-клонов.
        /// </summary>
        private void SetWrappedVisualsActive(bool isActive)
        {
            if (wrapVisualCloneRoot != null && wrapVisualCloneRoot.gameObject.activeSelf != isActive)
            {
                wrapVisualCloneRoot.gameObject.SetActive(isActive);
            }
        }

        /// <summary>
        /// Возвращает суммарные визуальные bounds игрока для wrap-логики.
        /// </summary>
        private Bounds GetPlayerVisualBounds()
        {
            if (cachedVisualBoundsFrame == Time.frameCount)
            {
                return cachedVisualBounds;
            }

            var hasBounds = false;
            var bounds = default(Bounds);

            for (var index = sourceRenderers.Count - 1; index >= 0; index--)
            {
                var sourceRenderer = sourceRenderers[index];
                if (sourceRenderer == null)
                {
                    sourceRenderers.RemoveAt(index);
                    if (index < wrapRendererClones.Count)
                    {
                        var clone = wrapRendererClones[index];
                        if (clone != null)
                        {
                            if (Application.isPlaying)
                            {
                                Destroy(clone.gameObject);
                            }
                            else
                            {
                                DestroyImmediate(clone.gameObject);
                            }
                        }

                        wrapRendererClones.RemoveAt(index);
                    }

                    continue;
                }

                try
                {
                    if (!sourceRenderer.enabled || !sourceRenderer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    if (hasBounds)
                    {
                        bounds.Encapsulate(sourceRenderer.bounds);
                    }
                    else
                    {
                        bounds = sourceRenderer.bounds;
                        hasBounds = true;
                    }
                }
                catch (MissingReferenceException)
                {
                    sourceRenderers.RemoveAt(index);
                    if (index < wrapRendererClones.Count)
                    {
                        var clone = wrapRendererClones[index];
                        if (clone != null)
                        {
                            if (Application.isPlaying)
                            {
                                Destroy(clone.gameObject);
                            }
                            else
                            {
                                DestroyImmediate(clone.gameObject);
                            }
                        }

                        wrapRendererClones.RemoveAt(index);
                    }
                }
            }

            if (hasBounds)
            {
                cachedVisualBounds = bounds;
                cachedVisualBoundsFrame = Time.frameCount;
                return cachedVisualBounds;
            }

            cachedVisualBounds = playerCollider != null ? playerCollider.bounds : new Bounds(transform.position, Vector3.zero);
            cachedVisualBoundsFrame = Time.frameCount;
            return cachedVisualBounds;
        }

        /// <summary>
        /// Проверяет, какие параметры действительно есть в Animator Controller игрока.
        /// </summary>
        private void CacheAnimatorParameters()
        {
            if (animator == null)
            {
                return;
            }

            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == SpeedHash)
                {
                    hasSpeedParameter = true;
                }
                else if (parameter.nameHash == VerticalSpeedHash)
                {
                    hasVerticalSpeedParameter = true;
                }
                else if (parameter.nameHash == GroundedHash)
                {
                    hasGroundedParameter = true;
                }
                else if (parameter.nameHash == JumpHash)
                {
                    hasJumpParameter = true;
                }
                else if (parameter.nameHash == DeathHash)
                {
                    hasDeathParameter = true;
                    deathParameterType = parameter.type;
                }
            }
        }

        /// <summary>
        /// Передает скорость и одноразовый импульс прыжка в Animator.
        /// </summary>
        private void UpdateAnimatorParameters()
        {
            if (animator == null)
            {
                return;
            }

            var velocity = body.linearVelocity;

            if (hasSpeedParameter)
            {
                animator.SetFloat(SpeedHash, Mathf.Abs(velocity.x));
            }

            if (hasVerticalSpeedParameter)
            {
                animator.SetFloat(VerticalSpeedHash, velocity.y);
            }

            if (hasGroundedParameter)
            {
                animator.SetBool(GroundedHash, false);
            }

            if (hasJumpParameter)
            {
                animator.SetBool(JumpHash, Time.time < jumpAnimationPulseUntil);
            }
        }

        /// <summary>
        /// Запускает death-триггер Animator, если он есть в текущем контроллере.
        /// </summary>
        private void TriggerDeathAnimation()
        {
            if (animator != null && hasDeathParameter)
            {
                animator.SetTrigger(DeathHash);
            }
        }

        /// <summary>
        /// Считывает горизонтальный ввод из Input System или legacy Input Manager.
        /// </summary>
        private float ReadHorizontalInput()
        {
#if ENABLE_INPUT_SYSTEM
            RegisterMoveAction();
            var actionInput = moveAction != null ? moveAction.ReadValue<Vector2>().x : 0f;
            if (Mathf.Abs(actionInput) > 0.001f)
            {
                return Mathf.Clamp(actionInput, -1f, 1f);
            }

            return ReadHorizontalInputFallback();
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetAxisRaw("Horizontal");
#else
            return 0f;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private static float ReadHorizontalInputFallback()
        {
            var input = 0f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
                {
                    input -= 1f;
                }

                if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
                {
                    input += 1f;
                }
            }

            if (Gamepad.current != null)
            {
                input += Gamepad.current.leftStick.x.ReadValue();
            }

            if (ComControllerDevice.current != null)
            {
                if (ComControllerDevice.current.button02.isPressed)
                {
                    input -= 1f;
                }

                if (ComControllerDevice.current.button03.isPressed)
                {
                    input += 1f;
                }
            }

            return Mathf.Clamp(input, -1f, 1f);
        }

        private void RegisterMoveAction()
        {
            if (moveAction != null)
            {
                return;
            }

            moveAction = ResolveMoveAction();
            if (moveAction == null)
            {
                return;
            }

            moveActionWasEnabled = moveAction.enabled;
            if (!moveAction.enabled)
            {
                moveAction.Enable();
            }
        }

        private void UnregisterMoveAction()
        {
            if (moveAction == null)
            {
                return;
            }

            if (!moveActionWasEnabled && moveAction.enabled)
            {
                moveAction.Disable();
            }

            moveAction = null;
            moveActionWasEnabled = false;
        }

        private InputAction ResolveMoveAction()
        {
            if (moveActionReference != null && moveActionReference.action != null)
            {
                return moveActionReference.action;
            }

            if (InputSystem.actions != null)
            {
                var projectMoveAction = InputSystem.actions.FindAction(moveActionPath, false);
                if (projectMoveAction != null)
                {
                    return projectMoveAction;
                }
            }

            return inputActionsAsset != null ? inputActionsAsset.FindAction(moveActionPath, false) : null;
        }
#endif
    }
}
