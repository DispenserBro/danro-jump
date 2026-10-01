using UnityEngine;

namespace DanroJump.Prizes.Qr
{
    /// <summary>
    /// Палитра цветов для рангов QR-приза.
    /// </summary>
    public static class QrPrizeTierPalette
    {
        private static readonly Color32 BronzeQrColor = new(134, 84, 38, 255);
        private static readonly Color32 SilverQrColor = new(88, 102, 120, 255);
        private static readonly Color32 GoldQrColor = new(160, 124, 24, 255);

        public static Color GetQrColor(QrPrizeTier tier)
        {
            return tier switch
            {
                QrPrizeTier.Bronze => BronzeQrColor,
                QrPrizeTier.Silver => SilverQrColor,
                QrPrizeTier.Gold => GoldQrColor,
                _ => Color.black
            };
        }
    }
}
