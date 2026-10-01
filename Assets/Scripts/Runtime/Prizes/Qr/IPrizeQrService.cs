namespace DanroJump.Prizes.Qr
{
    /// <summary>
    /// Создает QR-билет для призов, которые выдаются через Telegram-связь.
    /// </summary>
    public interface IPrizeQrService
    {
        bool TryCreateTicket(PrizeFlowResult result, out PrizeQrTicket ticket, out string error);
    }
}
