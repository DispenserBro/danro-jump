using DanroJump.Prizes;
using DanroJump.Settings;

namespace DanroJump.UI.MainMenu
{
    /// <summary>
    /// Формирует операторскую сводку призовых уровней и legacy-флагов.
    /// </summary>
    internal static class PrizeInfoFormatter
    {
        public static string Build(IReadOnlyServiceSettings settings)
        {
            if (settings == null)
            {
                return "Раздел призов открыт. Сервис настроек пока недоступен.";
            }

            var smallPrize = settings.GetBool(ServiceSettingsKeys.GiveSmallPrize) ? "включён" : "выключен";
            var bigPrize = settings.GetBool(ServiceSettingsKeys.GiveBigPrize) ? "включён" : "выключен";
            var levels = PrizeLevelTable.FromJson(settings.GetString(ServiceSettingsKeys.PrizeLevels));
            if (levels.Count > 0)
            {
                return $"Раздел призов открыт. Настроено уровней: {levels.Count}.";
            }

            return $"Раздел призов открыт. Таблица уровней пустая. Legacy: малый приз {smallPrize}, большой приз {bigPrize}.";
        }
    }
}
