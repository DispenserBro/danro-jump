namespace DanroJump.Audio
{
    /// <summary>
    /// Канонические события, для которых можно назначать клипы в GameAudioSettings.
    /// </summary>
    public enum GameAudioEvent
    {
        UiNavigate = 0,
        UiSubmit = 1,
        UiBack = 2,
        UiError = 3,
        GameStart = 10,
        PlayerJump = 20,
        PlayerDeath = 21,
        PlayerRespawn = 22,
        GameOver = 23,
        ContinueAccepted = 24,
        PlayerLand = 25,
        PlayerHurt = 26,
        PlayerBoostStarted = 27,
        PlayerBoostEnded = 28,
        CoinCollected = 30,
        BoostCollected = 31,
        SpringActivated = 32,
        JumpPadActivated = 33,
        JetpackCollected = 34,
        PrizeStarted = 40,
        PrizeQrStarted = 41,
        PrizePhysicalStarted = 42,
        EnemySpawned = 50,
        EnemyMoved = 51,
        EnemyAttack = 52,
        EnemyHit = 53,
        EnemyDefeated = 54
    }
}
