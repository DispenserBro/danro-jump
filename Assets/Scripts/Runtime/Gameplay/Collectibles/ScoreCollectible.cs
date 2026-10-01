using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Collectible, который начисляет только очки.
    /// </summary>
    public sealed class ScoreCollectible : CollectibleBase
    {
        [SerializeField] private int scoreValue = 1;

        private GameplaySession session;

        /// <summary>
        /// Количество очков, начисляемое при подборе.
        /// </summary>
        public int Value => scoreValue;

        /// <summary>
        /// Получает игровую сессию для начисления очков.
        /// </summary>
        [Zenject.Inject]
        public void Construct(GameplaySession injectedSession)
        {
            session = injectedSession;
        }

        /// <summary>
        /// Добавляет очки в текущую сессию после подбора.
        /// </summary>
        protected override void OnCollected(JumpPlayerController player)
        {
            session?.AddScore(scoreValue);
        }
    }
}
