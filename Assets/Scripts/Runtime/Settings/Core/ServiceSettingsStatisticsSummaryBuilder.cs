using System;
using System.Linq;
using DanroJump.Prizes;

namespace DanroJump.Settings
{
    /// <summary>
    /// Собирает текст сервисной статистики без знания о маршрутизации кнопок.
    /// </summary>
    internal static class ServiceSettingsStatisticsSummaryBuilder
    {
        public static ServiceSettingsActionResult Build(
            IReadOnlyServiceSettings settings,
            IPrizeSessionStateService prizeSessionState)
        {
            var snapshot = settings?.Snapshot();
            var totalSettings = snapshot?.Count ?? 0;
            var visibleDefinitions = settings?.Database?.Entries.Count(entry => entry.ShownInServiceMenu) ?? 0;
            var startCost = settings != null ? Math.Max(0, settings.GetInt(ServiceSettingsKeys.StartGameCost)) : 0;
            var freePlay = settings != null && settings.GetBool(ServiceSettingsKeys.FreePlay);
            var priceText = freePlay ? "free play" : $"{startCost}";

            var prizeSummary = string.Empty;
            if (prizeSessionState != null)
            {
                var historyList = prizeSessionState.History;
                var recentHistory = historyList != null && historyList.Count > 0
                    ? string.Join("\n", historyList.Skip(Math.Max(0, historyList.Count - 5)).Select(record => 
                        $"- [{record.StartedAtUtc:yyyy-MM-dd HH:mm:ss}] {FormatReward(record.Result.RewardKind)} ({record.Result.Score}очк) -> {FormatStatus(record.Status)}{(string.IsNullOrWhiteSpace(record.Note) ? "" : ": " + record.Note)}"))
                    : "История призов пуста.";

                var gameplayHistory = prizeSessionState.GameplayHistory;
                var avgScore = gameplayHistory != null && gameplayHistory.Count > 0
                    ? gameplayHistory.Average(r => r.Score)
                    : 0f;
                var avgCoins = gameplayHistory != null && gameplayHistory.Count > 0
                    ? gameplayHistory.Average(r => r.Coins)
                    : 0f;
                var avgHeight = gameplayHistory != null && gameplayHistory.Count > 0
                    ? gameplayHistory.Average(r => r.MaxHeight)
                    : 0f;

                var recentGameplay = gameplayHistory != null && gameplayHistory.Count > 0
                    ? string.Join("\n", gameplayHistory.Skip(Math.Max(0, gameplayHistory.Count - 5)).Select(record => 
                        $"- [{record.Timestamp:yyyy-MM-dd HH:mm:ss}] очков: {record.Score}, монет: {record.Coins}, высота: {record.MaxHeight:F1}м, смертей: {record.DeathCount}, приз: {FormatReward(record.RewardKind)}"))
                    : "История игр пуста.";

                prizeSummary = $"\n\n--- Эксплуатационная статистика ---\n" +
                               $"Всего сыграно игр: {prizeSessionState.TotalGamesPlayed}\n" +
                               $"Набрано очков всего: {prizeSessionState.TotalScore}\n" +
                               $"Собрано монет всего: {prizeSessionState.TotalCoins}\n" +
                               $"Средние показатели (последние сессии): очков {avgScore:F1}, монет {avgCoins:F1}, высота {avgHeight:F1}м\n" +
                               $"Показов QR-кодов: {prizeSessionState.QrShows}\n" +
                               $"Успешных физ. выдач: {prizeSessionState.PhysicalDispenses}\n" +
                               $"Сбоев физ. выдач: {prizeSessionState.PhysicalDispenseFailures}\n" +
                               $"Сбоев связи по COM: {prizeSessionState.ComErrors}\n" +
                               $"Призовые сессии: начато {prizeSessionState.StartedCount}, завершено {prizeSessionState.CompletedCount}, сбоев {prizeSessionState.FailedCount}, повторов заблокировано {prizeSessionState.DuplicateBlockedCount}\n" +
                               $"\n--- Последние Игры (до 5) ---\n{recentGameplay}\n" +
                               $"\n--- Последние Призовые сессии (до 5) ---\n{recentHistory}";
            }
            else
            {
                prizeSummary = "\nПризовая история недоступна в текущем контексте.";
            }

            return ServiceSettingsActionResult.Success(
                "Статистика",
                $"Настроек загружено: {totalSettings}. Видимых пунктов: {visibleDefinitions}. Стоимость старта: {priceText}.{prizeSummary}",
                "Статистика");
        }

        private static string FormatReward(PrizeRewardKind rewardKind)
        {
            return rewardKind switch
            {
                PrizeRewardKind.QRCode => "QR",
                PrizeRewardKind.Hopper => "Хоппер",
                PrizeRewardKind.PrizeStand => "Витрина",
                PrizeRewardKind.Big => "Большой приз",
                PrizeRewardKind.Small => "Малый приз",
                _ => "Нет приза",
            };
        }

        private static string FormatStatus(PrizeSessionStatus status)
        {
            return status switch
            {
                PrizeSessionStatus.Pending => "ожидает показа",
                PrizeSessionStatus.Shown => "показана",
                PrizeSessionStatus.Completed => "завершена",
                PrizeSessionStatus.Failed => "ошибка",
                PrizeSessionStatus.DuplicateBlocked => "повтор заблокирован",
                _ => status.ToString(),
            };
        }
    }
}
