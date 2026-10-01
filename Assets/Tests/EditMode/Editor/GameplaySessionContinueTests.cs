using System;
using System.Collections.Generic;
using System.Reflection;
using DanroJump.Gameplay;
using DanroJump.Hardware.Com;
using DanroJump.Prizes;
using DanroJump.SceneFlow;
using DanroJump.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

namespace DanroJump.Tests.EditMode
{
    public sealed class GameplaySessionContinueTests
    {
        [Test]
        public void CanContinueGame_IsTrueOnlyWhenEnabledAndBalanceIsEnough()
        {
            var session = CreateGameOverSession(
                new StubSettings(canContinue: true, freePlay: false, continueCost: 5, additionalLives: 2),
                new StubWallet(balance: 5),
                out _,
                out _);

            Assert.That(session.CanContinueGame, Is.True);
            Assert.That(session.ContinueGameCost, Is.EqualTo(5));
            Assert.That(session.ContinueGameLives, Is.EqualTo(2));
        }

        [Test]
        public void TryContinueGame_SpendsBalanceAddsLivesAndCancelsMenuReturn()
        {
            var session = CreateGameOverSession(
                new StubSettings(canContinue: true, freePlay: false, continueCost: 5, additionalLives: 2),
                new StubWallet(balance: 8),
                out var sceneFlow,
                out var wallet);

            var continued = session.TryContinueGame();

            Assert.That(continued, Is.True);
            Assert.That(wallet.Credits, Is.EqualTo(3));
            Assert.That(wallet.SpendCalls, Is.EqualTo(1));
            Assert.That(session.RemainingLives, Is.EqualTo(2));
            Assert.That(session.State, Is.EqualTo(GameplaySessionState.Running));
            Assert.That(sceneFlow.CancelGameOverReturnCalls, Is.EqualTo(1));
        }

        [Test]
        public void TryContinueGame_ReturnsFalseWhenBalanceIsInsufficient()
        {
            var session = CreateGameOverSession(
                new StubSettings(canContinue: true, freePlay: false, continueCost: 5, additionalLives: 2),
                new StubWallet(balance: 4),
                out var sceneFlow,
                out var wallet);

            var continued = session.TryContinueGame();

            Assert.That(continued, Is.False);
            Assert.That(wallet.Credits, Is.EqualTo(4));
            Assert.That(wallet.SpendCalls, Is.EqualTo(0));
            Assert.That(session.State, Is.EqualTo(GameplaySessionState.GameOver));
            Assert.That(sceneFlow.CancelGameOverReturnCalls, Is.EqualTo(0));
        }

        [Test]
        public void TryContinueGame_UnblocksPlayerRespawnAfterGameOver()
        {
            var playerObject = new GameObject("Continue Player");
            var cameraObject = new GameObject("Continue Camera");
            try
            {
                playerObject.AddComponent<Rigidbody2D>();
                playerObject.AddComponent<BoxCollider2D>();
                var player = playerObject.AddComponent<JumpPlayerController>();
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.transform.position = new Vector3(0f, 0f, -10f);
                player.Construct(camera);
                typeof(JumpPlayerController)
                    .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(player, null);
                player.Kill();
                player.BlockPendingRespawn();

                var session = new GameplaySession(
                    player,
                    new StubSettings(canContinue: true, freePlay: true, continueCost: 0, additionalLives: 3),
                    new RecordingSceneFlowService());
                TestObjectFactory.SetPrivateField(session, "<State>k__BackingField", GameplaySessionState.GameOver);

                var continued = session.TryContinueGame();

                Assert.That(continued, Is.True);
                Assert.That(session.RemainingLives, Is.EqualTo(3));
                Assert.That(session.State, Is.EqualTo(GameplaySessionState.Running));
                Assert.That(player.IsDead, Is.False);

                session.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void FinalDeath_SchedulesDelayedMenuReturnSoGameOverPanelCanBeShown()
        {
            var sceneFlow = new RecordingSceneFlowService();
            var session = new GameplaySession(null, new StubSettings(canContinue: false, freePlay: false, continueCost: 0, additionalLives: 1), sceneFlow);
            TestObjectFactory.SetPrivateField(session, "<RemainingLives>k__BackingField", 1);

            typeof(GameplaySession)
                .GetMethod("HandlePlayerDied", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(session, new object[] { null });

            Assert.That(session.State, Is.EqualTo(GameplaySessionState.GameOver));
            Assert.That(sceneFlow.LastGameOverReturnDelay, Is.EqualTo(TimeSpan.FromSeconds(8)).Within(TimeSpan.FromMilliseconds(1)));
        }

        [Test]
        public void FinalDeath_WithPrizeFlowResult_DoesNotScheduleAutomaticMenuReturn()
        {
            var sceneFlow = new RecordingSceneFlowService();
            var prizeFlow = new StubPrizeFlow(PrizeRewardKind.Big);
            var session = new GameplaySession(
                null,
                new StubSettings(canContinue: false, freePlay: false, continueCost: 0, additionalLives: 1),
                sceneFlow,
                prizeFlow);
            TestObjectFactory.SetPrivateField(session, "<RemainingLives>k__BackingField", 1);

            typeof(GameplaySession)
                .GetMethod("HandlePlayerDied", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(session, new object[] { null });

            Assert.That(session.State, Is.EqualTo(GameplaySessionState.GameOver));
            Assert.That(prizeFlow.StartCalls, Is.EqualTo(1));
            Assert.That(sceneFlow.LastGameOverReturnDelay, Is.Null);
        }

        private static GameplaySession CreateGameOverSession(
            IReadOnlyServiceSettings settings,
            StubWallet wallet,
            out RecordingSceneFlowService sceneFlow,
            out StubWallet returnedWallet)
        {
            sceneFlow = new RecordingSceneFlowService();
            returnedWallet = wallet;
            var session = new GameplaySession(null, settings, sceneFlow, null, wallet);
            TestObjectFactory.SetPrivateField(session, "<State>k__BackingField", GameplaySessionState.GameOver);
            return session;
        }

        private sealed class StubSettings : IReadOnlyServiceSettings
        {
            private readonly bool canContinue;
            private readonly bool freePlay;
            private readonly int continueCost;
            private readonly int additionalLives;

            public StubSettings(bool canContinue, bool freePlay, int continueCost, int additionalLives)
            {
                this.canContinue = canContinue;
                this.freePlay = freePlay;
                this.continueCost = continueCost;
                this.additionalLives = additionalLives;
            }

            public ServiceSettingsDatabase Database => null;
            public event Action SettingsLoaded { add { } remove { } }
            public event Action SettingsSaved { add { } remove { } }
            public event Action<string, ServiceSettingValue> SettingChanged { add { } remove { } }
            public bool TryGetDefinition(string key, out ServiceSettingDefinition definition) { definition = null; return false; }
            public IReadOnlyDictionary<string, ServiceSettingValue> Snapshot() => new Dictionary<string, ServiceSettingValue>();
            public ServiceSettingValue Get(string key) => default;

            public bool GetBool(string key)
            {
                return key == ServiceSettingsKeys.CanContinueGame && canContinue ||
                    key == ServiceSettingsKeys.FreePlay && freePlay;
            }

            public int GetInt(string key)
            {
                return key switch
                {
                    ServiceSettingsKeys.ContinueGameCost => continueCost,
                    ServiceSettingsKeys.AdditionalLives => additionalLives,
                    _ => 0,
                };
            }

            public float GetFloat(string key) => 0f;
            public string GetString(string key) => string.Empty;
            public int GetOptionIndex(string key) => 0;
        }

        private sealed class StubWallet : IComCreditWallet
        {
            private readonly UnityEvent<int> creditAdded = new();
            private readonly UnityEvent<int> creditRemoved = new();
            private readonly UnityEvent<int> creditsChanged = new();

            public StubWallet(int balance)
            {
                Credits = balance;
            }

            public int Credits { get; private set; }
            public int SpendCalls { get; private set; }
            public UnityEvent<int> CreditAdded => creditAdded;
            public UnityEvent<int> CreditRemoved => creditRemoved;
            public UnityEvent<int> CreditsChanged => creditsChanged;
            public void AddCredit(int amount = 1) { Credits += amount; creditsChanged.Invoke(Credits); }

            public bool TrySpendCredits(int amount = 1)
            {
                if (Credits < amount)
                {
                    return false;
                }

                SpendCalls++;
                Credits -= amount;
                creditRemoved.Invoke(Credits);
                creditsChanged.Invoke(Credits);
                return true;
            }

            public void SetCredits(int value)
            {
                Credits = Mathf.Max(0, value);
                creditsChanged.Invoke(Credits);
            }
        }

        private sealed class RecordingSceneFlowService : ISceneFlowService
        {
            public int CancelGameOverReturnCalls { get; private set; }
            public TimeSpan? LastGameOverReturnDelay { get; private set; }
            public void LoadScene(string sceneName) { }
            public void OpenMainMenu() { }
            public void StartGameplay(string sceneName) { }
            public void OpenSettingsScene(string sceneName) { }
            public void ReturnToMainMenuAfterGameOver(TimeSpan delay) => LastGameOverReturnDelay = delay;
            public void CancelGameOverReturn() => CancelGameOverReturnCalls++;
            public bool SceneExists(string sceneName) => true;
        }

        private sealed class StubPrizeFlow : IPrizeFlowService
        {
            private readonly PrizeRewardKind rewardKind;

            public StubPrizeFlow(PrizeRewardKind rewardKind)
            {
                this.rewardKind = rewardKind;
            }

            public event Action<PrizeFlowResult> PrizeFlowStarted { add { } remove { } }
            public PrizeFlowResult LastResult { get; private set; }
            public int StartCalls { get; private set; }

            public PrizeFlowResult EvaluateCompletedSession(GameplaySession session)
            {
                return new PrizeFlowResult(rewardKind, session?.Score ?? 0, session?.Coins ?? 0, session?.MaxRunHeight ?? 0f);
            }

            public PrizeFlowResult StartPrizeFlow(GameplaySession session)
            {
                StartCalls++;
                LastResult = EvaluateCompletedSession(session);
                return LastResult;
            }
        }
    }
}
