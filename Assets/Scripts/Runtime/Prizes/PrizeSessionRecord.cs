using System;

namespace DanroJump.Prizes
{
    /// <summary>
    /// Запись истории решения о призе и его выдачи.
    /// </summary>
    public sealed class PrizeSessionRecord
    {
        public PrizeSessionRecord(
            string sessionId,
            PrizeFlowResult result,
            PrizeSessionStatus status,
            DateTime startedAtUtc,
            DateTime? updatedAtUtc,
            string note = null)
        {
            SessionId = sessionId ?? string.Empty;
            Result = result;
            Status = status;
            StartedAtUtc = startedAtUtc;
            UpdatedAtUtc = updatedAtUtc ?? startedAtUtc;
            Note = note ?? string.Empty;
        }

        public PrizeSessionRecord(
            string sessionId,
            PrizeFlowResult result,
            PrizeSessionStatus status,
            DateTime startedAtUtc,
            string note = null)
            : this(sessionId, result, status, startedAtUtc, null, note)
        {
        }

        public string SessionId { get; }
        public PrizeFlowResult Result { get; }
        public PrizeSessionStatus Status { get; private set; }
        public DateTime StartedAtUtc { get; }
        public DateTime UpdatedAtUtc { get; private set; }
        public string Note { get; private set; }

        public void SetStatus(PrizeSessionStatus status, DateTime updatedAtUtc, string note = null)
        {
            Status = status;
            UpdatedAtUtc = updatedAtUtc;
            Note = note ?? string.Empty;
        }
    }
}
