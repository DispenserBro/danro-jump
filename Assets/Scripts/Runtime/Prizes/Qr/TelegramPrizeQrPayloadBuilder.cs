using System;
using System.Globalization;
using System.Text;
using DanroJump.Settings;

namespace DanroJump.Prizes.Qr
{
    /// <summary>
    /// Собирает Telegram t.me URL для QR-приза из сервисного телефона и короткого текста приза.
    /// </summary>
    public sealed class TelegramPrizeQrPayloadBuilder
    {
        public const string DefaultMessageTemplate =
            "{0}";

        private const string DefaultPrizeText = "QR-приз";

        public bool TryBuild(
            PrizeFlowResult result,
            IReadOnlyServiceSettings settings,
            out TelegramPrizeQrPayload payload,
            out string error)
        {
            payload = default;
            var phone = settings?.GetString(ServiceSettingsKeys.PrizePhoneNumber) ?? string.Empty;
            var phoneDigits = NormalizePhoneDigits(phone);
            if (string.IsNullOrWhiteSpace(phoneDigits))
            {
                error = "Не задан номер телефона для QR-приза.";
                return false;
            }

            var prizeText = QrPrizeTextSettings.GetPrizeTextForResult(settings, result, out var tier);
            if (string.IsNullOrWhiteSpace(prizeText))
            {
                prizeText = DefaultPrizeText;
            }

            var message = FormatMessageTemplate(DefaultMessageTemplate, result, phone, prizeText);
            var encodedMessage = Uri.EscapeDataString(message);
            var url = $"https://t.me/+{phoneDigits}?text={encodedMessage}";
            payload = new TelegramPrizeQrPayload(url, phone, phoneDigits, message, tier);
            error = string.Empty;
            return true;
        }

        public static string NormalizePhoneDigits(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(phone.Length);
            foreach (var symbol in phone)
            {
                if (char.IsDigit(symbol))
                {
                    builder.Append(symbol);
                }
            }

            return builder.ToString();
        }

        public static string FormatMessageTemplate(
            string template,
            PrizeFlowResult result,
            string phone,
            string prizeText = null)
        {
            var normalizedPrizeText = QrPrizeTextSettings.NormalizePrizeText(prizeText);
            if (!string.IsNullOrEmpty(normalizedPrizeText) && (string.IsNullOrWhiteSpace(template) || template == "{0}"))
            {
                return normalizedPrizeText;
            }

            return (template ?? string.Empty)
                .Replace("{0}", normalizedPrizeText)
                .Replace("{prize}", normalizedPrizeText)
                .Replace("{score}", result.Score.ToString(CultureInfo.InvariantCulture))
                .Replace("{coins}", result.Coins.ToString(CultureInfo.InvariantCulture))
                .Replace("{height}", result.Height.ToString("0", CultureInfo.InvariantCulture))
                .Replace("{kind}", result.RewardKind.ToString())
                .Replace("{phone}", phone ?? string.Empty);
        }
    }

    public readonly struct TelegramPrizeQrPayload
    {
        public TelegramPrizeQrPayload(string url, string originalPhone, string phoneDigits, string message, QrPrizeTier tier)
        {
            Url = url ?? string.Empty;
            OriginalPhone = originalPhone ?? string.Empty;
            PhoneDigits = phoneDigits ?? string.Empty;
            Message = message ?? string.Empty;
            Tier = tier;
        }

        public string Url { get; }
        public string OriginalPhone { get; }
        public string PhoneDigits { get; }
        public string Message { get; }
        public QrPrizeTier Tier { get; }
    }
}
