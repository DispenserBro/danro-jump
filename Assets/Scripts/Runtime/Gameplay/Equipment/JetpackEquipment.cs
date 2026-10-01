using System.Threading;
using Cysharp.Threading.Tasks;
using DanroJump.Audio;
using DanroJump.Runtime.Gameplay.Equipment;
using UnityEngine;
using UnityEngine.VFX;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Джетпак как оборудование платформы: поднимает игрока вверх и крепит visual к точке экипировки.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JetpackEquipment : PlatformEquipmentBase, IPickable, ILandingEquipment
    {
        [SerializeField] [Min(0f)] private float liftSpeed = 18f;
        [SerializeField] [Min(0f)] private float liftHeight = 100f;
        [SerializeField] private string attachmentSlotId = "Jetpack";
        [SerializeField] private bool replaceExistingInSlot = true;
        [SerializeField] private string equippedSortingLayerName = "Player";
        [SerializeField] private int equippedSortingOrder = 160;
        [SerializeField] private int equippedEffectSortingOrder = 155;
        [SerializeField] private float detachWhenFallingVelocity = -0.01f;
        [SerializeField] [Min(0f)] private float detachHorizontalSpeed = 2.4f;
        [SerializeField] private float detachVerticalSpeed = 2.2f;
        [SerializeField] [Min(0f)] private float detachedGravityScale = 2.2f;
        [SerializeField] private float detachedAngularVelocity = 180f;

        private readonly System.Collections.Generic.List<Collider2D> colliders = new();
        private readonly System.Collections.Generic.List<SpriteRenderer> spriteRenderers = new();
        private Rigidbody2D pickupBody;
        private IGameAudioService audioService;
        private bool isCollected;
        private bool isAttachedToPlayer;
        private CancellationTokenSource lifetimeCancellation;
        private CancellationTokenSource destroyWhenOutsideCameraCancellation;

        [Zenject.Inject]
        public void Construct([Zenject.InjectOptional] IGameAudioService injectedAudioService = null)
        {
            audioService = injectedAudioService;
        }

        /// <summary>
        /// Сбрасывает состояние подбора при повторном спавне оборудования.
        /// </summary>
        public override void ResetEquipment()
        {
            CancelLifetimeTask();
            CancelDestroyWhenOutsideCameraTask();

            isCollected = false;
            isAttachedToPlayer = false;
            EnsurePickupCollidersAreTriggers();
            ResetPickupBody();
            SetPickupPhysicsEnabled(true);
            CacheSpriteRenderers();
            StopAttachedEffects(gameObject);
        }

        public override void HandlePlayerContact(JumpPlayerController player, Component source)
        {
            TryPickUp(player);
        }

        public bool TryActivateFromLanding(JumpPlayerController player)
        {
            if (!TryPickUp(player))
            {
                return false;
            }

            NotifyPlatformLandingHandlers(player);
            return true;
        }

        /// <summary>
        /// Подбирает джетпак и крепит его visual к точке экипировки игрока.
        /// </summary>
        public bool TryPickUp(JumpPlayerController player)
        {
            if (isCollected || player == null || player.IgnoresEnvironmentInteractions)
            {
                return false;
            }

            audioService?.Play(GameAudioEvent.JetpackCollected, transform.position);
            var attachment = player.GetComponent<PlayerEquipmentAttachment>();
            if (attachment == null)
            {
                Debug.LogWarning($"{nameof(JetpackEquipment)} requires {nameof(PlayerEquipmentAttachment)} on the player.", this);
                isCollected = true;
                player.ApplyHeightLift(liftSpeed, liftHeight);
                player.PlayJumpMidAirAnimationIfNeeded();
                MarkDetachedFromPlatform();
                DestroyReplacementPickup();
                return true;
            }

            if (!attachment.TryReserveSlot(attachmentSlotId))
            {
                isCollected = true;
                player.ApplyHeightLift(liftSpeed, liftHeight);
                player.PlayJumpMidAirAnimationIfNeeded();
                if (attachment.TryGetAttached(attachmentSlotId, out var attachedJetpack))
                {
                    if (attachedJetpack.TryGetComponent(out JetpackEquipment equippedJetpack))
                    {
                        equippedJetpack.RestartAttachedLifecycle(player, attachment);
                    }
                    else
                    {
                        PlayAttachedEffects(attachedJetpack);
                    }
                }

                DestroyReplacementPickup();
                return true;
            }

            isCollected = true;
            player.ApplyHeightLift(liftSpeed, liftHeight);
            player.PlayJumpMidAirAnimationIfNeeded();
            MarkDetachedFromPlatform();
            AttachToPlayer(player, attachment);
            return true;
        }

        private void AttachToPlayer(JumpPlayerController player, PlayerEquipmentAttachment attachment)
        {
            if (player == null || attachment == null)
            {
                if (attachment != null)
                {
                    attachment.ReleaseReservation(attachmentSlotId);
                }

                return;
            }

            SetPickupPhysicsEnabled(false);
            ApplyEquippedSorting();

            var attachedObject = attachment.Attach(gameObject, attachmentSlotId, replaceExistingInSlot);
            if (attachedObject == null)
            {
                attachment.ReleaseReservation(attachmentSlotId);
                DestroyReplacementPickup();
                return;
            }

            isAttachedToPlayer = true;
            attachedObject.transform.localScale = Vector3.one;
            player.RefreshVisualRenderers();
            PlayAttachedEffects(attachedObject);

            StartAttachedLifetime(player, attachment);
        }

        private void RestartAttachedLifecycle(JumpPlayerController player, PlayerEquipmentAttachment attachment)
        {
            isAttachedToPlayer = true;
            PlayAttachedEffects(gameObject);
            StartAttachedLifetime(player, attachment);
        }

        private void DestroyReplacementPickup()
        {
            MarkDetachedFromPlatform();
            StopAttachedEffects(gameObject);
            SetPickupPhysicsEnabled(false);
            transform.SetParent(null, true);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            CancelLifetimeTask();
            CancelDestroyWhenOutsideCameraTask();

            StopAttachedEffects(gameObject);
        }

        private void StartAttachedLifetime(JumpPlayerController player, PlayerEquipmentAttachment attachment)
        {
            CancelLifetimeTask();
            lifetimeCancellation = new CancellationTokenSource();
            DetachWhenFallingAsync(player, attachment, lifetimeCancellation).Forget(Debug.LogException);
        }

        private async UniTask DetachWhenFallingAsync(
            JumpPlayerController player,
            PlayerEquipmentAttachment attachment,
            CancellationTokenSource cancellationSource)
        {
            try
            {
                var token = cancellationSource.Token;
                var hasObservedActiveLift = false;

                while (player != null && attachment != null && !token.IsCancellationRequested)
                {
                    if (!attachment.TryGetAttached(attachmentSlotId, out var attachedObject) || attachedObject != gameObject)
                    {
                        return;
                    }

                    if (player.IsDead)
                    {
                        DetachFromPlayer(player, attachment);
                        return;
                    }

                    if (player.IsHeightLiftActive)
                    {
                        hasObservedActiveLift = true;
                    }

                    var fallingThreshold = Mathf.Max(0f, detachWhenFallingVelocity);
                    if (hasObservedActiveLift && player.VerticalVelocity <= fallingThreshold)
                    {
                        DetachFromPlayer(player, attachment);
                        return;
                    }

                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, token);
                }
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                if (lifetimeCancellation == cancellationSource)
                {
                    lifetimeCancellation = null;
                    cancellationSource.Dispose();
                }
            }
        }

        private void DetachFromPlayer(JumpPlayerController player, PlayerEquipmentAttachment attachment)
        {
            if (!isAttachedToPlayer)
            {
                return;
            }

            isAttachedToPlayer = false;
            StopAttachedEffects(gameObject);

            if (attachment == null || !attachment.TryDetach(attachmentSlotId, gameObject, out _))
            {
                transform.SetParent(null, true);
            }

            SetDetachedPhysicsMotion(player);
            CancelDestroyWhenOutsideCameraTask();
            destroyWhenOutsideCameraCancellation = new CancellationTokenSource();
            DestroyWhenOutsideCameraAsync(destroyWhenOutsideCameraCancellation).Forget(Debug.LogException);
        }

        private async UniTask DestroyWhenOutsideCameraAsync(CancellationTokenSource cancellationSource)
        {
            try
            {
                var token = cancellationSource.Token;
                while (!IsOutsideCamera() && !token.IsCancellationRequested)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                if (!token.IsCancellationRequested)
                {
                    Destroy(gameObject);
                }
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                if (destroyWhenOutsideCameraCancellation == cancellationSource)
                {
                    destroyWhenOutsideCameraCancellation = null;
                    cancellationSource.Dispose();
                }
            }
        }

        private bool IsOutsideCamera()
        {
            var gameplayCamera = Camera.main;
            if (gameplayCamera == null)
            {
                return false;
            }

            var viewportPosition = gameplayCamera.WorldToViewportPoint(transform.position);
            return viewportPosition.y < 0f || viewportPosition.y > 1f || viewportPosition.x < 0f || viewportPosition.x > 1f;
        }

        private void MarkDetachedFromPlatform()
        {
            if (TryGetComponent(out PlatformSpawnedContent marker))
            {
                marker.DetachFromPlatform();
            }
        }

        private void CancelLifetimeTask()
        {
            if (lifetimeCancellation == null)
            {
                return;
            }

            var cancellation = lifetimeCancellation;
            lifetimeCancellation = null;
            cancellation.Cancel();
            cancellation.Dispose();
        }

        private void CancelDestroyWhenOutsideCameraTask()
        {
            if (destroyWhenOutsideCameraCancellation == null)
            {
                return;
            }

            var cancellation = destroyWhenOutsideCameraCancellation;
            destroyWhenOutsideCameraCancellation = null;
            cancellation.Cancel();
            cancellation.Dispose();
        }

        private void SetPickupPhysicsEnabled(bool isEnabled)
        {
            pickupBody ??= GetComponent<Rigidbody2D>();
            if (pickupBody != null)
            {
                pickupBody.simulated = isEnabled;
            }

            SetPickupCollidersEnabled(isEnabled);
        }

        private void ResetPickupBody()
        {
            pickupBody ??= GetComponent<Rigidbody2D>();
            if (pickupBody == null)
            {
                return;
            }

            pickupBody.bodyType = RigidbodyType2D.Kinematic;
            pickupBody.gravityScale = 0f;
            pickupBody.linearVelocity = Vector2.zero;
            pickupBody.angularVelocity = 0f;
        }

        private void SetDetachedPhysicsMotion(JumpPlayerController player)
        {
            SetPickupCollidersEnabled(false);
            pickupBody ??= GetComponent<Rigidbody2D>();
            if (pickupBody == null)
            {
                return;
            }

            var horizontalDirection = -1f;
            if (player != null)
            {
                var deltaX = transform.position.x - player.transform.position.x;
                if (Mathf.Abs(deltaX) > 0.01f)
                {
                    horizontalDirection = Mathf.Sign(deltaX);
                }
            }

            pickupBody.bodyType = RigidbodyType2D.Dynamic;
            pickupBody.gravityScale = detachedGravityScale;
            pickupBody.simulated = true;
            pickupBody.linearVelocity = new Vector2(horizontalDirection * detachHorizontalSpeed, detachVerticalSpeed);
            pickupBody.angularVelocity = horizontalDirection * detachedAngularVelocity;
        }

        private void SetPickupCollidersEnabled(bool isEnabled)
        {
            GetComponentsInChildren(true, colliders);
            for (var index = 0; index < colliders.Count; index++)
            {
                var pickupCollider = colliders[index];
                if (pickupCollider != null)
                {
                    pickupCollider.compositeOperation = Collider2D.CompositeOperation.None;
                    pickupCollider.enabled = isEnabled;
                }
            }

            colliders.Clear();
        }

        private void EnsurePickupCollidersAreTriggers()
        {
            GetComponentsInChildren(true, colliders);
            for (var index = 0; index < colliders.Count; index++)
            {
                var pickupCollider = colliders[index];
                if (pickupCollider != null)
                {
                    pickupCollider.isTrigger = true;
                    pickupCollider.compositeOperation = Collider2D.CompositeOperation.None;
                }
            }

            colliders.Clear();
        }

        private void CacheSpriteRenderers()
        {
            spriteRenderers.Clear();
            GetComponentsInChildren(true, spriteRenderers);
        }

        private void ApplyEquippedSorting()
        {
            CacheSpriteRenderers();
            for (var index = 0; index < spriteRenderers.Count; index++)
            {
                var spriteRenderer = spriteRenderers[index];
                if (spriteRenderer == null)
                {
                    continue;
                }

                spriteRenderer.sortingLayerName = equippedSortingLayerName;
                spriteRenderer.sortingOrder = equippedSortingOrder;
            }

            var vfxRenderers = GetComponentsInChildren<VFXRenderer>(true);
            for (var index = 0; index < vfxRenderers.Length; index++)
            {
                var vfxRenderer = vfxRenderers[index];
                if (vfxRenderer == null)
                {
                    continue;
                }

                vfxRenderer.sortingLayerName = equippedSortingLayerName;
                vfxRenderer.sortingOrder = equippedEffectSortingOrder;
            }
        }

        private void PlayAttachedEffects(GameObject attachedObject)
        {
            if (attachedObject == null)
            {
                return;
            }

            var effects = attachedObject.GetComponentsInChildren<VisualEffect>(true);
            for (var index = 0; index < effects.Length; index++)
            {
                var effect = effects[index];
                if (effect == null)
                {
                    continue;
                }

                effect.gameObject.SetActive(true);
                if (effect.TryGetComponent<JetpackFlameVfxSettings>(out var settings))
                {
                    settings.Play(equippedSortingLayerName, equippedEffectSortingOrder);
                    continue;
                }

                effect.enabled = true;
                if (effect.TryGetComponent<VFXRenderer>(out var vfxRenderer))
                {
                    vfxRenderer.enabled = true;
                    vfxRenderer.sortingLayerName = equippedSortingLayerName;
                    vfxRenderer.sortingOrder = equippedEffectSortingOrder;
                }

                effect.Reinit();
                effect.Play();
            }
        }

        private static void StopAttachedEffects(GameObject attachedObject)
        {
            if (attachedObject == null)
            {
                return;
            }

            var effects = attachedObject.GetComponentsInChildren<VisualEffect>(true);
            for (var index = 0; index < effects.Length; index++)
            {
                var effect = effects[index];
                if (effect == null)
                {
                    continue;
                }

                if (effect.TryGetComponent<JetpackFlameVfxSettings>(out var settings))
                {
                    settings.Stop();
                }
                else
                {
                    effect.Stop();
                    effect.enabled = false;
                }
            }
        }
    }
}
