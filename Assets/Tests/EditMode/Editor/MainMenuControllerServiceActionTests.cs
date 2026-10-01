using System;
using System.Collections.Generic;
using System.Reflection;
using DanroJump.Prizes.Qr;
using DanroJump.Settings;
using DanroJump.UI.ArcadeInput;
using DanroJump.UI.MainMenu;
using DanroJump.UI.Modals;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class MainMenuControllerServiceActionTests
    {
        private GameObject gameObject;

        [TearDown]
        public void TearDown()
        {
            if (gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void QrSettingsAction_OpensQrTierSettingsModal()
        {
            var controller = CreateController();
            var settings = new StubSettingsService();
            var qrSettingsRoot = CreateQrSettingsRoot();
            var qrSettingsModal = new QrCodeSettingsModalWindow(qrSettingsRoot, settings);

            TestObjectFactory.SetPrivateField(controller, "serviceSettingsService", settings);
            TestObjectFactory.SetPrivateField(controller, "qrCodeSettingsModal", qrSettingsModal);
            TestObjectFactory.SetPrivateField(controller, "modalStack", new ModalStackController(new VisualElement(), null));

            InvokeServiceAction(
                controller,
                ServiceSettingsActionResult.Success(
                    "Настройки QR",
                    "Открыты настройки QR.",
                    ServiceSettingsKeys.QrPrizeTextSettings));

            Assert.That(qrSettingsModal.IsOpen, Is.True);
            Assert.That(qrSettingsRoot.Q<Label>("QrSettingsTierValue").text, Is.EqualTo("Бронзовый"));
            Assert.That(qrSettingsRoot.Q<Label>("QrSettingsPrizeTextValue").text, Is.EqualTo("Бронза"));

            qrSettingsModal.Dispose();
        }

        [Test]
        public void OpenQrPrizeTextInput_SavesSelectedTierPrizeTextWithoutTechnicalTemplate()
        {
            var controller = CreateController();
            var settings = new StubSettingsService();
            var qrSettingsModal = new QrCodeSettingsModalWindow(CreateQrSettingsRoot(), settings);
            var textInputRoot = CreateTextInputRoot();
            var textInputModal = new ServiceTextInputModalWindow(textInputRoot);

            TestObjectFactory.SetPrivateField(controller, "serviceSettingsService", settings);
            TestObjectFactory.SetPrivateField(controller, "serviceTextInputModal", textInputModal);
            TestObjectFactory.SetPrivateField(controller, "qrCodeSettingsModal", qrSettingsModal);
            TestObjectFactory.SetPrivateField(controller, "modalStack", new ModalStackController(new VisualElement(), null));

            InvokeOpenQrPrizeTextInput(controller, QrPrizeTier.Silver, ServiceSettingsKeys.QrPrizeTextSilver);

            Assert.That(textInputModal.IsOpen, Is.True);
            Assert.That(textInputRoot.Q<Label>("ServiceTextInputSetting").text, Is.EqualTo("Серебряный QR"));
            Assert.That(textInputRoot.Q<Label>("ServiceTextInputPreview").text, Is.EqualTo("Серебро"));
            Assert.That(textInputRoot.Q<Label>("ServiceTextInputPreview").text, Does.Not.Contain("{score}"));

            Assert.That(textInputModal.Confirm(), Is.True);

            Assert.That(settings.LastSetStringKey, Is.EqualTo(ServiceSettingsKeys.QrPrizeTextSilver));
            Assert.That(settings.LastSetStringValue, Is.EqualTo("Серебро"));
            Assert.That(settings.SaveCalls, Is.EqualTo(1));

            qrSettingsModal.Dispose();
            textInputModal.Dispose();
        }

        [Test]
        public void OpenSettings_RequiresPinCodeInput()
        {
            var controller = CreateController();
            var settings = new StubSettingsService();
            var settingsModal = new SettingsModalWindow(new VisualElement(), settings, null);
            var textInputRoot = CreateTextInputRoot();
            var textInputModal = new ServiceTextInputModalWindow(textInputRoot);

            TestObjectFactory.SetPrivateField(controller, "serviceSettingsService", settings);
            TestObjectFactory.SetPrivateField(controller, "serviceTextInputModal", textInputModal);
            TestObjectFactory.SetPrivateField(controller, "settingsModal", settingsModal);
            TestObjectFactory.SetPrivateField(controller, "modalStack", new ModalStackController(new VisualElement(), null));

            InvokeOpenSettings(controller);

            Assert.That(textInputModal.IsOpen, Is.True);
            Assert.That(settingsModal.IsOpen, Is.False);
            Assert.That(textInputRoot.Q<Label>("ServiceTextInputTitle").text, Is.EqualTo("Авторизация"));
            Assert.That(textInputRoot.Q<Label>("ServiceTextInputSetting").text, Is.EqualTo("Введите PIN-код для доступа к настройкам"));

            settingsModal.Dispose();
            textInputModal.Dispose();
        }

        [Test]
        public void OpenSettings_WithCorrectPIN_OpensSettingsModal()
        {
            var controller = CreateController();
            var settings = new StubSettingsService();
            var settingsModal = new SettingsModalWindow(new VisualElement(), settings, null);
            var textInputRoot = CreateTextInputRoot();
            var textInputModal = new ServiceTextInputModalWindow(textInputRoot);

            TestObjectFactory.SetPrivateField(controller, "serviceSettingsService", settings);
            TestObjectFactory.SetPrivateField(controller, "serviceTextInputModal", textInputModal);
            TestObjectFactory.SetPrivateField(controller, "settingsModal", settingsModal);
            TestObjectFactory.SetPrivateField(controller, "modalStack", new ModalStackController(new VisualElement(), null));

            InvokeOpenSettings(controller);

            var profile = new ArcadeStringInputProfile("0123456789", 4);
            var correctState = new ArcadeStringInputState(profile, "1234");
            TestObjectFactory.SetPrivateField(textInputModal, "state", correctState);

            Assert.That(textInputModal.Confirm(), Is.True);

            Assert.That(settingsModal.IsOpen, Is.True);

            settingsModal.Dispose();
            textInputModal.Dispose();
        }

        [Test]
        public void OpenSettings_WithIncorrectPIN_DoesNotOpenSettingsModalAndPlaysError()
        {
            var controller = CreateController();
            var settings = new StubSettingsService();
            var settingsModal = new SettingsModalWindow(new VisualElement(), settings, null);
            var textInputRoot = CreateTextInputRoot();
            var textInputModal = new ServiceTextInputModalWindow(textInputRoot);
            var stubAudio = new StubAudioService();

            TestObjectFactory.SetPrivateField(controller, "serviceSettingsService", settings);
            TestObjectFactory.SetPrivateField(controller, "serviceTextInputModal", textInputModal);
            TestObjectFactory.SetPrivateField(controller, "settingsModal", settingsModal);
            TestObjectFactory.SetPrivateField(controller, "audioService", stubAudio);
            TestObjectFactory.SetPrivateField(controller, "modalStack", new ModalStackController(new VisualElement(), null));

            InvokeOpenSettings(controller);

            var profile = new ArcadeStringInputProfile("0123456789", 4);
            var incorrectState = new ArcadeStringInputState(profile, "9999");
            TestObjectFactory.SetPrivateField(textInputModal, "state", incorrectState);

            Assert.That(textInputModal.Confirm(), Is.True);

            Assert.That(settingsModal.IsOpen, Is.False);
            Assert.That(stubAudio.LastPlayEvent, Is.EqualTo(DanroJump.Audio.GameAudioEvent.UiError));

            settingsModal.Dispose();
            textInputModal.Dispose();
        }

        [Test]
        public void OpenSettings_WithPinDisabled_OpensSettingsModalDirectly()
        {
            var controller = CreateController();
            var settings = new StubSettingsService();
            var settingsModal = new SettingsModalWindow(new VisualElement(), settings, null);
            var textInputRoot = CreateTextInputRoot();
            var textInputModal = new ServiceTextInputModalWindow(textInputRoot);

            settings.SetBool(ServiceSettingsKeys.RequireSettingsPin, false);

            TestObjectFactory.SetPrivateField(controller, "serviceSettingsService", settings);
            TestObjectFactory.SetPrivateField(controller, "serviceTextInputModal", textInputModal);
            TestObjectFactory.SetPrivateField(controller, "settingsModal", settingsModal);
            TestObjectFactory.SetPrivateField(controller, "modalStack", new ModalStackController(new VisualElement(), null));

            InvokeOpenSettings(controller);

            Assert.That(textInputModal.IsOpen, Is.False);
            Assert.That(settingsModal.IsOpen, Is.True);

            settingsModal.Dispose();
            textInputModal.Dispose();
        }

        [Test]
        public void SettingsPinButtonAction_OpensTextInputModalForSettingsPin()
        {
            var controller = CreateController();
            var settings = new StubSettingsService();
            var textInputRoot = CreateTextInputRoot();
            var textInputModal = new ServiceTextInputModalWindow(textInputRoot);

            TestObjectFactory.SetPrivateField(controller, "serviceSettingsService", settings);
            TestObjectFactory.SetPrivateField(controller, "serviceTextInputModal", textInputModal);
            TestObjectFactory.SetPrivateField(controller, "modalStack", new ModalStackController(new VisualElement(), null));

            var definition = TestObjectFactory.CreateSettingDefinition(
                ServiceSettingsKeys.SettingsPinButton,
                ServiceSettingValueType.Button,
                ServiceSettingValue.Button());

            var result = ServiceSettingsActionRouter.Execute(definition, settings, null);

            InvokeServiceAction(controller, result);

            Assert.That(textInputModal.IsOpen, Is.True);
            Assert.That(textInputRoot.Q<Label>("ServiceTextInputTitle").text, Is.EqualTo("PIN-код"));
            Assert.That(textInputRoot.Q<Label>("ServiceTextInputSetting").text, Is.EqualTo("PIN-код для доступа к меню настроек (4 цифры)"));

            textInputModal.Dispose();
        }

        private MainMenuController CreateController()
        {
            gameObject = new GameObject("MainMenuControllerServiceActionTest");
            return gameObject.AddComponent<MainMenuController>();
        }

        private static void InvokeServiceAction(
            MainMenuController controller,
            ServiceSettingsActionResult result)
        {
            var method = typeof(MainMenuController).GetMethod(
                "HandleServiceActionActivated",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, new object[] { null, result });
        }

        private static void InvokeOpenQrPrizeTextInput(
            MainMenuController controller,
            QrPrizeTier tier,
            string settingKey)
        {
            var method = typeof(MainMenuController).GetMethod(
                "OpenQrPrizeTextInput",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, new object[] { tier, settingKey });
        }

        private static void InvokeOpenSettings(MainMenuController controller)
        {
            var method = typeof(MainMenuController).GetMethod(
                "OpenSettings",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, null);
        }

        private static VisualElement CreateQrSettingsRoot()
        {
            var root = new VisualElement { name = "QrSettingsModal" };
            root.Add(new Label { name = "QrSettingsTierValue" });
            root.Add(new Label { name = "QrSettingsPrizeTextValue" });
            root.Add(new Label { name = "QrSettingsStatus" });
            root.Add(new Button { name = "QrSettingsTierPreviousButton" });
            root.Add(new Button { name = "QrSettingsTierNextButton" });
            root.Add(new Button { name = "QrSettingsEditTextButton" });
            root.Add(new Button { name = "QrSettingsCloseButton" });
            return root;
        }

        private static VisualElement CreateTextInputRoot()
        {
            var root = new VisualElement { name = "ServiceTextInputModal" };
            root.Add(new Label { name = "ServiceTextInputTitle" });
            root.Add(new Label { name = "ServiceTextInputSetting" });
            root.Add(new Label { name = "ServiceTextInputPreview" });
            root.Add(new VisualElement { name = "ServiceTextInputSlots" });
            root.Add(new Label { name = "ServiceTextInputSymbol" });
            root.Add(new Label { name = "ServiceTextInputStatus" });
            root.Add(new Button { name = "ServiceTextInputConfirmButton" });
            root.Add(new Button { name = "ServiceTextInputDeleteButton" });
            root.Add(new Button { name = "ServiceTextInputCancelButton" });
            return root;
        }

        private sealed class StubAudioService : DanroJump.Audio.IGameAudioService
        {
            public bool IsReady => true;
            public bool IsMusicPlaying => false;
            public DanroJump.Audio.GameMusicCue? CurrentMusicCue => null;
            public DanroJump.Audio.GameAudioEvent? LastPlayEvent { get; private set; }

            public void Play(DanroJump.Audio.GameAudioEvent eventId) => LastPlayEvent = eventId;
            public void Play(DanroJump.Audio.GameAudioEvent eventId, Vector3 worldPosition) => LastPlayEvent = eventId;
            public void StartMusic() { }
            public void PlayMusic(DanroJump.Audio.GameMusicCue cue) { }
            public void StopMusic() { }
            public void ApplyServiceVolumes() { }
        }

        private sealed class StubSettingsService : IServiceSettingsService
        {
            private readonly Dictionary<string, ServiceSettingDefinition> definitions = new();
            private readonly Dictionary<string, string> values = new();
            private readonly Dictionary<string, bool> boolValues = new();

            public StubSettingsService()
            {
                AddStringDefinition(ServiceSettingsKeys.QrPrizeTextBronze, "Бронзовый QR", "Бронза");
                AddStringDefinition(ServiceSettingsKeys.QrPrizeTextSilver, "Серебряный QR", "Серебро");
                AddStringDefinition(ServiceSettingsKeys.QrPrizeTextGold, "Золотой QR", "Золото");
                AddStringDefinition(ServiceSettingsKeys.SettingsPin, "PIN-код для доступа к меню настроек (4 цифры)", "1234");
            }

            public string LastSetStringKey { get; private set; }
            public string LastSetStringValue { get; private set; }
            public int SaveCalls { get; private set; }

            public ServiceSettingsDatabase Database => null;
            public event Action SettingsLoaded { add { } remove { } }
            public event Action SettingsSaved { add { } remove { } }
            public event Action<string, ServiceSettingValue> SettingChanged { add { } remove { } }

            public bool TryGetDefinition(string key, out ServiceSettingDefinition definition)
            {
                return definitions.TryGetValue(key, out definition);
            }

            public IReadOnlyDictionary<string, ServiceSettingValue> Snapshot()
            {
                return new Dictionary<string, ServiceSettingValue>();
            }

            public ServiceSettingValue Get(string key)
            {
                return ServiceSettingValue.String(GetString(key));
            }

            public bool GetBool(string key)
            {
                bool val;
                if (string.Equals(key, ServiceSettingsKeys.RequireSettingsPin, StringComparison.Ordinal))
                {
                    return !boolValues.TryGetValue(key, out val) || val;
                }
                return boolValues.TryGetValue(key, out val) && val;
            }
            public int GetInt(string key) => 0;
            public float GetFloat(string key) => 0f;

            public string GetString(string key)
            {
                string valStr;
                if (string.Equals(key, ServiceSettingsKeys.SettingsPin, StringComparison.Ordinal))
                {
                    return values.TryGetValue(key, out valStr) ? valStr : "1234";
                }
                return values.TryGetValue(key, out valStr) ? valStr : string.Empty;
            }

            public int GetOptionIndex(string key) => 0;
            public void Load() { }
            public void Save() => SaveCalls++;
            public void ReloadExternalChanges() { }
            public void ApplyAll() { }
            public void ResetAllToDefaults() { }
            public void ResetGroupToDefaults(string groupName) { }
            public bool Set(string key, ServiceSettingValue value) => true;
            public bool SetExternal(string key, ServiceSettingValue value) => true;
            public bool SetBool(string key, bool value)
            {
                boolValues[key] = value;
                return true;
            }
            public bool SetInt(string key, int value) => true;
            public bool SetFloat(string key, float value) => true;

            public bool SetString(string key, string value)
            {
                LastSetStringKey = key;
                LastSetStringValue = value;
                values[key] = value;
                return true;
            }

            public bool SetOptionIndex(string key, int optionIndex) => true;

            private void AddStringDefinition(string key, string displayName, string value)
            {
                values[key] = value;
                var definition = TestObjectFactory.CreateSettingDefinition(
                    key,
                    ServiceSettingValueType.String,
                    ServiceSettingValue.String(value));
                TestObjectFactory.SetPrivateField(definition, "displayName", displayName);
                definitions[key] = definition;
            }
        }
    }
}
