using DanroJump.Settings;

namespace DanroJump.Prizes.Qr
{
    /// <summary>
    /// Читает короткие тексты QR-призов по рангам из сервисных настроек.
    /// </summary>
    public static class QrPrizeTextSettings
    {
        public const int MaxPrizeTextLength = 12;

        private static readonly QrPrizeTier[] PreferredTiers =
        {
            QrPrizeTier.Gold,
            QrPrizeTier.Silver,
            QrPrizeTier.Bronze
        };

        public static string GetSettingKey(QrPrizeTier tier)
        {
            return tier switch
            {
                QrPrizeTier.Bronze => ServiceSettingsKeys.QrPrizeTextBronze,
                QrPrizeTier.Silver => ServiceSettingsKeys.QrPrizeTextSilver,
                QrPrizeTier.Gold => ServiceSettingsKeys.QrPrizeTextGold,
                _ => string.Empty
            };
        }

        public static string GetDisplayName(QrPrizeTier tier)
        {
            return tier switch
            {
                QrPrizeTier.Bronze => "Бронзовый",
                QrPrizeTier.Silver => "Серебряный",
                QrPrizeTier.Gold => "Золотой",
                _ => "Не задан"
            };
        }

        public static string GetPrizeText(IReadOnlyServiceSettings settings, QrPrizeTier tier)
        {
            var key = GetSettingKey(tier);
            return string.IsNullOrWhiteSpace(key)
                ? string.Empty
                : NormalizePrizeText(settings?.GetString(key));
        }

        public static string GetPreferredPrizeText(IReadOnlyServiceSettings settings)
        {
            foreach (var tier in PreferredTiers)
            {
                var text = GetPrizeText(settings, tier);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        public static string GetPrizeTextForResult(IReadOnlyServiceSettings settings, PrizeFlowResult result, out QrPrizeTier tier)
        {
            tier = ResolveTier(settings, result);
            if (tier != QrPrizeTier.None)
            {
                var tierText = GetPrizeText(settings, tier);
                if (!string.IsNullOrWhiteSpace(tierText))
                {
                    return tierText;
                }
            }

            return GetPreferredPrizeText(settings);
        }

        public static QrPrizeTier ResolveTier(IReadOnlyServiceSettings settings, PrizeFlowResult result)
        {
            if (result.RewardKind != PrizeRewardKind.QRCode)
            {
                return QrPrizeTier.None;
            }

            return ResolveTier(settings, result.Score);
        }

        public static QrPrizeTier ResolveTier(IReadOnlyServiceSettings settings, int score)
        {
            var sanitizedScore = score < 0 ? 0 : score;
            var levels = PrizeLevelTable.FromJson(settings?.GetString(ServiceSettingsKeys.PrizeLevels));
            var qrLevelIndex = -1;

            for (var index = 0; index < levels.Levels.Count; index++)
            {
                var level = levels.Levels[index];
                if (level.RewardKind != PrizeRewardKind.QRCode)
                {
                    continue;
                }

                if (level.Score > sanitizedScore)
                {
                    break;
                }

                qrLevelIndex++;
            }

            if (qrLevelIndex >= 0)
            {
                return qrLevelIndex switch
                {
                    0 => QrPrizeTier.Bronze,
                    1 => QrPrizeTier.Silver,
                    _ => QrPrizeTier.Gold
                };
            }

            foreach (var tier in PreferredTiers)
            {
                if (!string.IsNullOrWhiteSpace(GetPrizeText(settings, tier)))
                {
                    return tier;
                }
            }

            return QrPrizeTier.None;
        }

        public static string NormalizePrizeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var normalized = text.Trim();
            return normalized.Length <= MaxPrizeTextLength
                ? normalized
                : normalized.Substring(0, MaxPrizeTextLength);
        }
    }
}
