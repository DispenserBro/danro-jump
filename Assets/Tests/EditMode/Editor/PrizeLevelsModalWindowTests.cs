using System;
using System.Collections.Generic;
using DanroJump.Prizes;
using DanroJump.Settings;
using DanroJump.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class PrizeLevelsModalWindowTests
    {
        [Test]
        public void AddOrUpdateLevel_SavesPrizeLevelsJson()
        {
            var settings = new StubSettingsService();
            var window = new PrizeLevelsModalWindow(CreateListRoot(), settings);

            window.AddOrUpdateLevel(120, PrizeRewardKind.Small);
            window.AddOrUpdateLevel(240, PrizeRewardKind.Big);

            var table = PrizeLevelTable.FromJson(settings.GetString(ServiceSettingsKeys.PrizeLevels));
            Assert.That(settings.SaveCalls, Is.EqualTo(2));
            Assert.That(table.Count, Is.EqualTo(2));
            Assert.That(table.ResolveRewardKind(250), Is.EqualTo(PrizeRewardKind.Big));
        }

        [Test]
        public void AddOrUpdateLevel_ReplacesExistingScore()
        {
            var settings = new StubSettingsService();
            var window = new PrizeLevelsModalWindow(CreateListRoot(), settings);

            window.AddOrUpdateLevel(100, PrizeRewardKind.Small);
            window.AddOrUpdateLevel(100, PrizeRewardKind.None);

            var table = PrizeLevelTable.FromJson(settings.GetString(ServiceSettingsKeys.PrizeLevels));
            Assert.That(table.Count, Is.EqualTo(1));
            Assert.That(table.ResolveRewardKind(100), Is.EqualTo(PrizeRewardKind.None));
        }

        [Test]
        public void Open_BuildsRowsForConfiguredLevels()
        {
            var table = new PrizeLevelTable();
            table.Upsert(100, PrizeRewardKind.Small);
            table.Upsert(200, PrizeRewardKind.Big);
            var settings = new StubSettingsService(table.ToJson());
            var root = CreateListRoot();
            var window = new PrizeLevelsModalWindow(root, settings);

            window.Open();

            var rowCount = 0;
            root.Query<VisualElement>(className: "prize-level-row").ForEach(_ => rowCount++);
            Assert.That(rowCount, Is.EqualTo(2));
            Assert.That(root.Q<Label>("PrizeLevelsStatus").text, Does.Contain("Настроено уровней: 2"));
        }

        [Test]
        public void JoystickSubmit_OnAddButtonRaisesAddRequested()
        {
            var settings = new StubSettingsService();
            var root = CreateListRoot();
            var window = new PrizeLevelsModalWindow(root, settings);
            var calls = 0;
            window.AddRequested += () => calls++;

            window.HandleNavigationKey(root.Q<Button>("PrizeLevelsAddButton"), KeyCode.JoystickButton0);

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void KeyboardSubmit_OnDeleteButtonRemovesLevel()
        {
            var table = new PrizeLevelTable();
            table.Upsert(100, PrizeRewardKind.QRCode);
            table.Upsert(200, PrizeRewardKind.Hopper);
            var settings = new StubSettingsService(table.ToJson());
            var root = CreateListRoot();
            var window = new PrizeLevelsModalWindow(root, settings);

            window.Open();
            var deleteButton = root.Q<Button>(className: "prize-level-delete-button");
            Assert.That(deleteButton, Is.Not.Null);

            Assert.That(window.HandleNavigationKey(deleteButton, KeyCode.Return), Is.True);

            var savedTable = PrizeLevelTable.FromJson(settings.GetString(ServiceSettingsKeys.PrizeLevels));
            Assert.That(savedTable.Count, Is.EqualTo(1));
            Assert.That(savedTable.ResolveRewardKind(250), Is.EqualTo(PrizeRewardKind.Hopper));
        }

        [Test]
        public void HorizontalKeyboardInput_IsHandledForArcadeFocusNavigation()
        {
            var settings = new StubSettingsService();
            var root = CreateListRoot();
            var window = new PrizeLevelsModalWindow(root, settings);

            Assert.That(window.HandleNavigationKey(root.Q<Button>("PrizeLevelsAddButton"), KeyCode.RightArrow), Is.True);
        }

        private static VisualElement CreateListRoot()
        {
            var root = new VisualElement { name = "PrizeLevelsModal" };
            root.Add(new ScrollView { name = "PrizeLevelsList" });
            root.Add(new Label { name = "PrizeLevelsStatus" });
            root.Add(new Button { name = "PrizeLevelsAddButton" });
            root.Add(new Button { name = "PrizeLevelsCloseButton" });
            return root;
        }

        private sealed class StubSettingsService : IServiceSettingsService
        {
            private string prizeLevelsJson;

            public StubSettingsService(string prizeLevelsJson = "{\"levels\":[]}")
            {
                this.prizeLevelsJson = prizeLevelsJson;
            }

            public int SaveCalls { get; private set; }
            public ServiceSettingsDatabase Database => null;
            public event Action SettingsLoaded { add { } remove { } }
            public event Action SettingsSaved { add { } remove { } }
            public event Action<string, ServiceSettingValue> SettingChanged { add { } remove { } }
            public void Load() { }
            public void Save() => SaveCalls++;
            public void ReloadExternalChanges() { }
            public void ApplyAll() { }
            public void ResetAllToDefaults() { }
            public void ResetGroupToDefaults(string groupName) { }
            public bool Set(string key, ServiceSettingValue value)
            {
                if (key != ServiceSettingsKeys.PrizeLevels || value.type != ServiceSettingValueType.String)
                {
                    return false;
                }

                prizeLevelsJson = value.stringValue;
                return true;
            }

            public bool SetExternal(string key, ServiceSettingValue value) => Set(key, value);
            public bool SetBool(string key, bool value) => false;
            public bool SetInt(string key, int value) => false;
            public bool SetFloat(string key, float value) => false;
            public bool SetString(string key, string value) => Set(key, ServiceSettingValue.String(value));
            public bool SetOptionIndex(string key, int optionIndex) => false;
            public bool TryGetDefinition(string key, out ServiceSettingDefinition definition)
            {
                definition = null;
                return key == ServiceSettingsKeys.PrizeLevels;
            }

            public IReadOnlyDictionary<string, ServiceSettingValue> Snapshot()
            {
                return new Dictionary<string, ServiceSettingValue>
                {
                    [ServiceSettingsKeys.PrizeLevels] = ServiceSettingValue.String(prizeLevelsJson)
                };
            }

            public ServiceSettingValue Get(string key)
            {
                return key == ServiceSettingsKeys.PrizeLevels
                    ? ServiceSettingValue.String(prizeLevelsJson)
                    : default;
            }

            public bool GetBool(string key) => false;
            public int GetInt(string key) => 0;
            public float GetFloat(string key) => 0f;
            public string GetString(string key) => key == ServiceSettingsKeys.PrizeLevels ? prizeLevelsJson : string.Empty;
            public int GetOptionIndex(string key) => 0;
        }
    }
}
