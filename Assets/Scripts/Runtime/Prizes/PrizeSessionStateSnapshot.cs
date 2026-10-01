using System;
using System.Collections.Generic;
using System.Linq;

namespace DanroJump.Prizes
{
    /// <summary>
    /// Снимок призового состояния для persistent recovery между перезапусками.
    /// </summary>
    public sealed class PrizeSessionStateSnapshot
    {
        public PrizeSessionStateSnapshot(
            IReadOnlyList<PrizeSessionRecord> history,
            IReadOnlyCollection<string> issuedSessionIds,
            string activeSessionId,
            int startedCount,
            int completedCount,
            int failedCount,
            int duplicateBlockedCount,
            int fallbackSessionCounter,
            IReadOnlyList<GameplaySessionRecord> gameplayHistory = null,
            int totalGamesPlayed = 0,
            int totalScore = 0,
            int totalCoins = 0,
            int qrShows = 0,
            int physicalDispenses = 0,
            int physicalDispenseFailures = 0,
            int comErrors = 0)
        {
            History = history?.Where(static record => record != null).ToArray() ?? Array.Empty<PrizeSessionRecord>();
            IssuedSessionIds = issuedSessionIds?.Where(static sessionId => !string.IsNullOrWhiteSpace(sessionId)).ToArray() ?? Array.Empty<string>();
            ActiveSessionId = activeSessionId ?? string.Empty;
            StartedCount = Math.Max(0, startedCount);
            CompletedCount = Math.Max(0, completedCount);
            FailedCount = Math.Max(0, failedCount);
            DuplicateBlockedCount = Math.Max(0, duplicateBlockedCount);
            FallbackSessionCounter = Math.Max(0, fallbackSessionCounter);
            GameplayHistory = gameplayHistory?.Where(static record => record != null).ToArray() ?? Array.Empty<GameplaySessionRecord>();
            TotalGamesPlayed = Math.Max(0, totalGamesPlayed);
            TotalScore = Math.Max(0, totalScore);
            TotalCoins = Math.Max(0, totalCoins);
            QrShows = Math.Max(0, qrShows);
            PhysicalDispenses = Math.Max(0, physicalDispenses);
            PhysicalDispenseFailures = Math.Max(0, physicalDispenseFailures);
            ComErrors = Math.Max(0, comErrors);
        }

        public IReadOnlyList<PrizeSessionRecord> History { get; }
        public IReadOnlyList<GameplaySessionRecord> GameplayHistory { get; }
        public IReadOnlyCollection<string> IssuedSessionIds { get; }
        public string ActiveSessionId { get; }
        public int StartedCount { get; }
        public int CompletedCount { get; }
        public int FailedCount { get; }
        public int DuplicateBlockedCount { get; }
        public int FallbackSessionCounter { get; }
        public int TotalGamesPlayed { get; }
        public int TotalScore { get; }
        public int TotalCoins { get; }
        public int QrShows { get; }
        public int PhysicalDispenses { get; }
        public int PhysicalDispenseFailures { get; }
        public int ComErrors { get; }
    }
}
