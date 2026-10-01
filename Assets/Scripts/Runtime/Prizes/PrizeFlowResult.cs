namespace DanroJump.Prizes
{
    /// <summary>
    /// Результат решения о призе по завершенной игровой сессии.
    /// </summary>
    public readonly struct PrizeFlowResult
    {
        public PrizeFlowResult(PrizeRewardKind rewardKind, int score, int coins, float height, string sessionId = null)
        {
            RewardKind = rewardKind;
            Score = score;
            Coins = coins;
            Height = height;
            SessionId = sessionId ?? string.Empty;
        }

        public PrizeRewardKind RewardKind { get; }
        public int Score { get; }
        public int Coins { get; }
        public float Height { get; }
        public string SessionId { get; }
        public bool HasPrize => RewardKind != PrizeRewardKind.None;
    }
}
