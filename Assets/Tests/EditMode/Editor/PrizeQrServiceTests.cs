using System;
using System.Collections.Generic;
using System.Linq;
using DanroJump.Prizes;
using DanroJump.Prizes.Qr;
using DanroJump.Settings;
using NUnit.Framework;
using UnityEngine;

namespace DanroJump.Tests.EditMode
{
    public sealed class PrizeQrServiceTests
    {
        [Test]
        public void TryCreateTicket_CreatesTicketOnlyForQrPrize()
        {
            var service = new PrizeQrService(new StubReadOnlySettings(
                new Dictionary<string, ServiceSettingValue>
                {
                    [ServiceSettingsKeys.PrizePhoneNumber] = ServiceSettingValue.String("+79999999999"),
                    [ServiceSettingsKeys.PrizeLevels] = ServiceSettingValue.String("{\"levels\":[{\"score\":50,\"rewardKind\":1},{\"score\":100,\"rewardKind\":1},{\"score\":150,\"rewardKind\":1}]}"),
                    [ServiceSettingsKeys.QrPrizeTextBronze] = ServiceSettingValue.String("Бронза"),
                    [ServiceSettingsKeys.QrPrizeTextSilver] = ServiceSettingValue.String("Серебро"),
                    [ServiceSettingsKeys.QrPrizeTextGold] = ServiceSettingValue.String("Золото")
                }));

            var success = service.TryCreateTicket(
                new PrizeFlowResult(PrizeRewardKind.QRCode, 120, 0, 10f),
                out var ticket,
                out var error);

            try
            {
                Assert.That(success, Is.True, error);
                Assert.That(ticket, Is.Not.Null);
                Assert.That(ticket.Message, Is.EqualTo("Серебро"));
                Assert.That(ticket.Tier, Is.EqualTo(QrPrizeTier.Silver));
                Assert.That(ticket.TelegramUrl, Is.EqualTo(
                    "https://t.me/+79999999999?text=" + Uri.EscapeDataString("Серебро")));
                Assert.That(ticket.Texture, Is.Not.Null);
                Assert.That(ticket.Texture.GetPixels().Any(pixel => pixel == QrPrizeTierPalette.GetQrColor(QrPrizeTier.Silver)), Is.True);
            }
            finally
            {
                if (ticket?.Texture != null)
                {
                    UnityEngine.Object.DestroyImmediate(ticket.Texture);
                }
            }

            var nonQrSuccess = service.TryCreateTicket(
                new PrizeFlowResult(PrizeRewardKind.Hopper, 50, 0, 10f),
                out var nonQrTicket,
                out _);

            Assert.That(nonQrSuccess, Is.False);
            Assert.That(nonQrTicket, Is.Null);
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
