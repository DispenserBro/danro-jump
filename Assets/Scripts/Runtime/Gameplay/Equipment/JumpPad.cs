using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DanroJump.Audio;
using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Пружинная площадка, которая усиливает прыжок игрока и проигрывает squash/stretch-анимацию.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class JumpPad : PlatformEquipmentBase, ILandingEquipment
    {
        [SerializeField] [Min(1f)] private float jumpHeightMultiplier = 3f;
        [SerializeField] private float landingVelocityThreshold = 0.05f;
        [SerializeField] private float landingTopTolerance = 0.2f;
        [SerializeField] private float activationCooldown = 0.08f;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private string visualRootName = "Visual";
        [SerializeField] private Vector2 stretchScale = new(0.9f, 1.35f);
        [SerializeField] [Min(0f)] private float stretchOutDuration = 0.06f;
        [SerializeField] [Min(0f)] private float recoverDuration = 0.14f;

        private Collider2D jumpPadCollider;
        private IGameAudioService audioService;
        private CancellationTokenSource stretchCancellation;
        private Vector3 visualBaseScale = Vector3.one;
        private float lastActivationTime = -1f;
        private bool hasVisualBaseScale;

        /// <summary>
        /// Во сколько раз пружина усиливает стандартный прыжок игрока.
        /// </summary>
        public float JumpHeightMultiplier => jumpHeightMultiplier;

        [Zenject.Inject]
        public void Construct([Zenject.InjectOptional] IGameAudioService injectedAudioService = null)
        {
            audioService = injectedAudioService;
        }

        /// <summary>
        /// Подготавливает trigger-коллайдер и визуальную часть пружины.
        /// </summary>
        private void Awake()
        {
            EnsureTriggerCollider();
            ResolveVisualRoot();
            CacheVisualBaseScale();
        }

        /// <summary>
        /// Проверяет контакт при первом входе игрока в trigger.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            TryHandlePlayerContact(other);
        }

        /// <summary>
        /// Повторно проверяет контакт, если игрок уже находится внутри trigger.
        /// </summary>
        private void OnTriggerStay2D(Collider2D other)
        {
            TryHandlePlayerContact(other);
        }

        /// <summary>
        /// Преобразует Collider2D в игрока и передает контакт в общий handler.
        /// </summary>
        private void TryHandlePlayerContact(Collider2D other)
        {
            if (other.TryGetComponent(out JumpPlayerController player) && !player.IgnoresEnvironmentInteractions)
            {
                HandlePlayerContact(player, this);
            }
        }

        /// <summary>
        /// Сбрасывает cooldown, коллайдер и визуальный scale при переиспользовании.
        /// </summary>
        public override void ResetEquipment()
        {
            lastActivationTime = -1f;
            EnsureTriggerCollider();
            ResolveVisualRoot();
            CacheVisualBaseScale();
            ResetVisualScale();
        }

        /// <summary>
        /// Пробует активировать пружину от контакта игрока.
        /// </summary>
        public override void HandlePlayerContact(JumpPlayerController player, Component source)
        {
            TryActivate(player);
        }

        /// <summary>
        /// Активирует усиленный прыжок, если игрок действительно приземляется сверху.
        /// </summary>
        /// <returns>True, если пружина сработала.</returns>
        public bool TryActivate(JumpPlayerController player)
        {
            if (!CanActivate(player))
            {
                return false;
            }

            NotifyPlatformLandingHandlers(player);
            audioService?.Play(GameAudioEvent.JumpPadActivated, transform.position);
            player.BounceWithEnemyPassThrough(CalculateBoostVelocity(player));
            lastActivationTime = Time.time;
            PlayStretchAnimation();
            return true;
        }

        public bool TryActivateFromLanding(JumpPlayerController player)
        {
            return TryActivate(player);
        }

        /// <summary>
        /// Проверяет направление движения, cooldown и высоту GroundCheck относительно верха пружины.
        /// </summary>
        private bool CanActivate(JumpPlayerController player)
        {
            if (player == null || player.IgnoresEnvironmentInteractions || Time.time - lastActivationTime < activationCooldown)
            {
                return false;
            }

            var isLanding = player.VerticalVelocity <= landingVelocityThreshold ||
                player.PreviousVerticalVelocity <= landingVelocityThreshold;
            if (!isLanding)
            {
                return false;
            }

            if (jumpPadCollider == null)
            {
                EnsureTriggerCollider();
            }

            if (jumpPadCollider == null)
            {
                return false;
            }

            var landingLineY = jumpPadCollider.bounds.max.y - landingTopTolerance;
            return player.GroundCheckY >= landingLineY || player.PreviousGroundCheckY >= landingLineY;
        }

        /// <summary>
        /// Переводит множитель высоты в скорость прыжка через квадратный корень.
        /// </summary>
        private float CalculateBoostVelocity(JumpPlayerController player)
        {
            return player.AutoJumpVelocity * Mathf.Sqrt(jumpHeightMultiplier);
        }

        /// <summary>
        /// Гарантирует, что коллайдер пружины работает как trigger.
        /// </summary>
        private void EnsureTriggerCollider()
        {
            jumpPadCollider = GetComponent<Collider2D>();
            if (jumpPadCollider != null)
            {
                jumpPadCollider.isTrigger = true;
            }
        }

        /// <summary>
        /// Находит визуальный корень пружины для stretch-анимации.
        /// </summary>
        private void ResolveVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            visualRoot = transform.Find(visualRootName);
            if (visualRoot == null && TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                visualRoot = spriteRenderer.transform;
            }
        }

        /// <summary>
        /// Запоминает исходный scale визуала, чтобы корректно восстановить его после анимации.
        /// </summary>
        private void CacheVisualBaseScale()
        {
            if (visualRoot == null || hasVisualBaseScale)
            {
                return;
            }

            visualBaseScale = visualRoot.localScale;
            hasVisualBaseScale = true;
        }

        /// <summary>
        /// Запускает короткую анимацию растяжения пружины.
        /// </summary>
        private void PlayStretchAnimation()
        {
            ResolveVisualRoot();
            CacheVisualBaseScale();

            if (visualRoot == null)
            {
                return;
            }

            CancelStretchAnimation();
            stretchCancellation = new CancellationTokenSource();
            StretchVisualAsync(stretchCancellation).Forget(Debug.LogException);
        }

        /// <summary>
        /// Останавливает активную stretch-анимацию и возвращает базовый scale.
        /// </summary>
        private void ResetVisualScale()
        {
            CancelStretchAnimation();

            if (visualRoot != null && hasVisualBaseScale)
            {
                visualRoot.localScale = visualBaseScale;
            }
        }

        private void OnDestroy()
        {
            CancelStretchAnimation();
        }

        private void CancelStretchAnimation()
        {
            if (stretchCancellation == null)
            {
                return;
            }

            try
            {
                stretchCancellation.Cancel();
            }
            catch
            {
                // ignored
            }

            stretchCancellation.Dispose();
            stretchCancellation = null;
        }

        /// <summary>
        /// Последовательно растягивает визуал и возвращает его к исходному размеру.
        /// </summary>
        private async UniTask StretchVisualAsync(CancellationTokenSource cancellationSource)
        {
            var stretchedScale = new Vector3(
                visualBaseScale.x * stretchScale.x,
                visualBaseScale.y * stretchScale.y,
                visualBaseScale.z);

            var token = cancellationSource.Token;
            try
            {
                await AnimateVisualScaleAsync(visualBaseScale, stretchedScale, stretchOutDuration, token);
                await AnimateVisualScaleAsync(stretchedScale, visualBaseScale, recoverDuration, token);

                if (!token.IsCancellationRequested && visualRoot != null)
                {
                    visualRoot.localScale = visualBaseScale;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // ignored
            }
            finally
            {
                if (stretchCancellation == cancellationSource)
                {
                    stretchCancellation = null;
                    cancellationSource.Dispose();
                }
            }
        }

        /// <summary>
        /// Интерполирует scale визуального корня за указанное время.
        /// </summary>
        private async UniTask AnimateVisualScaleAsync(Vector3 from, Vector3 to, float duration, CancellationToken cancellationToken)
        {
            if (duration <= 0f)
            {
                if (visualRoot != null)
                {
                    visualRoot.localScale = to;
                }

                return;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (visualRoot == null)
                {
                    return;
                }

                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var easedT = 1f - (1f - t) * (1f - t);
                visualRoot.localScale = Vector3.LerpUnclamped(from, to, easedT);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (visualRoot != null)
            {
                visualRoot.localScale = to;
            }
        }
    }
}
