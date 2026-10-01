using DanroJump.Prizes;

namespace DanroJump.UI.MainMenu
{
    internal static class PrizeLevelUiFormatter
    {
        public static string FormatKind(PrizeRewardKind rewardKind)
        {
            return rewardKind switch
            {
                PrizeRewardKind.QRCode => "QR-код",
                PrizeRewardKind.Hopper => "Хоппер",
                PrizeRewardKind.PrizeStand => "Витрина",
                PrizeRewardKind.Big => "Большой приз",
                PrizeRewardKind.Small => "Малый приз",
                _ => "Без приза"
            };
        }

        public static PrizeRewardKind PreviousKind(PrizeRewardKind rewardKind)
        {
            return rewardKind switch
            {
                PrizeRewardKind.Small => PrizeRewardKind.Big,
                PrizeRewardKind.Big => PrizeRewardKind.PrizeStand,
                PrizeRewardKind.PrizeStand => PrizeRewardKind.Hopper,
                PrizeRewardKind.Hopper => PrizeRewardKind.QRCode,
                PrizeRewardKind.QRCode => PrizeRewardKind.None,
                _ => PrizeRewardKind.Big
            };
        }

        public static PrizeRewardKind NextKind(PrizeRewardKind rewardKind)
        {
            return rewardKind switch
            {
                PrizeRewardKind.Big => PrizeRewardKind.Small,
                PrizeRewardKind.Small => PrizeRewardKind.None,
                PrizeRewardKind.None => PrizeRewardKind.QRCode,
                PrizeRewardKind.QRCode => PrizeRewardKind.Hopper,
                PrizeRewardKind.Hopper => PrizeRewardKind.PrizeStand,
                PrizeRewardKind.PrizeStand => PrizeRewardKind.Big,
                _ => PrizeRewardKind.None
            };
        }
    }
}
