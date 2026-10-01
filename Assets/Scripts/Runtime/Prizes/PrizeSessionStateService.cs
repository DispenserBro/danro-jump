using System;
using System.Collections.Generic;
using System.Linq;
using DanroJump.Hardware.Com;

namespace DanroJump.Prizes
{
    /// <summary>
    /// In-memory журнал призовых решений для сервисной статистики и защиты от повторной выдачи.
    /// </summary>
    public sealed class PrizeSessionStateService : IPrizeSessionStateService
    {
        private const string RecoveryFailureNote = "Сессия восстановлена после перезапуска до завершения выдачи.";
        private const int MaxHistorySize = 50;

        private readonly List<PrizeSessionRecord> history = new();
        private readonly List<GameplaySessionRecord> gameplayHistory = new();
        private readonly HashSet<string> issuedSessionIds = new(StringComparer.Ordinal);
        private readonly IPrizeSessionStateRepository repository;
        private int fallbackSessionCounter;

        public PrizeSessionStateService(
            [Zenject.InjectOptional] IPrizeSessionStateRepository repository = null,
            [Zenject.InjectOptional] IComSystem comSystem = null)
        {
            this.repository = repository;
            RestoreState();
            if (comSystem != null)
            {
                comSystem.DeviceDisconnected.AddListener(HandleDeviceDisconnected);
            }
        }

        private void HandleDeviceDisconnected(string deviceId)
        {
            RecordComError();
        }

        public event Action Changed;

        public PrizeSessionRecord ActiveSession { get; private set; }
        public IReadOnlyList<PrizeSessionRecord> History => history;
        public IReadOnlyList<GameplaySessionRecord> GameplayHistory => gameplayHistory;
        public int StartedCount { get; private set; }
        public int CompletedCount { get; private set; }
        public int FailedCount { get; private set; }
        public int DuplicateBlockedCount { get; private set; }

        public int TotalGamesPlayed { get; private set; }
        public int TotalScore { get; private set; }
        public int TotalCoins { get; private set; }
        public int QrShows { get; private set; }
        public int PhysicalDispenses { get; private set; }
        public int PhysicalDispenseFailures { get; private set; }
        public int ComErrors { get; private set; }

        public bool TryBeginSession(PrizeFlowResult result, out PrizeSessionRecord record)
        {
            record = null;
            if (!result.HasPrize)
            {
                return false;
            }

            var sessionId = ResolveSessionId(result);
            if (ActiveSession != null)
            {
                DuplicateBlockedCount++;
                record = new PrizeSessionRecord(
                    sessionId,
                    result,
                    PrizeSessionStatus.DuplicateBlocked,
                    DateTime.UtcNow,
                    "Новая выдача заблокирована: предыдущая призовая сессия ещё активна.");
                history.Add(record);
                PersistState();
                Changed?.Invoke();
                return false;
            }

            if (issuedSessionIds.Contains(sessionId))
            {
                DuplicateBlockedCount++;
                record = new PrizeSessionRecord(
                    sessionId,
                    result,
                    PrizeSessionStatus.DuplicateBlocked,
                    DateTime.UtcNow,
                    "Повторная выдача этой игровой сессии заблокирована.");
                history.Add(record);
                PersistState();
                Changed?.Invoke();
                return false;
            }

            issuedSessionIds.Add(sessionId);
            record = new PrizeSessionRecord(sessionId, result, PrizeSessionStatus.Pending, DateTime.UtcNow);
            ActiveSession = record;
            history.Add(record);
            StartedCount++;
            PersistState();
            Changed?.Invoke();
            return true;
        }

        public bool MarkActiveSessionShown()
        {
            return SetActiveStatus(PrizeSessionStatus.Shown, null, countTerminal: false);
        }

        public bool CompleteActiveSession(string note = null)
        {
            return SetActiveStatus(PrizeSessionStatus.Completed, note, countTerminal: true);
        }

        public bool FailActiveSession(string reason)
        {
            return SetActiveStatus(PrizeSessionStatus.Failed, reason, countTerminal: true);
        }

        public string BuildSummary()
        {
            var activeText = ActiveSession == null
                ? "Активной выдачи нет."
                : $"Активная выдача: {FormatReward(ActiveSession.Result.RewardKind)}, статус {FormatStatus(ActiveSession.Status)}, очки {ActiveSession.Result.Score}.";
            var last = history.LastOrDefault();
            var lastText = last == null
                ? "История пуста."
                : $"Последняя запись: {FormatReward(last.Result.RewardKind)}, {FormatStatus(last.Status)}, очки {last.Result.Score}.";

            return $"Игр: {TotalGamesPlayed}. Очков всего: {TotalScore}. Монет всего: {TotalCoins}. QR показов: {QrShows}. Физич. выдач: {PhysicalDispenses} (ошибок: {PhysicalDispenseFailures}). COM-ошибок: {ComErrors}. Призовые сессии: начато {StartedCount}, завершено {CompletedCount}, ошибок {FailedCount}, повторов заблокировано {DuplicateBlockedCount}. {activeText} {lastText}";
        }

        public void RecordGameplaySession(
            string sessionId,
            int score,
            int coins,
            float maxHeight,
            int deathCount,
            PrizeRewardKind rewardKind)
        {
            var record = new GameplaySessionRecord(
                sessionId,
                score,
                coins,
                maxHeight,
                deathCount,
                rewardKind,
                DateTime.UtcNow
            );

            gameplayHistory.Add(record);
            if (gameplayHistory.Count > MaxHistorySize)
            {
                gameplayHistory.RemoveAt(0);
            }

            TotalGamesPlayed++;
            TotalScore += score;
            TotalCoins += coins;
            PersistState();
            Changed?.Invoke();
        }

        public void RecordQrShow()
        {
            QrShows++;
            PersistState();
            Changed?.Invoke();
        }

        public void RecordComError()
        {
            ComErrors++;
            PersistState();
            Changed?.Invoke();
        }

        public void RecordPhysicalDispense(bool success)
        {
            if (success)
            {
                PhysicalDispenses++;
            }
            else
            {
                PhysicalDispenseFailures++;
            }
            PersistState();
            Changed?.Invoke();
        }

        private bool SetActiveStatus(PrizeSessionStatus status, string note, bool countTerminal)
        {
            if (ActiveSession == null)
            {
                return false;
            }

            ActiveSession.SetStatus(status, DateTime.UtcNow, note);
            if (countTerminal)
            {
                if (status == PrizeSessionStatus.Completed)
                {
                    CompletedCount++;
                }
                else if (status == PrizeSessionStatus.Failed)
                {
                    FailedCount++;
                }

                ActiveSession = null;
            }

            PersistState();
            Changed?.Invoke();
            return true;
        }

        private void RestoreState()
        {
            if (repository == null || !repository.TryLoad(out var snapshot) || snapshot == null)
            {
                return;
            }

            history.Clear();
            gameplayHistory.Clear();
            issuedSessionIds.Clear();

            if (snapshot.GameplayHistory != null)
            {
                foreach (var record in snapshot.GameplayHistory)
                {
                    if (record != null)
                    {
                        gameplayHistory.Add(record);
                    }
                }
            }

            foreach (var record in snapshot.History)
            {
                if (record != null)
                {
                    history.Add(record);
                }
            }

            foreach (var sessionId in snapshot.IssuedSessionIds)
            {
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    issuedSessionIds.Add(sessionId);
                }
            }

            StartedCount = snapshot.StartedCount;
            CompletedCount = snapshot.CompletedCount;
            FailedCount = snapshot.FailedCount;
            DuplicateBlockedCount = snapshot.DuplicateBlockedCount;
            fallbackSessionCounter = snapshot.FallbackSessionCounter;
            TotalGamesPlayed = snapshot.TotalGamesPlayed;
            TotalScore = snapshot.TotalScore;
            TotalCoins = snapshot.TotalCoins;
            QrShows = snapshot.QrShows;
            PhysicalDispenses = snapshot.PhysicalDispenses;
            PhysicalDispenseFailures = snapshot.PhysicalDispenseFailures;
            ComErrors = snapshot.ComErrors;
            ActiveSession = null;

            var restoredActive = FindHistoryRecord(snapshot.ActiveSessionId);
            if (restoredActive != null &&
                (restoredActive.Status == PrizeSessionStatus.Pending || restoredActive.Status == PrizeSessionStatus.Shown))
            {
                restoredActive.SetStatus(
                    PrizeSessionStatus.Failed,
                    DateTime.UtcNow,
                    AppendRecoveryNote(restoredActive.Note));
                FailedCount++;
                PersistState();
            }
        }

        private void PersistState()
        {
            repository?.Save(CreateSnapshot());
        }

        private PrizeSessionStateSnapshot CreateSnapshot()
        {
            return new PrizeSessionStateSnapshot(
                history.ToArray(),
                issuedSessionIds.ToArray(),
                ActiveSession?.SessionId,
                StartedCount,
                CompletedCount,
                FailedCount,
                DuplicateBlockedCount,
                fallbackSessionCounter,
                gameplayHistory.ToArray(),
                TotalGamesPlayed,
                TotalScore,
                TotalCoins,
                QrShows,
                PhysicalDispenses,
                PhysicalDispenseFailures,
                ComErrors);
        }

        private PrizeSessionRecord FindHistoryRecord(string sessionId)
        {
            return string.IsNullOrWhiteSpace(sessionId)
                ? null
                : history.LastOrDefault(record => string.Equals(record.SessionId, sessionId, StringComparison.Ordinal));
        }

        private static string AppendRecoveryNote(string existingNote)
        {
            return string.IsNullOrWhiteSpace(existingNote)
                ? RecoveryFailureNote
                : existingNote + " " + RecoveryFailureNote;
        }

        private string ResolveSessionId(PrizeFlowResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.SessionId))
            {
                return result.SessionId;
            }

            fallbackSessionCounter++;
            return $"prize-session-{fallbackSessionCounter}";
        }

        private static string FormatReward(PrizeRewardKind rewardKind)
        {
            return rewardKind switch
            {
                PrizeRewardKind.QRCode => "QR",
                PrizeRewardKind.Hopper => "хоппер",
                PrizeRewardKind.PrizeStand => "витрина",
                PrizeRewardKind.Big => "большой приз",
                PrizeRewardKind.Small => "малый приз",
                _ => "нет приза",
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
