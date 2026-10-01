using System;
using System.Collections.Generic;
using DanroJump.Prizes;
using DanroJump.Settings;
using NUnit.Framework;

namespace DanroJump.Tests.EditMode
{
    public sealed class ServiceSettingsActionRouterTests
    {
        [Test]
        public void StatisticsButton_ReturnsLoadedSettingsSummary()
        {
            var settings = new StubReadOnlySettings(
                new Dictionary<string, ServiceSettingValue>
                {
                    [ServiceSettingsKeys.FreePlay] = ServiceSettingValue.Bool(false),
                    [ServiceSettingsKeys.StartGameCost] = ServiceSettingValue.Int(150),
                });
            var definition = TestObjectFactory.CreateSettingDefinition(
                ServiceSettingsKeys.StatisticsButton,
                ServiceSettingValueType.Button,
                ServiceSettingValue.Button());

            var result = ServiceSettingsActionRouter.Execute(definition, settings);

            Assert.That(result.Handled, Is.True);
            Assert.That(result.Title, Is.EqualTo("Статистика"));
            Assert.That(result.Status, Does.Contain("Настроек загружено: 2"));
            Assert.That(result.Status, Does.Contain("Стоимость старта: 150"));
        }

        [Test]
        public void StatisticsButton_IncludesPrizeSessionSummaryWhenAvailable()
        {
            var settings = new StubReadOnlySettings(
                new Dictionary<string, ServiceSettingValue>
                {
                    [ServiceSettingsKeys.FreePlay] = ServiceSettingValue.Bool(true),
                    [ServiceSettingsKeys.StartGameCost] = ServiceSettingValue.Int(150),
                });
            var prizeSessions = new PrizeSessionStateService();
            prizeSessions.TryBeginSession(
                new PrizeFlowResult(PrizeRewardKind.QRCode, 250, 3, 70f, "run-stat"),
                out _);
            prizeSessions.MarkActiveSessionShown();
            var definition = TestObjectFactory.CreateSettingDefinition(
                ServiceSettingsKeys.StatisticsButton,
                ServiceSettingValueType.Button,
                ServiceSettingValue.Button());

            var result = ServiceSettingsActionRouter.Execute(definition, settings, prizeSessions);

            Assert.That(result.Status, Does.Contain("Призовые сессии"));
            Assert.That(result.Status, Does.Contain("начато 1"));
            Assert.That(result.Status, Does.Contain("QR"));
            Assert.That(result.Status, Does.Contain("показана"));
        }

        [Test]
        public void StatisticsButton_ReportsUnavailablePrizeHistoryWithoutStateService()
        {
            var definition = TestObjectFactory.CreateSettingDefinition(
                ServiceSettingsKeys.StatisticsButton,
                ServiceSettingValueType.Button,
                ServiceSettingValue.Button());

            var result = ServiceSettingsActionRouter.Execute(definition, new StubReadOnlySettings());

            Assert.That(result.Status, Does.Contain("Призовая история недоступна"));
        }

        [Test]
        public void QrSettingsButton_ReturnsQrStateSummary()
        {
            var settings = new StubReadOnlySettings(
                new Dictionary<string, ServiceSettingValue>
                {
                    [ServiceSettingsKeys.ShowPrizeError] = ServiceSettingValue.Bool(true),
                    [ServiceSettingsKeys.PrizePhoneNumber] = ServiceSettingValue.String("+79990000000"),
                    [ServiceSettingsKeys.QrPrizeTextBronze] = ServiceSettingValue.String("Бронза"),
                    [ServiceSettingsKeys.QrPrizeTextSilver] = ServiceSettingValue.String("Серебро"),
                    [ServiceSettingsKeys.QrPrizeTextGold] = ServiceSettingValue.String("Золото"),
                });
            var definition = TestObjectFactory.CreateSettingDefinition(
                ServiceSettingsKeys.QrSettingsButton,
                ServiceSettingValueType.Button,
                ServiceSettingValue.Button());

            var result = ServiceSettingsActionRouter.Execute(definition, settings);

            Assert.That(result.Handled, Is.True);
            Assert.That(result.Title, Is.EqualTo("Настройки QR"));
            Assert.That(result.FocusKey, Is.EqualTo(ServiceSettingsKeys.QrPrizeTextSettings));
            Assert.That(result.Status, Does.Contain("Ошибки QR: показывать"));
            Assert.That(result.Status, Does.Contain("+79990000000"));
            Assert.That(result.Status, Does.Contain("Бронзовый: Бронза"));
            Assert.That(result.Status, Does.Contain("Серебряный: Серебро"));
            Assert.That(result.Status, Does.Contain("Золотой: Золото"));
        }

        [Test]
        public void LevelsPrizesButton_ReturnsPrizeLevelsFocusKey()
        {
            var settings = new StubReadOnlySettings(
                new Dictionary<string, ServiceSettingValue>
                {
                    [ServiceSettingsKeys.PrizeLevels] = ServiceSettingValue.String("{\"levels\":[{\"score\":100,\"rewardKind\":1}]}"),
                });
            var definition = TestObjectFactory.CreateSettingDefinition(
                ServiceSettingsKeys.LevelsPrizesButton,
                ServiceSettingValueType.Button,
                ServiceSettingValue.Button());

            var result = ServiceSettingsActionRouter.Execute(definition, settings);

            Assert.That(result.Handled, Is.True);
            Assert.That(result.FocusKey, Is.EqualTo(ServiceSettingsKeys.PrizeLevels));
            Assert.That(result.Status, Does.Contain("Настроено уровней: 1"));
        }

        [Test]
        public void LevelsPrizesButton_EmptyTableReportsLegacyFallback()
        {
            var settings = new StubReadOnlySettings(
                new Dictionary<string, ServiceSettingValue>
                {
                    [ServiceSettingsKeys.PrizeLevels] = ServiceSettingValue.String("{\"levels\":[]}"),
                    [ServiceSettingsKeys.GiveSmallPrize] = ServiceSettingValue.Bool(true),
                    [ServiceSettingsKeys.GiveBigPrize] = ServiceSettingValue.Bool(false),
                });
            var definition = TestObjectFactory.CreateSettingDefinition(
                ServiceSettingsKeys.LevelsPrizesButton,
                ServiceSettingValueType.Button,
                ServiceSettingValue.Button());

            var result = ServiceSettingsActionRouter.Execute(definition, settings);

            Assert.That(result.Status, Does.Contain("Таблица уровней пустая"));
            Assert.That(result.Status, Does.Contain("малый приз включён"));
        }

        [Test]
        public void HopperTestButton_ReturnsHopperTestFocusKey()
        {
            var definition = TestObjectFactory.CreateSettingDefinition(
                ServiceSettingsKeys.HopperTestButton,
                ServiceSettingValueType.Button,
                ServiceSettingValue.Button());

            var result = ServiceSettingsActionRouter.Execute(definition, new StubReadOnlySettings());

            Assert.That(result.Handled, Is.True);
            Assert.That(result.Title, Is.EqualTo("Тест выдачи"));
            Assert.That(result.FocusKey, Is.EqualTo(ServiceSettingsKeys.HopperTestButton));
            Assert.That(result.Status, Does.Contain("тест хоппера"));
        }


        [Test]
        public void UnknownButton_ReturnsSafeFallback()
        {
            var definition = TestObjectFactory.CreateSettingDefinition(
                "btn.unknown",
                ServiceSettingValueType.Button,
                ServiceSettingValue.Button());

            var result = ServiceSettingsActionRouter.Execute(definition, new StubReadOnlySettings());

            Assert.That(result.Handled, Is.False);
            Assert.That(result.Status, Does.Contain("пока нет маршрута"));
        }

        private sealed class StubReadOnlySettings : IReadOnlyServiceSettings
        {
            private readonly Dictionary<string, ServiceSettingValue> values;

            public StubReadOnlySettings(Dictionary<string, ServiceSettingValue> values = null)
            {
                this.values = values ?? new Dictionary<string, ServiceSettingValue>();
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
                return values;
            }

            public ServiceSettingValue Get(string key)
            {
                return values.TryGetValue(key, out var value) ? value : default;
            }

            public bool GetBool(string key)
            {
                return values.TryGetValue(key, out var value) && value.type == ServiceSettingValueType.Bool && value.boolValue;
            }

            public int GetInt(string key)
            {
                return values.TryGetValue(key, out var value) && value.type == ServiceSettingValueType.Int ? value.intValue : 0;
            }

            public float GetFloat(string key)
            {
                return values.TryGetValue(key, out var value) && value.type == ServiceSettingValueType.Float ? value.floatValue : 0f;
            }

            public string GetString(string key)
            {
                return values.TryGetValue(key, out var value) && value.type == ServiceSettingValueType.String
                    ? value.stringValue
                    : string.Empty;
            }

            public int GetOptionIndex(string key)
            {
                return values.TryGetValue(key, out var value) && value.type == ServiceSettingValueType.Option ? value.intValue : 0;
            }
        }
    }
}
