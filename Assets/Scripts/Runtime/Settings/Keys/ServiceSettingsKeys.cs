namespace DanroJump.Settings
{
    /// <summary>
    /// Канонические ключи сервисных настроек проекта.
    /// </summary>
    public static class ServiceSettingsKeys
    {
        public const string MusicVolume = "volume.music";
        public const string SoundsVolume = "volume.sounds";

        public const string FreePlay = "payment.freeGame";
        public const string ShowSupportNumber = "payment.showSupportNumber";
        public const string SupportNumber = "payment.supportNumber";
        public const string PaymentImpulseCost = "payment.paymentImpulseCost";
        public const string StartGameCost = "payment.startGameCost";
        public const string ContinueGameCost = "payment.continueGameCost";
        public const string Currency = "payment.currency";
        public const string GamePrice = StartGameCost;

        public const string ShowBigPrizeText = "prizes.showBigPrizeText";
        public const string GiveSmallPrize = "prizes.giveSmallPrize";
        public const string GiveBigPrize = "prizes.giveBigPrize";
        public const string ErrorGivePrize = "prizes.errorGivePrize";
        public const string ShowPrizeError = "prizes.showPrizeError";
        public const string PrizePhoneNumber = "prizes.phoneNumber";
        public const string QrMessageTemplate = "prizes.qrMessageTemplate";
        public const string QrPrizeTextBronze = "prizes.qrTextBronze";
        public const string QrPrizeTextSilver = "prizes.qrTextSilver";
        public const string QrPrizeTextGold = "prizes.qrTextGold";
        public const string QrPrizeTextSettings = "prizes.qrTextSettings";
        public const string BigPrize = "prizes.bigPrize";
        public const string PrizeLevels = "prizes.levels";

        public const string StartingLives = "gameplay.startLives";
        public const string AdditionalLives = "gameplay.additionalLives";
        public const string CanContinueGame = "gameplay.canContinueGame";
        public const string InactivityTimeout = "gameplay.inactivityTimeout";
        public const string GameDifficulty = "gameplay.gameDifficulty";
        
        public const string RequireSettingsPin = "security.requirePin";
        public const string SettingsPin = "security.pin";

        public const string StatisticsButton = "btn.statistics";
        public const string SettingsPinButton = "btn.settingsPin";
        public const string PhoneNumberButton = "btn.phoneNumber";
        public const string BigPrizeButton = "btn.bigPrize";
        public const string SupportNumberButton = "btn.supportNumber";
        public const string CurrencyButton = "btn.currency";
        public const string HopperTestButton = "btn.hopper_test";
        public const string LevelsPrizesButton = "btn.levels_prizes";
        public const string QrSettingsButton = "btn.qr_settings";
        public const string StandSettingsButton = "btn.stand_settings";
    }
}
