using UnityEngine;
using DanroJump.Audio;
using DanroJump.Vfx;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Collectible монеты, который увеличивает счетчик монет и очки сессии.
    /// </summary>
    public sealed class CoinCollectible : CollectibleBase
    {
        [SerializeField] [Min(1)] private int coinValue = 1;
        [SerializeField] [Min(0)] private int scoreValue = 1;

        private GameplaySession session;
        private IGameAudioService audioService;
        private IVfxService vfxService;

        /// <summary>
        /// Получает игровую сессию для записи награды.
        /// </summary>
        [Zenject.Inject]
        public void Construct(
            GameplaySession injectedSession,
            [Zenject.InjectOptional] IGameAudioService injectedAudioService = null,
            [Zenject.InjectOptional] IVfxService injectedVfxService = null)
        {
            session = injectedSession;
            audioService = injectedAudioService;
            vfxService = injectedVfxService;
        }

        /// <summary>
        /// Начисляет монеты и очки после подбора.
        /// </summary>
        protected override void OnCollected(JumpPlayerController player)
        {
            audioService?.Play(GameAudioEvent.CoinCollected, transform.position);
            vfxService?.SpawnCoinCollectedEffect(transform.position);
            session?.AddCoins(coinValue);
            session?.AddScore(scoreValue);
        }
    }
}
