using System;
using DanroJump.Gameplay;
using DanroJump.Settings;

namespace DanroJump.Prizes
{
    /// <summary>
    /// Базовый сервис призового решения. Пока использует сервисные флаги призов,
    /// позже сюда подключатся таблицы уровней, QR и аппаратная витрина.
    /// </summary>
    public sealed class PrizeFlowService : IPrizeFlowService
    {
        private readonly IReadOnlyServiceSettings settings;
        private readonly IPrizeSessionStateService sessionState;

        public PrizeFlowService(
            [Zenject.InjectOptional] IReadOnlyServiceSettings settings = null,
            [Zenject.InjectOptional] IPrizeSessionStateService sessionState = null)
        {
            this.settings = settings;
            this.sessionState = sessionState;
        }

        public event Action<PrizeFlowResult> PrizeFlowStarted;

        public PrizeFlowResult LastResult { get; private set; }

        public PrizeFlowResult EvaluateCompletedSession(GameplaySession session)
        {
            var score = session?.Score ?? 0;
            var rewardKind = ResolveRewardKind(score);
            return new PrizeFlowResult(
                rewardKind,
                score,
                session?.Coins ?? 0,
                session?.MaxRunHeight ?? 0f,
                session?.SessionId);
        }

        public PrizeFlowResult StartPrizeFlow(GameplaySession session)
        {
            LastResult = EvaluateCompletedSession(session);

            if (session != null && sessionState != null)
            {
                sessionState.RecordGameplaySession(
                    session.SessionId,
                    session.Score,
                    session.Coins,
                    session.MaxRunHeight,
                    session.DeathCount,
                    LastResult.RewardKind
                );
            }

            if (LastResult.HasPrize && sessionState != null &&
                !sessionState.TryBeginSession(LastResult, out _))
            {
                LastResult = default;
                return LastResult;
            }

            PrizeFlowStarted?.Invoke(LastResult);
            return LastResult;
        }

        private PrizeRewardKind ResolveRewardKind(int score)
        {
            if (settings == null)
            {
                return PrizeRewardKind.None;
            }

            var levels = PrizeLevelTable.FromJson(settings.GetString(ServiceSettingsKeys.PrizeLevels));
            if (levels.Count > 0)
            {
                return levels.ResolveRewardKind(score);
            }

            if (settings.GetBool(ServiceSettingsKeys.GiveBigPrize))
            {
                return PrizeRewardKind.Big;
            }

            return settings.GetBool(ServiceSettingsKeys.GiveSmallPrize)
                ? PrizeRewardKind.Small
                : PrizeRewardKind.None;
        }
    }
}
