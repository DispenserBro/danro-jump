using DanroJump.Settings;

namespace DanroJump.Prizes.Qr
{
    /// <summary>
    /// Создает QR-билет для Telegram-связи с владельцем автомата.
    /// </summary>
    public sealed class PrizeQrService : IPrizeQrService
    {
        private readonly IReadOnlyServiceSettings settings;
        private readonly TelegramPrizeQrPayloadBuilder payloadBuilder;
        private readonly QrCodeTextureRenderer textureRenderer;

        public PrizeQrService(
            [Zenject.InjectOptional] IReadOnlyServiceSettings settings = null,
            TelegramPrizeQrPayloadBuilder payloadBuilder = null,
            QrCodeTextureRenderer textureRenderer = null)
        {
            this.settings = settings;
            this.payloadBuilder = payloadBuilder ?? new TelegramPrizeQrPayloadBuilder();
            this.textureRenderer = textureRenderer ?? new QrCodeTextureRenderer();
        }

        public bool TryCreateTicket(PrizeFlowResult result, out PrizeQrTicket ticket, out string error)
        {
            ticket = null;
            if (result.RewardKind != PrizeRewardKind.QRCode)
            {
                error = "QR создается только для QR-приза.";
                return false;
            }

            if (!payloadBuilder.TryBuild(result, settings, out var payload, out error))
            {
                return false;
            }

            if (!QrCodeByteModeEncoder.TryEncode(payload.Url, out var matrix, out error))
            {
                return false;
            }

            var texture = textureRenderer.Render(
                matrix,
                darkColor: QrPrizeTierPalette.GetQrColor(payload.Tier));
            ticket = new PrizeQrTicket(
                payload.Url,
                payload.OriginalPhone,
                payload.PhoneDigits,
                payload.Message,
                texture,
                payload.Tier);
            error = string.Empty;
            return true;
        }
    }
}
