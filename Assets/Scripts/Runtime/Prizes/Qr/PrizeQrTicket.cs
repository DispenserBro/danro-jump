using UnityEngine;

namespace DanroJump.Prizes.Qr
{
    /// <summary>
    /// Готовые данные для QR-экрана призовой выдачи.
    /// </summary>
    public sealed class PrizeQrTicket
    {
        public PrizeQrTicket(
            string telegramUrl,
            string originalPhone,
            string phoneDigits,
            string message,
            Texture2D texture,
            QrPrizeTier tier)
        {
            TelegramUrl = telegramUrl ?? string.Empty;
            OriginalPhone = originalPhone ?? string.Empty;
            PhoneDigits = phoneDigits ?? string.Empty;
            Message = message ?? string.Empty;
            Texture = texture;
            Tier = tier;
        }

        public string TelegramUrl { get; }
        public string OriginalPhone { get; }
        public string PhoneDigits { get; }
        public string Message { get; }
        public Texture2D Texture { get; }
        public QrPrizeTier Tier { get; }
    }
}
