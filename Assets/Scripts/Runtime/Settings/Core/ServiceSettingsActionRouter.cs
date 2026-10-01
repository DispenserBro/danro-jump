using System;
using DanroJump.Prizes;
using DanroJump.UI.MainMenu;

namespace DanroJump.Settings
{
    /// <summary>
    /// Маршрутизирует кнопки сервисного меню в понятные оператору действия.
    /// </summary>
    public static class ServiceSettingsActionRouter
    {
        public static ServiceSettingsActionResult Execute(
            ServiceSettingDefinition definition,
            IReadOnlyServiceSettings settings,
            IPrizeSessionStateService prizeSessionState = null)
        {
            if (definition == null)
            {
                return ServiceSettingsActionResult.Unknown(
                    "Неизвестное действие",
                    "Кнопка сервисного меню не содержит описания действия.");
            }

            return definition.Key switch
            {
                ServiceSettingsKeys.StatisticsButton => ServiceSettingsStatisticsSummaryBuilder.Build(
                    settings,
                    prizeSessionState),
                ServiceSettingsKeys.PhoneNumberButton => OpenSettingEditor(
                    "Номер телефона",
                    "Открыто служебное окно номера телефона для QR-призов.",
                    ServiceSettingsKeys.PrizePhoneNumber),
                ServiceSettingsKeys.BigPrizeButton => OpenSettingEditor(
                    "Суперприз",
                    "Открыто служебное окно настройки суперприза.",
                    ServiceSettingsKeys.BigPrize),
                ServiceSettingsKeys.SupportNumberButton => OpenSettingEditor(
                    "Номер поддержки",
                    "Открыто служебное окно номера поддержки автомата.",
                    ServiceSettingsKeys.SupportNumber),
                ServiceSettingsKeys.SettingsPinButton => OpenSettingEditor(
                    "PIN-код",
                    "Открыто служебное окно изменения PIN-кода.",
                    ServiceSettingsKeys.SettingsPin),
                ServiceSettingsKeys.CurrencyButton => OpenSettingEditor(
                    "Валюта баланса",
                    "Открыто служебное окно валюты денежного баланса.",
                    ServiceSettingsKeys.Currency),
                ServiceSettingsKeys.HopperTestButton => ServiceSettingsActionResult.Success(
                    "Тест выдачи",
                    "Запускается тест хоппера: мотор выдачи будет включен до срабатывания датчика или таймаута.",
                    ServiceSettingsKeys.HopperTestButton),
                ServiceSettingsKeys.LevelsPrizesButton => ServiceSettingsActionResult.Success(
                    "Настройки призов",
                    PrizeInfoFormatter.Build(settings),
                    ServiceSettingsKeys.PrizeLevels),
                ServiceSettingsKeys.QrSettingsButton => ServiceSettingsActionResult.Success(
                    "Настройки QR",
                    QrInfoFormatter.Build(settings),
                    ServiceSettingsKeys.QrPrizeTextSettings),
                ServiceSettingsKeys.StandSettingsButton => ServiceSettingsActionResult.Success(
                    "Настройки витрины",
                    "Открыт раздел призовой витрины. Следующий шаг - подключить статусы ячеек и команды тестовой выдачи.",
                    "Призовая витрина"),
                _ => ServiceSettingsActionResult.Unknown(
                    definition.DisplayName,
                    string.IsNullOrWhiteSpace(definition.ButtonData)
                        ? "Для этой кнопки пока нет маршрута действия."
                        : $"Для этой кнопки пока нет маршрута действия: {definition.ButtonData}.")
            };
        }

        private static ServiceSettingsActionResult OpenSettingEditor(string title, string status, string settingKey)
        {
            return ServiceSettingsActionResult.Success(title, status, settingKey);
        }
    }

    public readonly struct ServiceSettingsActionResult
    {
        private ServiceSettingsActionResult(bool handled, string title, string status, string focusKey)
        {
            Handled = handled;
            Title = string.IsNullOrWhiteSpace(title) ? "Служебное действие" : title;
            Status = status ?? string.Empty;
            FocusKey = focusKey ?? string.Empty;
        }

        public bool Handled { get; }
        public string Title { get; }
        public string Status { get; }
        public string FocusKey { get; }

        public static ServiceSettingsActionResult Success(string title, string status, string focusKey)
        {
            return new ServiceSettingsActionResult(true, title, status, focusKey);
        }

        public static ServiceSettingsActionResult Unknown(string title, string status)
        {
            return new ServiceSettingsActionResult(false, title, status, string.Empty);
        }
    }
}
