using System;
using System.Collections.Generic;
using System.Reflection;
using DanroJump.Hardware.Com;
using DanroJump.Prizes.Qr;
using DanroJump.SceneFlow;
using DanroJump.Settings;
using DanroJump.UI.ArcadeInput;
using DanroJump.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class MainMenuControllerPaymentTests
    {
        private readonly List<GameObject> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void StartGame_SpendsConfiguredBalanceBeforeLoadingGameplay()
        {
            var controller = CreateController(
                new StubSettings(freePlay: false, startCost: 7),
                new StubWallet(balance: 10),
                out var sceneFlow,
                out var wallet);

            InvokeStartGame(controller);

            Assert.That(wallet.Credits, Is.EqualTo(3));
            Assert.That(wallet.SpendCalls, Is.EqualTo(1));
            Assert.That(sceneFlow.LoadedScene, Is.EqualTo("Game"));
        }

        [Test]
        public void StartGame_DoesNotLoadGameplayWhenBalanceIsInsufficient()
        {
            var controller = CreateController(
                new StubSettings(freePlay: false, startCost: 7),
                new StubWallet(balance: 4),
                out var sceneFlow,
                out var wallet);

            InvokeStartGame(controller);

            Assert.That(wallet.Credits, Is.EqualTo(4));
            Assert.That(wallet.SpendCalls, Is.EqualTo(0));
            Assert.That(sceneFlow.LoadedScene, Is.Null);
        }

        [Test]
        public void StartGame_FreePlayLoadsGameplayWithoutSpendingBalance()
        {
            var controller = CreateController(
                new StubSettings(freePlay: true, startCost: 7),
                new StubWallet(balance: 4),
                out var sceneFlow,
                out var wallet);

            InvokeStartGame(controller);

            Assert.That(wallet.Credits, Is.EqualTo(4));
            Assert.That(wallet.SpendCalls, Is.EqualTo(0));
            Assert.That(sceneFlow.LoadedScene, Is.EqualTo("Game"));
        }

        [Test]
        public void RefreshStaticLabels_UsesConfiguredSupportNumber()
        {
            var controller = CreateController(
                new StubSettings(freePlay: false, startCost: 7, showSupportNumber: true, supportNumber: "+71112223344"),
                new StubWallet(balance: 10),
                out _,
                out _);
            var supportLabel = new Label("тех. поддержка +79999999999");
            TestObjectFactory.SetPrivateField(controller, "supportPhoneLabel", supportLabel);

            InvokeRefreshStaticLabels(controller);

            Assert.That(supportLabel.text, Is.EqualTo("тех. поддержка +71112223344"));
            Assert.That(supportLabel.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void RefreshStaticLabels_HidesSupportNumberWhenDisabled()
        {
            var controller = CreateController(
                new StubSettings(freePlay: false, startCost: 7, showSupportNumber: false, supportNumber: "+71112223344"),
                new StubWallet(balance: 10),
                out _,
                out _);
            var supportLabel = new Label("тех. поддержка +79999999999");
            TestObjectFactory.SetPrivateField(controller, "supportPhoneLabel", supportLabel);

            InvokeRefreshStaticLabels(controller);

            Assert.That(supportLabel.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void ResolveTextInputProfile_UsesShortTextForQrPrizeTexts()
        {
            var method = typeof(MainMenuController).GetMethod(
                "ResolveTextInputProfile",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            var profile = (ArcadeStringInputProfile)method.Invoke(
                null,
                new object[] { ServiceSettingsKeys.QrPrizeTextGold });

            Assert.That(profile.MaxLength, Is.EqualTo(QrPrizeTextSettings.MaxPrizeTextLength));
            Assert.That(profile.AllowedSymbols, Does.Not.Contain("{"));
            Assert.That(profile.AllowedSymbols, Does.Not.Contain("}"));
        }

        private MainMenuController CreateController(
            IReadOnlyServiceSettings settings,
            StubWallet wallet,
            out RecordingSceneFlowService sceneFlow,
            out StubWallet returnedWallet)
        {
            var gameObject = new GameObject("MainMenuControllerPaymentTest");
            createdObjects.Add(gameObject);

            var controller = gameObject.AddComponent<MainMenuController>();
            sceneFlow = new RecordingSceneFlowService();
            returnedWallet = wallet;

            TestObjectFactory.SetPrivateField(controller, "gameplaySceneName", "Game");
            TestObjectFactory.SetPrivateField(controller, "serviceSettings", settings);
            TestObjectFactory.SetPrivateField(controller, "sceneFlow", sceneFlow);
            TestObjectFactory.SetPrivateField(controller, "creditWallet", wallet);
            return controller;
        }

        private static void InvokeStartGame(MainMenuController controller)
        {
            var method = typeof(MainMenuController).GetMethod(
                "StartGame",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, null);
        }

        private static void InvokeRefreshStaticLabels(MainMenuController controller)
        {
            var method = typeof(MainMenuController).GetMethod(
                "RefreshStaticLabels",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, null);
        }

        private sealed class StubSettings : IReadOnlyServiceSettings
        {
            private readonly bool freePlay;
            private readonly int startCost;
            private readonly bool showSupportNumber;
            private readonly string supportNumber;

            public StubSettings(
                bool freePlay,
                int startCost,
                bool showSupportNumber = true,
                string supportNumber = "+79999999999")
            {
                this.freePlay = freePlay;
                this.startCost = startCost;
                this.showSupportNumber = showSupportNumber;
                this.supportNumber = supportNumber;
            }

            public ServiceSettingsDatabase Database => null;
            public event Action SettingsLoaded { add { } remove { } }
            public event Action SettingsSaved { add { } remove { } }
            public event Action<string, ServiceSettingValue> SettingChanged { add { } remove { } }

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
                if (string.Equals(key, ServiceSettingsKeys.FreePlay, StringComparison.Ordinal))
                {
                    return ServiceSettingValue.Bool(freePlay);
                }

                if (string.Equals(key, ServiceSettingsKeys.StartGameCost, StringComparison.Ordinal))
                {
                    return ServiceSettingValue.Int(startCost);
                }

                if (string.Equals(key, ServiceSettingsKeys.ShowSupportNumber, StringComparison.Ordinal))
                {
                    return ServiceSettingValue.Bool(showSupportNumber);
                }

                if (string.Equals(key, ServiceSettingsKeys.SupportNumber, StringComparison.Ordinal))
                {
                    return ServiceSettingValue.String(supportNumber);
                }

                return default;
            }

            public bool GetBool(string key)
            {
                if (string.Equals(key, ServiceSettingsKeys.FreePlay, StringComparison.Ordinal))
                {
                    return freePlay;
                }

                return string.Equals(key, ServiceSettingsKeys.ShowSupportNumber, StringComparison.Ordinal) &&
                    showSupportNumber;
            }

            public int GetInt(string key)
            {
                return string.Equals(key, ServiceSettingsKeys.StartGameCost, StringComparison.Ordinal) ? startCost : 0;
            }

            public float GetFloat(string key) => 0f;
            public string GetString(string key)
            {
                return string.Equals(key, ServiceSettingsKeys.SupportNumber, StringComparison.Ordinal)
                    ? supportNumber
                    : string.Empty;
            }

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

            public void AddCredit(int amount = 1)
            {
                Credits += amount;
                creditAdded.Invoke(Credits);
                creditsChanged.Invoke(Credits);
            }

            public bool TrySpendCredits(int amount = 1)
            {
                if (amount <= 0)
                {
                    return true;
                }

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
            public string LoadedScene { get; private set; }

            public void LoadScene(string sceneName)
            {
                LoadedScene = sceneName;
            }

            public void OpenMainMenu()
            {
            }

            public void StartGameplay(string sceneName)
            {
                LoadedScene = sceneName;
            }

            public void OpenSettingsScene(string sceneName)
            {
                LoadedScene = sceneName;
            }

            public void ReturnToMainMenuAfterGameOver(TimeSpan delay)
            {
            }

            public void CancelGameOverReturn()
            {
            }

            public bool SceneExists(string sceneName)
            {
                return !string.IsNullOrWhiteSpace(sceneName);
            }
        }
    }
}
