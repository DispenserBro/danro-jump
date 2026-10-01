using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DanroJump.Prizes
{
    /// <summary>
    /// JSON-хранилище snapshot призовых сессий в Application.persistentDataPath.
    /// </summary>
    public sealed class JsonPrizeSessionStateRepository : IPrizeSessionStateRepository
    {
        private const string DefaultFileName = "prize-session-state.json";

        public JsonPrizeSessionStateRepository(string fileName)
        {
            var safeFileName = GetSafeFileName(fileName);
            Path = System.IO.Path.Combine(Application.persistentDataPath, safeFileName);
        }

        public string Path { get; }

        private bool Exists => File.Exists(Path);

        public bool TryLoad(out PrizeSessionStateSnapshot snapshot)
        {
            snapshot = null;
            if (!Exists)
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(Path);
                var data = JsonUtility.FromJson<PrizeSessionStateSaveData>(json);
                if (data == null)
                {
                    return false;
                }

                var history = new List<PrizeSessionRecord>();
                if (data.history != null)
                {
                    foreach (var entry in data.history)
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(entry.sessionId))
                        {
                            continue;
                        }

                        var result = new PrizeFlowResult(
                            entry.rewardKind,
                            entry.score,
                            entry.coins,
                            entry.height,
                            entry.resultSessionId);
                        history.Add(new PrizeSessionRecord(
                            entry.sessionId,
                            result,
                            entry.status,
                            FromUtcTicks(entry.startedAtUtcTicks),
                            FromUtcTicks(entry.updatedAtUtcTicks),
                            entry.note));
                    }
                }

                var gameplayHistory = new List<GameplaySessionRecord>();
                if (data.gameplayHistory != null)
                {
                    foreach (var entry in data.gameplayHistory)
                    {
                        if (entry == null)
                        {
                            continue;
                        }

                        gameplayHistory.Add(new GameplaySessionRecord(
                            entry.sessionId,
                            entry.score,
                            entry.coins,
                            entry.maxHeight,
                            entry.deathCount,
                            entry.rewardKind,
                            FromUtcTicks(entry.timestampTicks)));
                    }
                }

                snapshot = new PrizeSessionStateSnapshot(
                    history,
                    data.issuedSessionIds ?? new List<string>(),
                    data.activeSessionId,
                    data.startedCount,
                    data.completedCount,
                    data.failedCount,
                    data.duplicateBlockedCount,
                    data.fallbackSessionCounter,
                    gameplayHistory,
                    data.totalGamesPlayed,
                    data.totalScore,
                    data.totalCoins,
                    data.qrShows,
                    data.physicalDispenses,
                    data.physicalDispenseFailures,
                    data.comErrors);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{nameof(JsonPrizeSessionStateRepository)}] Failed to load prize sessions from '{Path}': {exception.Message}");
                return false;
            }
        }

        public bool Save(PrizeSessionStateSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning($"[{nameof(JsonPrizeSessionStateRepository)}] Snapshot is null. Save skipped.");
                return false;
            }

            try
            {
                var directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var data = new PrizeSessionStateSaveData
                {
                    activeSessionId = snapshot.ActiveSessionId ?? string.Empty,
                    startedCount = snapshot.StartedCount,
                    completedCount = snapshot.CompletedCount,
                    failedCount = snapshot.FailedCount,
                    duplicateBlockedCount = snapshot.DuplicateBlockedCount,
                    fallbackSessionCounter = snapshot.FallbackSessionCounter,
                    totalGamesPlayed = snapshot.TotalGamesPlayed,
                    totalScore = snapshot.TotalScore,
                    totalCoins = snapshot.TotalCoins,
                    qrShows = snapshot.QrShows,
                    physicalDispenses = snapshot.PhysicalDispenses,
                    physicalDispenseFailures = snapshot.PhysicalDispenseFailures,
                    comErrors = snapshot.ComErrors
                };

                foreach (var sessionId in snapshot.IssuedSessionIds)
                {
                    if (!string.IsNullOrWhiteSpace(sessionId))
                    {
                        data.issuedSessionIds.Add(sessionId);
                    }
                }

                foreach (var record in snapshot.History)
                {
                    if (record == null || string.IsNullOrWhiteSpace(record.SessionId))
                    {
                        continue;
                    }

                    data.history.Add(new PrizeSessionStateSaveData.Entry
                    {
                        sessionId = record.SessionId,
                        rewardKind = record.Result.RewardKind,
                        score = record.Result.Score,
                        coins = record.Result.Coins,
                        height = record.Result.Height,
                        resultSessionId = record.Result.SessionId,
                        status = record.Status,
                        startedAtUtcTicks = record.StartedAtUtc.Ticks,
                        updatedAtUtcTicks = record.UpdatedAtUtc.Ticks,
                        note = record.Note ?? string.Empty
                    });
                }

                if (snapshot.GameplayHistory != null)
                {
                    foreach (var record in snapshot.GameplayHistory)
                    {
                        if (record == null)
                        {
                            continue;
                        }

                        data.gameplayHistory.Add(new PrizeSessionStateSaveData.GameplayEntry
                        {
                            sessionId = record.SessionId,
                            score = record.Score,
                            coins = record.Coins,
                            maxHeight = record.MaxHeight,
                            deathCount = record.DeathCount,
                            rewardKind = record.RewardKind,
                            timestampTicks = record.Timestamp.Ticks
                        });
                    }
                }

                WriteAllTextSafely(Path, JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[{nameof(JsonPrizeSessionStateRepository)}] Failed to save prize sessions to '{Path}': {exception.Message}");
                return false;
            }
        }

        private static DateTime FromUtcTicks(long ticks)
        {
            if (ticks <= 0)
            {
                return DateTime.UtcNow;
            }

            try
            {
                return new DateTime(ticks, DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateTime.UtcNow;
            }
        }

        private static void WriteAllTextSafely(string path, string contents)
        {
            var tempPath = path + ".tmp";
            var backupPath = path + ".bak";

            try
            {
                File.WriteAllText(tempPath, contents);

                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, backupPath, true);
                    return;
                }

                File.Move(tempPath, path);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static string GetSafeFileName(string fileName)
        {
            var requestedFileName = string.IsNullOrWhiteSpace(fileName) ? DefaultFileName : fileName.Trim();
            var safeFileName = System.IO.Path.GetFileName(requestedFileName);

            if (string.IsNullOrWhiteSpace(safeFileName) ||
                safeFileName == "." ||
                safeFileName == ".." ||
                safeFileName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                Debug.LogWarning(
                    $"[{nameof(JsonPrizeSessionStateRepository)}] Unsafe file name '{fileName}' was replaced with '{DefaultFileName}'.");
                return DefaultFileName;
            }

            if (!string.Equals(requestedFileName, safeFileName, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"[{nameof(JsonPrizeSessionStateRepository)}] Prize session path '{fileName}' was reduced to safe file name '{safeFileName}'.");
            }

            return safeFileName;
        }

        [Serializable]
        private sealed class PrizeSessionStateSaveData
        {
            public string activeSessionId = string.Empty;
            public int startedCount;
            public int completedCount;
            public int failedCount;
            public int duplicateBlockedCount;
            public int fallbackSessionCounter;
            public int totalGamesPlayed;
            public int totalScore;
            public int totalCoins;
            public int qrShows;
            public int physicalDispenses;
            public int physicalDispenseFailures;
            public int comErrors;
            public List<string> issuedSessionIds = new();
            public List<Entry> history = new();
            public List<GameplayEntry> gameplayHistory = new();

            [Serializable]
            public sealed class Entry
            {
                public string sessionId = string.Empty;
                public PrizeRewardKind rewardKind;
                public int score;
                public int coins;
                public float height;
                public string resultSessionId = string.Empty;
                public PrizeSessionStatus status;
                public long startedAtUtcTicks;
                public long updatedAtUtcTicks;
                public string note = string.Empty;
            }

            [Serializable]
            public sealed class GameplayEntry
            {
                public string sessionId = string.Empty;
                public int score;
                public int coins;
                public float maxHeight;
                public int deathCount;
                public PrizeRewardKind rewardKind;
                public long timestampTicks;
            }
        }
    }
}
