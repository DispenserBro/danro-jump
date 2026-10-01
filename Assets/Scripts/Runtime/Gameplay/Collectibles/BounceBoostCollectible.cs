using UnityEngine;
using DanroJump.Audio;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Collectible, который дает игроку усиленный вертикальный отскок и очки.
    /// </summary>
    public sealed class BounceBoostCollectible : CollectibleBase
    {
        [SerializeField] [Min(0f)] private float bounceVelocity = 24f;
        [SerializeField] [Min(0)] private int scoreValue = 5;

        private GameplaySession session;
        private IGameAudioService audioService;

        /// <summary>
        /// Получает игровую сессию для начисления очков.
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
        /// Применяет усиленный прыжок и награду за подбор.
        /// </summary>
        protected override void OnCollected(JumpPlayerController player)
        {
            audioService?.Play(GameAudioEvent.BoostCollected, transform.position);
            player.BounceWithEnemyPassThrough(bounceVelocity);
            session?.AddScore(scoreValue);
        }
    }
}
