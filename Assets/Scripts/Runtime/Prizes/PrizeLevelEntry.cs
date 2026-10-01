using System;
using UnityEngine;

namespace DanroJump.Prizes
{
    /// <summary>
    /// Настраиваемый призовой порог: тип приза, который выдается при достижении указанного счета.
    /// </summary>
    [Serializable]
    public struct PrizeLevelEntry
    {
        [SerializeField] private int score;
        [SerializeField] private PrizeRewardKind rewardKind;

        public PrizeLevelEntry(int score, PrizeRewardKind rewardKind)
        {
            this.score = Mathf.Max(0, score);
            this.rewardKind = rewardKind;
        }

        public int Score => Mathf.Max(0, score);

        public PrizeRewardKind RewardKind => rewardKind;
    }
}
