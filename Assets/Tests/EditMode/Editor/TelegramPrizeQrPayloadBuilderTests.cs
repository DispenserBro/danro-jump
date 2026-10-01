using System;
using System.Collections.Generic;
using DanroJump.Prizes;
using DanroJump.Prizes.Qr;
using DanroJump.Settings;
using NUnit.Framework;

namespace DanroJump.Tests.EditMode
{
    public sealed class TelegramPrizeQrPayloadBuilderTests
    {
        [Test]
        public void TryBuild_NormalizesPhoneAndBuildsTelegramUrl()
        {
            var settings = new StubReadOnlySettings(
                new Dictionary<string, ServiceSettingValue>
                {
                    [ServiceSettingsKeys.PrizePhoneNumber] = ServiceSettingValue.String("+7 (999) 999-99-99"),
                    [ServiceSettingsKeys.PrizeLevels] = ServiceSettingValue.String("{\"levels\":[{\"score\":100,\"rewardKind\":1}]}"),
                    [ServiceSettingsKeys.QrPrizeTextBronze] = ServiceSettingValue.String("Бронза")
                });
            var result = new PrizeFlowResult(PrizeRewardKind.QRCode, 125, 3, 42.4f);

            var success = new TelegramPrizeQrPayloadBuilder().TryBuild(result, settings, out var payload, out var error);

            Assert.That(success, Is.True, error);
            Assert.That(payload.PhoneDigits, Is.EqualTo("79999999999"));
            Assert.That(payload.Message, Is.EqualTo("Бронза"));
            Assert.That(payload.Tier, Is.EqualTo(QrPrizeTier.Bronze));
            Assert.That(payload.Url, Is.EqualTo(
                "https://t.me/+79999999999?text=" + Uri.EscapeDataString("Бронза")));
        }

        [Test]
        public void TryBuild_UsesTierTextForResolvedQrLevelAndEncodesMessage()
        {
            var settings = new StubReadOnlySettings(
                new Dictionary<string, ServiceSettingValue>
                {
                    [ServiceSettingsKeys.PrizePhoneNumber] = ServiceSettingValue.String("+79999999999"),
                    [ServiceSettingsKeys.PrizeLevels] = ServiceSettingValue.String("{\"levels\":[{\"score\":50,\"rewardKind\":1},{\"score\":150,\"rewardKind\":1},{\"score\":250,\"rewardKind\":1}]}"),
                    [ServiceSettingsKeys.QrPrizeTextBronze] = ServiceSettingValue.String("Бронза"),
                    [ServiceSettingsKeys.QrPrizeTextSilver] = ServiceSettingValue.String("Серебро"),
                    [ServiceSettingsKeys.QrPrizeTextGold] = ServiceSettingValue.String("Золотой приз")
                });
            var result = new PrizeFlowResult(PrizeRewardKind.QRCode, 170, 9, 19.7f);

            var success = new TelegramPrizeQrPayloadBuilder().TryBuild(result, settings, out var payload, out _);

            Assert.That(success, Is.True);
            Assert.That(payload.Message, Is.EqualTo("Серебро"));
            Assert.That(payload.Tier, Is.EqualTo(QrPrizeTier.Silver));
            Assert.That(payload.Url, Does.StartWith("https://t.me/+79999999999?text="));
            Assert.That(payload.Url, Does.Contain(Uri.EscapeDataString("Серебро")));
            Assert.That(payload.Url, Does.Not.Contain(" "));
        }

        [Test]
        public void FormatMessageTemplate_StillSupportsLegacyPlaceholdersForCompatibility()
        {
            var message = TelegramPrizeQrPayloadBuilder.FormatMessageTemplate(
                "Приз: {prize}, очки: {score}, высота: {height}, телефон: {phone}",
                new PrizeFlowResult(PrizeRewardKind.QRCode, 77, 9, 19.7f),
                "+79999999999",
                "Бронза");

            Assert.That(message, Is.EqualTo("Приз: Бронза, очки: 77, высота: 20, телефон: +79999999999"));
        }

        [TestCase("")]
        [TestCase("abc")]
        public void TryBuild_InvalidPhoneFailsSafely(string phone)
        {
            var settings = new StubReadOnlySettings(
                new Dictionary<string, ServiceSettingValue>
                {
                    [ServiceSettingsKeys.PrizePhoneNumber] = ServiceSettingValue.String(phone)
                });

            var success = new TelegramPrizeQrPayloadBuilder().TryBuild(
                new PrizeFlowResult(PrizeRewardKind.QRCode, 1, 0, 1f),
                settings,
                out var payload,
                out var error);

            Assert.That(success, Is.False);
            Assert.That(payload.Url, Is.Null.Or.Empty);
            Assert.That(error, Does.Contain("номер телефона"));
        }

        private sealed class StubReadOnlySettings : IReadOnlyServiceSettings
        {
            private readonly Dictionary<string, ServiceSettingValue> values;

            public StubReadOnlySettings(Dictionary<string, ServiceSettingValue> values)
            {
                this.values = values;
            }

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
        }
    }
}
