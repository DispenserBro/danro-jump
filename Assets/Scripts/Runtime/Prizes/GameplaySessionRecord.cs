using System;

namespace DanroJump.Prizes
{
    /// <summary>
    /// Запись о сыгранной игровой сессии для эксплуатационной истории.
    /// </summary>
    [Serializable]
    public sealed class GameplaySessionRecord
    {
        public GameplaySessionRecord(
            string sessionId,
            int score,
            int coins,
            float maxHeight,
            int deathCount,
            PrizeRewardKind rewardKind,
            DateTime timestamp)
        {
            SessionId = sessionId ?? string.Empty;
            Score = score;
            Coins = coins;
            MaxHeight = maxHeight;
            DeathCount = deathCount;
            RewardKind = rewardKind;
            Timestamp = timestamp;
        }

        public string SessionId { get; }
        public int Score { get; }
        public int Coins { get; }
        public float MaxHeight { get; }
        public int DeathCount { get; }
        public PrizeRewardKind RewardKind { get; }
        public DateTime Timestamp { get; }
    }
}
