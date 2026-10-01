using System;
using DanroJump.Gameplay;

namespace DanroJump.Prizes
{
    /// <summary>
    /// Принимает решение о призе и сообщает UI/аппаратному слою о завершении забега.
    /// </summary>
    public interface IPrizeFlowService
    {
        event Action<PrizeFlowResult> PrizeFlowStarted;

        PrizeFlowResult LastResult { get; }

        PrizeFlowResult EvaluateCompletedSession(GameplaySession session);

        PrizeFlowResult StartPrizeFlow(GameplaySession session);
    }
}
