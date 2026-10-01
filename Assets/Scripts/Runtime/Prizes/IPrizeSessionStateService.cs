using System;
using System.Collections.Generic;

namespace DanroJump.Prizes
{
    /// <summary>
    /// Хранит текущую призовую выдачу, историю решений и блокирует повторную выдачу одной сессии.
    /// </summary>
    public interface IPrizeSessionStateService
    {
        event Action Changed;

        PrizeSessionRecord ActiveSession { get; }
        IReadOnlyList<PrizeSessionRecord> History { get; }
        IReadOnlyList<GameplaySessionRecord> GameplayHistory { get; }
        int StartedCount { get; }
        int CompletedCount { get; }
        int FailedCount { get; }
        int DuplicateBlockedCount { get; }

        int TotalGamesPlayed { get; }
        int TotalScore { get; }
        int TotalCoins { get; }
        int QrShows { get; }
        int PhysicalDispenses { get; }
        int PhysicalDispenseFailures { get; }
        int ComErrors { get; }

        bool TryBeginSession(PrizeFlowResult result, out PrizeSessionRecord record);
        bool MarkActiveSessionShown();
        bool CompleteActiveSession(string note = null);
        bool FailActiveSession(string reason);
        void RecordGameplaySession(
            string sessionId,
            int score,
            int coins,
            float maxHeight,
            int deathCount,
            PrizeRewardKind rewardKind
        );
        void RecordQrShow();
        void RecordComError();
        void RecordPhysicalDispense(bool success);
        string BuildSummary();
    }
}
