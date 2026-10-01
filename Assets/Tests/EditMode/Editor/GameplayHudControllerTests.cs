using System;
using System.Collections.Generic;
using System.Reflection;
using DanroJump.Gameplay;
using DanroJump.Hardware.Com;
using DanroJump.Prizes;
using DanroJump.Settings;
using DanroJump.UI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class GameplayHudControllerTests
    {
        [Test]
        public void RefreshContinueOverlay_OpensGameOverPanelWhenContinueIsUnavailable()
        {
            var gameObject = new GameObject("GameplayHudControllerTest");
            try
            {
                var controller = gameObject.AddComponent<GameplayHudController>();
                var session = new GameplaySession(null, new StubSettings(canContinue: true));
                TestObjectFactory.SetPrivateField(session, "<State>k__BackingField", GameplaySessionState.GameOver);

                var overlayRoot = CreateContinueOverlayRoot();
                var overlay = new ContinueGameOverlayWindow(overlayRoot);

                TestObjectFactory.SetPrivateField(controller, "session", session);
                TestObjectFactory.SetPrivateField(controller, "continueGameOverlay", overlay);

                typeof(GameplayHudController)
                    .GetMethod("RefreshContinueOverlay", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(controller, null);

                Assert.That(overlay.IsOpen, Is.True);
                Assert.That(overlayRoot.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(overlayRoot.Q<Label>("ContinueGameTitle").text, Is.EqualTo("Игра окончена"));
                Assert.That(overlayRoot.Q<Button>("ContinueGameButton").enabledSelf, Is.False);

                session.Dispose();
                overlay.Dispose();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static VisualElement CreateContinueOverlayRoot()
        {
            var root = new VisualElement { name = "ContinueGameOverlay" };
            root.style.display = DisplayStyle.None;
            root.Add(new Label { name = "ContinueGameTitle" });
            root.Add(new Label { name = "ContinueGameDetails" });
            root.Add(new Button { name = "ContinueGameButton" });
            root.Add(new Button { name = "ContinueMenuButton" });
            return root;
        }

        private sealed class StubSettings : IReadOnlyServiceSettings
        {
            private readonly bool canContinue;

            public StubSettings(bool canContinue)
            {
                this.canContinue = canContinue;
            }

            public ServiceSettingsDatabase Database => null;
            public event Action SettingsLoaded { add { } remove { } }
            public event Action SettingsSaved { add { } remove { } }
            public event Action<string, ServiceSettingValue> SettingChanged { add { } remove { } }
            public bool TryGetDefinition(string key, out ServiceSettingDefinition definition) { definition = null; return false; }
            public IReadOnlyDictionary<string, ServiceSettingValue> Snapshot() => new Dictionary<string, ServiceSettingValue>();
            public ServiceSettingValue Get(string key) => default;
            public bool GetBool(string key) => key == ServiceSettingsKeys.CanContinueGame && canContinue;
            public int GetInt(string key) => key == ServiceSettingsKeys.ContinueGameCost ? 1 : 0;
            public float GetFloat(string key) => 0f;
            public string GetString(string key) => string.Empty;
            public int GetOptionIndex(string key) => 0;
        }

        [Test]
        public void HandlePrizeFlowStarted_PhysicalDispense_StartsHopperMotor()
        {
            var gameObject = new GameObject("GameplayHudControllerTest");
            try
            {
                var controller = gameObject.AddComponent<GameplayHudController>();
                var com = new StubComSystem();
                var state = new PrizeSessionStateService();

                controller.Construct(
                    new GameplaySession(null, new StubSettings(canContinue: false)),
                    injectedPrizeFlow: null,
                    injectedPrizeSessionState: state,
                    injectedPrizeQrService: null,
                    injectedSceneFlow: null,
                    injectedServiceSettings: null,
                    injectedAudioService: null,
                    injectedComSystem: com
                );

                var result = new PrizeFlowResult(PrizeRewardKind.Hopper, 100, 2, 50f, "hopper-test-run");
                state.TryBeginSession(result, out _);

                typeof(GameplayHudController)
                    .GetMethod("HandlePrizeFlowStarted", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(controller, new object[] { result });

                Assert.That(com.SentCommands, Contains.Item("301"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RefreshDevelopmentStatsVisibility_RequiresServiceSetting()
        {
            var gameObject = new GameObject("GameplayHudControllerTest");
            try
            {
                var controller = gameObject.AddComponent<GameplayHudController>();
                var statsRoot = new VisualElement { name = "RightStats" };

                TestObjectFactory.SetPrivateField(controller, "developmentStatsRoot", statsRoot);
                TestObjectFactory.SetPrivateField(controller, "enabledStatsInSettings", false);

                InvokeRefreshDevelopmentStatsVisibility(controller);

                Assert.That(statsRoot.style.display.value, Is.EqualTo(DisplayStyle.None));

                TestObjectFactory.SetPrivateField(controller, "enabledStatsInSettings", true);

                InvokeRefreshDevelopmentStatsVisibility(controller);

                Assert.That(statsRoot.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static void InvokeRefreshDevelopmentStatsVisibility(GameplayHudController controller)
        {
            typeof(GameplayHudController)
                .GetMethod("RefreshDevelopmentStatsVisibility", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(controller, null);
        }

        private sealed class StubComSystem : IComSystem
        {
            public List<string> SentCommands = new List<string>();
            public bool IsRunning => true;
            public string MainDeviceId => "main";
            public string PrizeStandDeviceId => "stand";
            public IReadOnlyCollection<string> ConnectedDeviceIds => new string[] { "main", "stand" };
            public UnityEngine.Events.UnityEvent<string> DeviceAuthorized { get; } = new UnityEngine.Events.UnityEvent<string>();
            public UnityEngine.Events.UnityEvent<string> DeviceDisconnected { get; } = new UnityEngine.Events.UnityEvent<string>();

            public void StartSystem() { }
            public void StopSystem(bool sendShutdownCommands) { }
            public bool SendToMain(string command)
            {
                SentCommands.Add(command);
                return true;
            }
            public bool SendToDevice(string deviceId, string command)
            {
                SentCommands.Add($"{deviceId}:{command}");
                return true;
            }
            public void SendToAll(string command) { }
            public string GetDeviceRole(string deviceId) => string.Empty;
            public string GetDeviceMac(string deviceId) => string.Empty;

            public UnityEngine.Events.UnityEvent<int> PrizeBoxOpened { get; } = new UnityEngine.Events.UnityEvent<int>();
            public Cysharp.Threading.Tasks.UniTask<IReadOnlyDictionary<int, PrizeBoxStatus>> GetPrizeBoxStatusesAsync(System.Threading.CancellationToken token)
            {
                IReadOnlyDictionary<int, PrizeBoxStatus> dict = new Dictionary<int, PrizeBoxStatus>();
                return Cysharp.Threading.Tasks.UniTask.FromResult(dict);
            }
            public Cysharp.Threading.Tasks.UniTask<int[]> GetReadyPrizeBoxesAsync(System.Threading.CancellationToken token)
            {
                return Cysharp.Threading.Tasks.UniTask.FromResult(new int[] { 5 });
            }
            public Cysharp.Threading.Tasks.UniTask<PrizeOpenResult> OpenPrizeBoxAsync(int boxNumber, System.Threading.CancellationToken token)
            {
                return Cysharp.Threading.Tasks.UniTask.FromResult(PrizeOpenResult.Success(boxNumber));
            }
            public Cysharp.Threading.Tasks.UniTask<PrizeOpenResult> ForceOpenPrizeBoxAsync(int boxNumber, System.Threading.CancellationToken token)
            {
                return Cysharp.Threading.Tasks.UniTask.FromResult(PrizeOpenResult.Success(boxNumber));
            }
            public bool SetPrizeStandIdleLighting() => true;
            public bool HighlightPrizeBox(int boxNumber) => true;

            public int Credits => 0;
            public UnityEngine.Events.UnityEvent<int> CreditAdded { get; } = new UnityEngine.Events.UnityEvent<int>();
            public UnityEngine.Events.UnityEvent<int> CreditRemoved { get; } = new UnityEngine.Events.UnityEvent<int>();
            public UnityEngine.Events.UnityEvent<int> CreditsChanged { get; } = new UnityEngine.Events.UnityEvent<int>();
            public void AddCredit(int amount) { }
            public bool TrySpendCredits(int amount) => true;
            public void SetCredits(int value) { }

            public string LastMainInputBits => string.Empty;
            public string LastMainAdcValues => string.Empty;
            public UnityEngine.Events.UnityEvent<string> MainInputReceived { get; } = new UnityEngine.Events.UnityEvent<string>();
            public UnityEngine.Events.UnityEvent<string> MainAdcReceived { get; } = new UnityEngine.Events.UnityEvent<string>();
            public UnityEngine.Events.UnityEvent HopperSensorTriggered { get; } = new UnityEngine.Events.UnityEvent();
            public string GetLastInputBits(string deviceId) => string.Empty;
            public string GetLastAdcValues(string deviceId) => string.Empty;

            public void PlayBoardLight(LightMode mode, float cycles = -1f) { }
            public void PlayBoardLightSequence(IReadOnlyList<LightStep> steps, bool loop) { }
            public void PlayDefaultBoardLight() { }
            public void StopBoardLightSequence() { }
            public void StopAllBoardEffects() { }
            public void BlinkHat() { }
            public void BlinkHat(float intervalSeconds) { }
            public void StopHatBlink(bool leaveHatEnabled = true) { }
            public void PauseLighting() { }
            public void ResumeLighting() { }
        }
    }
}
