using System.Collections.Generic;
using DanroJump.Gameplay;
using DanroJump.Prizes;
using DanroJump.Settings;
using NUnit.Framework;

namespace DanroJump.Tests.EditMode
{
    public sealed class PrizeFlowServiceTests
    {
        [Test]
        public void EvaluateCompletedSession_ReturnsNoneWhenPrizeFlagsAreDisabled()
        {
            var service = new PrizeFlowService(new StubReadOnlySettings(
                smallPrize: false,
                bigPrize: false));

            var result = service.EvaluateCompletedSession(null);

            Assert.That(result.RewardKind, Is.EqualTo(PrizeRewardKind.None));
            Assert.That(result.HasPrize, Is.False);
        }

        [Test]
        public void EvaluateCompletedSession_ReturnsSmallPrizeWhenSmallPrizeIsEnabled()
        {
            var service = new PrizeFlowService(new StubReadOnlySettings(
                smallPrize: true,
                bigPrize: false));

            var result = service.EvaluateCompletedSession(null);

            Assert.That(result.RewardKind, Is.EqualTo(PrizeRewardKind.Small));
            Assert.That(result.HasPrize, Is.True);
        }

        [Test]
        public void EvaluateCompletedSession_PrefersBigPrizeWhenBothPrizeFlagsAreEnabled()
        {
            var service = new PrizeFlowService(new StubReadOnlySettings(
                smallPrize: true,
                bigPrize: true));

            var result = service.EvaluateCompletedSession(null);

            Assert.That(result.RewardKind, Is.EqualTo(PrizeRewardKind.Big));
        }

        [Test]
        public void EvaluateCompletedSession_UsesConfiguredPrizeLevelsBeforeLegacyFlags()
        {
            var table = new PrizeLevelTable();
            table.Upsert(100, PrizeRewardKind.Small);
            table.Upsert(250, PrizeRewardKind.PrizeStand);
            var service = new PrizeFlowService(new StubReadOnlySettings(
                smallPrize: false,
                bigPrize: false,
                prizeLevelsJson: table.ToJson()));
            var session = new GameplaySession(null, new StubReadOnlySettings(false, false));
            session.AddScore(260);

            var result = service.EvaluateCompletedSession(session);

            Assert.That(result.Score, Is.EqualTo(260));
            Assert.That(result.RewardKind, Is.EqualTo(PrizeRewardKind.PrizeStand));
        }

        [Test]
        public void EvaluateCompletedSession_EmptyPrizeLevelsFallbacksToLegacyFlags()
        {
            var service = new PrizeFlowService(new StubReadOnlySettings(
                smallPrize: true,
                bigPrize: false,
                prizeLevelsJson: "{\"levels\":[]}"));

            var result = service.EvaluateCompletedSession(null);

            Assert.That(result.RewardKind, Is.EqualTo(PrizeRewardKind.Small));
        }

        [Test]
        public void StartPrizeFlow_StoresLastResultAndRaisesEvent()
        {
            var service = new PrizeFlowService(new StubReadOnlySettings(
                smallPrize: true,
                bigPrize: false));
            PrizeFlowResult raisedResult = default;
            var raised = false;
            service.PrizeFlowStarted += result =>
            {
                raised = true;
                raisedResult = result;
            };

            var result = service.StartPrizeFlow(null);

            Assert.That(raised, Is.True);
            Assert.That(raisedResult.RewardKind, Is.EqualTo(PrizeRewardKind.Small));
            Assert.That(service.LastResult.RewardKind, Is.EqualTo(result.RewardKind));
        }

        [Test]
        public void StartPrizeFlow_RegistersPrizeSessionStateBeforeRaisingEvent()
        {
            var sessionState = new PrizeSessionStateService();
            var service = new PrizeFlowService(
                new StubReadOnlySettings(smallPrize: true, bigPrize: false),
                sessionState);
            PrizeFlowResult raisedResult = default;
            service.PrizeFlowStarted += result => raisedResult = result;

            var result = service.StartPrizeFlow(null);

            Assert.That(result.HasPrize, Is.True);
            Assert.That(raisedResult.HasPrize, Is.True);
            Assert.That(sessionState.ActiveSession, Is.Not.Null);
            Assert.That(sessionState.ActiveSession.Result.RewardKind, Is.EqualTo(PrizeRewardKind.Small));
            Assert.That(sessionState.StartedCount, Is.EqualTo(1));
        }

        [Test]
        public void StartPrizeFlow_DoesNotRaiseEventWhenPrizeSessionIsBlockedAsDuplicate()
        {
            var sessionState = new PrizeSessionStateService();
            var service = new PrizeFlowService(
                new StubReadOnlySettings(
                    smallPrize: false,
                    bigPrize: false,
                    prizeLevelsJson: "{\"levels\":[{\"score\":0,\"rewardKind\":1}]}"),
                sessionState);
            var raisedCount = 0;
            service.PrizeFlowStarted += _ => raisedCount++;
            var session = new GameplaySession(null, new StubReadOnlySettings(false, false));

            var first = service.StartPrizeFlow(session);
            var second = service.StartPrizeFlow(session);

            Assert.That(first.HasPrize, Is.True);
            Assert.That(second.HasPrize, Is.False);
            Assert.That(raisedCount, Is.EqualTo(1));
            Assert.That(sessionState.DuplicateBlockedCount, Is.EqualTo(1));
        }

        private sealed class StubReadOnlySettings : IReadOnlyServiceSettings
        {
            private readonly bool smallPrize;
            private readonly bool bigPrize;
            private readonly string prizeLevelsJson;

            public StubReadOnlySettings(bool smallPrize, bool bigPrize, string prizeLevelsJson = "")
            {
                this.smallPrize = smallPrize;
                this.bigPrize = bigPrize;
                this.prizeLevelsJson = prizeLevelsJson;
            }

            public ServiceSettingsDatabase Database => null;
            public event System.Action SettingsLoaded { add { } remove { } }
            public event System.Action SettingsSaved { add { } remove { } }
            public event System.Action<string, ServiceSettingValue> SettingChanged { add { } remove { } }

            public bool TryGetDefinition(string key, out ServiceSettingDefinition definition)
            {
                definition = null;
                return false;
            }

            public IReadOnlyDictionary<string, ServiceSettingValue> Snapshot()
            {
                return new Dictionary<string, ServiceSettingValue>();
            }

            public ServiceSettingValue Get(string key)
            {
                return ServiceSettingValue.Bool(GetBool(key));
            }

            public bool GetBool(string key)
            {
                return key == ServiceSettingsKeys.GiveSmallPrize && smallPrize ||
                    key == ServiceSettingsKeys.GiveBigPrize && bigPrize;
            }

            public int GetInt(string key) => 0;
            public float GetFloat(string key) => 0f;
            public string GetString(string key)
            {
                return key == ServiceSettingsKeys.PrizeLevels ? prizeLevelsJson : string.Empty;
            }
            public int GetOptionIndex(string key) => 0;
        }
    }
}
