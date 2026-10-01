using DanroJump.Prizes.Qr;
using DanroJump.Settings;

namespace DanroJump.UI.MainMenu
{
    /// <summary>
    /// Формирует операторскую сводку по конфигурации QR-кодов.
    /// </summary>
    internal static class QrInfoFormatter
    {
        public static string Build(IReadOnlyServiceSettings settings)
        {
            if (settings == null)
            {
                return "Раздел QR открыт. Сервис настроек пока недоступен.";
            }

            var showQrErrors = settings.GetBool(ServiceSettingsKeys.ShowPrizeError) ? "показывать" : "скрывать";
            var phone = settings.GetString(ServiceSettingsKeys.PrizePhoneNumber);
            var phoneDigits = TelegramPrizeQrPayloadBuilder.NormalizePhoneDigits(phone);
            var phoneText = string.IsNullOrWhiteSpace(phone) ? "не задан" : phone;
            var bronze = FormatTierText(settings, QrPrizeTier.Bronze);
            var silver = FormatTierText(settings, QrPrizeTier.Silver);
            var gold = FormatTierText(settings, QrPrizeTier.Gold);
            var qrState = string.IsNullOrWhiteSpace(phoneDigits)
                ? "QR не готов: не задан телефон."
                : "QR готов к показу.";

            return $"Раздел QR открыт. {qrState} Ошибки QR: {showQrErrors}. Телефон: {phoneText}. Тексты призов: Бронзовый: {bronze}; Серебряный: {silver}; Золотой: {gold}.";
        }

        private static string FormatTierText(IReadOnlyServiceSettings settings, QrPrizeTier tier)
        {
            var text = QrPrizeTextSettings.GetPrizeText(settings, tier);
            return string.IsNullOrWhiteSpace(text) ? "<пусто>" : text;
        }
    }
}
