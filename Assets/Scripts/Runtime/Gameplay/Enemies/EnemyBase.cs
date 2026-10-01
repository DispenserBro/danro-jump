using UnityEngine;
using UnityEngine.U2D.Animation;
using DanroJump.Audio;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Базовый класс противника с обработкой trigger- и collision-контакта с игроком.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public abstract class EnemyBase : MonoBehaviour, IEnemy, IPlayerContactHandler
    {
        [SerializeField] private EnemySpawnKind spawnKind = EnemySpawnKind.GroundedOnly;
        [SerializeField] [Min(0)] private int stompScore = 10;
        [SerializeField] private float stompTopTolerance = 0.2f;
        [SerializeField] [Range(0f, 1f)] private float stompMinimumHeightFactor = 0.55f;

        private GameplaySession session;
        private IGameAudioService audioService;
        private Collider2D enemyCollider;
        private SpriteSkin[] spriteSkins;
        private Renderer[] visualRenderers;
        private bool visualComponentsCached;
        private bool visualWarningLogged;

        /// <summary>
        /// Тип мест, где этот prefab врага разрешено создавать.
        /// </summary>
        public EnemySpawnKind SpawnKind => spawnKind;

        /// <summary>
        /// True, если врага можно создавать на точках платформ.
        /// </summary>
        public bool CanSpawnOnPlatform => spawnKind == EnemySpawnKind.GroundedOnly || spawnKind == EnemySpawnKind.Universal;

        /// <summary>
        /// True, если врага можно создавать процедурно в поле, отдельно от платформ.
        /// </summary>
        public bool CanSpawnProcedurally => spawnKind == EnemySpawnKind.FlyingOnly || spawnKind == EnemySpawnKind.Universal;

        /// <summary>
        /// True, если противник сейчас должен реагировать на игрока.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Получает игровую сессию для начисления очков за stomp.
        /// </summary>
        [Zenject.Inject]
        public void Construct(
            GameplaySession injectedSession,
            [Zenject.InjectOptional] IGameAudioService injectedAudioService = null)
        {
            session = injectedSession;
            audioService = injectedAudioService;
        }

        /// <summary>
        /// Активирует противника при включении объекта.
        /// </summary>
        protected virtual void OnEnable()
        {
            ConfigureRuntimeVisuals();
            ActivateEnemy();
        }

        /// <summary>
        /// Деактивирует противника при выключении объекта.
        /// </summary>
        protected virtual void OnDisable()
        {
            DeactivateEnemy();
        }

        /// <summary>
        /// Передает trigger-контакт с игроком в конкретную реализацию.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (PlayerContactUtility.TryGetAlivePlayer(other, out var player))
            {
                HandlePlayerContact(player, this);
            }
        }

        /// <summary>
        /// Передает физическое столкновение с игроком в конкретную реализацию.
        /// </summary>
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (PlayerContactUtility.TryGetAlivePlayer(collision.collider, out var player))
            {
                HandlePlayerContact(player, this);
            }
        }

        /// <summary>
        /// Включает противника.
        /// </summary>
        public virtual void ActivateEnemy()
        {
            IsActive = true;
            audioService?.Play(GameAudioEvent.EnemySpawned, transform.position);
        }

        /// <summary>
        /// Выключает противника.
        /// </summary>
        public virtual void DeactivateEnemy()
        {
            IsActive = false;
        }

        /// <summary>
        /// Сбрасывает противника к активному состоянию.
        /// </summary>
        public virtual void ResetEnemy()
        {
            ConfigureRuntimeVisuals();
            ActivateEnemy();
        }

        /// <summary>
        /// Обрабатывает stomp сверху или убивает игрока при обычном контакте.
        /// </summary>
        public virtual void HandlePlayerContact(JumpPlayerController player, Component source)
        {
            if (!IsActive || player == null || player.IgnoresEnvironmentInteractions)
            {
                return;
            }

            if (player.TryIgnoreEnemy(this))
            {
                return;
            }

            if (IsStomp(player))
            {
                HandleStomp(player);
                return;
            }

            audioService?.Play(GameAudioEvent.EnemyAttack, transform.position);
            player.Kill();
        }

        /// <summary>
        /// Реакция врага на успешное уничтожение прыжком сверху.
        /// </summary>
        protected virtual void HandleStomp(JumpPlayerController player)
        {
            session?.AddScore(stompScore);
            player.Bounce(player.AutoJumpVelocity);
            audioService?.Play(GameAudioEvent.EnemyDefeated, transform.position);
            DeactivateEnemy();
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Проверяет, что игрок падает и его GroundCheck находится выше верхней части противника.
        /// </summary>
        protected virtual bool IsStomp(JumpPlayerController player)
        {
            enemyCollider ??= GetComponent<Collider2D>();
            var bounds = enemyCollider != null
                ? enemyCollider.bounds
                : new Bounds(transform.position, Vector3.one);
            var groundCheck = player.GroundCheckPosition;
            var isAboveEnemyHorizontally =
                groundCheck.x >= bounds.min.x - stompTopTolerance &&
                groundCheck.x <= bounds.max.x + stompTopTolerance;
            var stompLineY = Mathf.Lerp(bounds.min.y, bounds.max.y, stompMinimumHeightFactor);
            var isInUpperZone =
                groundCheck.y >= stompLineY - stompTopTolerance ||
                player.PreviousGroundCheckY >= bounds.max.y - stompTopTolerance;

            return player.VerticalVelocity <= 0.05f &&
                isAboveEnemyHorizontally &&
                isInUpperZone;
        }

        /// <summary>
        /// Приводит 2D Animation-визуал врага к предсказуемому runtime-режиму для Player build.
        /// </summary>
        private void ConfigureRuntimeVisuals()
        {
            CacheVisualComponentsIfNeeded();

            if (spriteSkins != null)
            {
                foreach (var spriteSkin in spriteSkins)
                {
                    if (spriteSkin == null)
                    {
                        continue;
                    }

                    spriteSkin.alwaysUpdate = true;
                    spriteSkin.forceCpuDeformation = true;
                }
            }

            if (visualRenderers == null)
            {
                return;
            }

            foreach (var visualRenderer in visualRenderers)
            {
                if (visualRenderer != null)
                {
                    visualRenderer.allowOcclusionWhenDynamic = false;
                }
            }

            if (!HasUsableVisual())
            {
                DisableEnemyColliders();
                if (!visualWarningLogged)
                {
                    Debug.LogWarning($"{name} enemy has no usable visual renderer. Enemy colliders were disabled.", this);
                    visualWarningLogged = true;
                }
            }
        }

        private void CacheVisualComponentsIfNeeded()
        {
            if (visualComponentsCached)
            {
                return;
            }

            spriteSkins = GetComponentsInChildren<SpriteSkin>(true);
            visualRenderers = GetComponentsInChildren<Renderer>(true);
            visualComponentsCached = true;
        }

        private bool HasUsableVisual()
        {
            if (visualRenderers == null || visualRenderers.Length == 0)
            {
                return false;
            }

            foreach (var visualRenderer in visualRenderers)
            {
                if (visualRenderer == null)
                {
                    continue;
                }

                if (visualRenderer is SpriteRenderer spriteRenderer)
                {
                    if (spriteRenderer.sprite != null)
                    {
                        return true;
                    }

                    continue;
                }

                return true;
            }

            return false;
        }

        private void DisableEnemyColliders()
        {
            var colliders = GetComponentsInChildren<Collider2D>(true);
            foreach (var targetCollider in colliders)
            {
                if (targetCollider != null)
                {
                    targetCollider.enabled = false;
                }
            }
        }
    }
}
