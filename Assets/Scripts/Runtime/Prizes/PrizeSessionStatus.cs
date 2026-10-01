namespace DanroJump.Prizes
{
    /// <summary>
    /// Состояние runtime-выдачи приза.
    /// </summary>
    public enum PrizeSessionStatus
    {
        Pending = 0,
        Shown = 1,
        Completed = 2,
        Failed = 3,
        DuplicateBlocked = 4,
    }
}
