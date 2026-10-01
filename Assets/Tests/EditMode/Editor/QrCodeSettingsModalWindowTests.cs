using System;
using System.Collections.Generic;
using DanroJump.Prizes.Qr;
using DanroJump.Settings;
using DanroJump.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class QrCodeSettingsModalWindowTests
    {
        [Test]
        public void Open_ShowsBronzeTierTextByDefault()
        {
            var root = CreateRoot();
            var window = new QrCodeSettingsModalWindow(root, new StubSettings());

            window.Open();

            Assert.That(root.Q<Label>("QrSettingsTierValue").text, Is.EqualTo("Бронзовый"));
            Assert.That(root.Q<Label>("QrSettingsPrizeTextValue").text, Is.EqualTo("Бронза"));

            window.Dispose();
        }

        [Test]
        public void RightNavigationCyclesTierAndSubmitRequestsSelectedTextEditor()
        {
            var root = CreateRoot();
            var window = new QrCodeSettingsModalWindow(root, new StubSettings());
            QrPrizeTier requestedTier = QrPrizeTier.None;
            string requestedKey = null;
            window.TextEditRequested += (tier, key) =>
            {
                requestedTier = tier;
                requestedKey = key;
            };

            window.Open();
            Assert.That(window.HandleNavigationKey(root.Q<Button>("QrSettingsEditTextButton"), KeyCode.RightArrow), Is.True);
            Assert.That(window.HandleNavigationKey(root.Q<Button>("QrSettingsEditTextButton"), KeyCode.Return), Is.True);

            Assert.That(root.Q<Label>("QrSettingsTierValue").text, Is.EqualTo("Серебряный"));
            Assert.That(root.Q<Label>("QrSettingsPrizeTextValue").text, Is.EqualTo("Серебро"));
            Assert.That(requestedTier, Is.EqualTo(QrPrizeTier.Silver));
            Assert.That(requestedKey, Is.EqualTo(ServiceSettingsKeys.QrPrizeTextSilver));

            window.Dispose();
        }

        private static VisualElement CreateRoot()
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

        private sealed class StubSettings : IServiceSettingsService
        {
            private readonly Dictionary<string, ServiceSettingValue> values = new()
            {
                [ServiceSettingsKeys.QrPrizeTextBronze] = ServiceSettingValue.String("Бронза"),
                [ServiceSettingsKeys.QrPrizeTextSilver] = ServiceSettingValue.String("Серебро"),
                [ServiceSettingsKeys.QrPrizeTextGold] = ServiceSettingValue.String("Золото"),
            };

            public ServiceSettingsDatabase Database => null;
            public event Action SettingsLoaded { add { } remove { } }
            public event Action SettingsSaved { add { } remove { } }
            public event Action<string, ServiceSettingValue> SettingChanged { add { } remove { } }
            public bool TryGetDefinition(string key, out ServiceSettingDefinition definition) { definition = null; return false; }
            public IReadOnlyDictionary<string, ServiceSettingValue> Snapshot() => values;
            public ServiceSettingValue Get(string key) => values.TryGetValue(key, out var value) ? value : default;
            public bool GetBool(string key) => values.TryGetValue(key, out var value) && value.type == ServiceSettingValueType.Bool && value.boolValue;
            public int GetInt(string key) => values.TryGetValue(key, out var value) && value.type == ServiceSettingValueType.Int ? value.intValue : 0;
            public float GetFloat(string key) => values.TryGetValue(key, out var value) && value.type == ServiceSettingValueType.Float ? value.floatValue : 0f;
            public string GetString(string key) => values.TryGetValue(key, out var value) && value.type == ServiceSettingValueType.String ? value.stringValue : string.Empty;
            public int GetOptionIndex(string key) => values.TryGetValue(key, out var value) && value.type == ServiceSettingValueType.Option ? value.intValue : 0;
            public void Load() { }
            public void Save() { }
            public void ReloadExternalChanges() { }
            public void ApplyAll() { }
            public void ResetAllToDefaults() { }
            public void ResetGroupToDefaults(string groupName) { }
            public bool Set(string key, ServiceSettingValue value) => true;
            public bool SetExternal(string key, ServiceSettingValue value) => true;
            public bool SetBool(string key, bool value) => true;
            public bool SetInt(string key, int value) => true;
            public bool SetFloat(string key, float value) => true;
            public bool SetString(string key, string value) => true;
            public bool SetOptionIndex(string key, int optionIndex) => true;
        }
    }
}
