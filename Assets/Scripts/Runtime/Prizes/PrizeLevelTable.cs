using System;
using System.Collections.Generic;
using UnityEngine;

namespace DanroJump.Prizes
{
    /// <summary>
    /// Сериализуемая таблица призов, привязанная к набранным очкам.
    /// </summary>
    [Serializable]
    public sealed class PrizeLevelTable
    {
        [SerializeField] private List<PrizeLevelEntry> levels = new();

        public IReadOnlyList<PrizeLevelEntry> Levels => levels;

        public int Count => levels.Count;

        public static PrizeLevelTable FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new PrizeLevelTable();
            }

            try
            {
                var table = JsonUtility.FromJson<PrizeLevelTable>(json);
                table ??= new PrizeLevelTable();
                table.Normalize();
                return table;
            }
            catch (ArgumentException)
            {
                return new PrizeLevelTable();
            }
        }

        public string ToJson()
        {
            Normalize();
            return JsonUtility.ToJson(this);
        }

        public PrizeRewardKind ResolveRewardKind(int score)
        {
            Normalize();
            var sanitizedScore = Mathf.Max(0, score);
            var rewardKind = PrizeRewardKind.None;

            foreach (var level in levels)
            {
                if (level.Score > sanitizedScore)
                {
                    break;
                }

                rewardKind = level.RewardKind;
            }

            return rewardKind;
        }

        public void Upsert(int score, PrizeRewardKind rewardKind)
        {
            var sanitizedScore = Mathf.Max(0, score);
            for (var index = 0; index < levels.Count; index++)
            {
                if (levels[index].Score == sanitizedScore)
                {
                    levels[index] = new PrizeLevelEntry(sanitizedScore, rewardKind);
                    Normalize();
                    return;
                }
            }

            levels.Add(new PrizeLevelEntry(sanitizedScore, rewardKind));
            Normalize();
        }

        public bool RemoveAt(int index)
        {
            Normalize();
            if (index < 0 || index >= levels.Count)
            {
                return false;
            }

            levels.RemoveAt(index);
            return true;
        }

        private void Normalize()
        {
            levels ??= new List<PrizeLevelEntry>();
            for (var index = 0; index < levels.Count; index++)
            {
                var level = levels[index];
                levels[index] = new PrizeLevelEntry(level.Score, SanitizeKind(level.RewardKind));
            }

            levels.Sort(static (left, right) => left.Score.CompareTo(right.Score));
            for (var index = levels.Count - 2; index >= 0; index--)
            {
                if (levels[index].Score == levels[index + 1].Score)
                {
                    levels.RemoveAt(index);
                }
            }
        }

        private static PrizeRewardKind SanitizeKind(PrizeRewardKind rewardKind)
        {
            return Enum.IsDefined(typeof(PrizeRewardKind), rewardKind)
                ? rewardKind
                : PrizeRewardKind.None;
        }
    }
}
