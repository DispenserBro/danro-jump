using UnityEngine;

namespace DanroJump.Hardware.Com
{
    /// <summary>
    /// Настройки аппаратной подсветки COM-системы.
    /// </summary>
    public readonly struct ComLightingOptions
    {
        private const string DefaultPrizeStandIdleLightingCommand = "8020";
        private const string DefaultPrizeStandHighlightBoxCommandPrefix = "8021";
        private const string DefaultBoardEffectsCommandPrefix = "86";
        private const string DefaultBoardEffectsOffCommand = "85";

        public readonly string PrizeStandIdleLightingCommand;
        public readonly string PrizeStandHighlightBoxCommandPrefix;
        public readonly string BoardEffectsCommandPrefix;
        public readonly string BoardEffectsOffCommand;
        public readonly string HatOnCommand;
        public readonly string HatOffCommand;
        public readonly float HatBlinkIntervalSeconds;
        public readonly float HatBlinkVarianceSeconds;
        public readonly LightMode DefaultLightMode;
        public readonly float DefaultLightCycles;

        public ComLightingOptions(
            string hatOnCommand,
            string hatOffCommand,
            float hatBlinkIntervalSeconds,
            float hatBlinkVarianceSeconds,
            LightMode defaultLightMode,
            float defaultLightCycles)
            : this(
                DefaultPrizeStandIdleLightingCommand,
                DefaultPrizeStandHighlightBoxCommandPrefix,
                DefaultBoardEffectsCommandPrefix,
                DefaultBoardEffectsOffCommand,
                hatOnCommand,
                hatOffCommand,
                hatBlinkIntervalSeconds,
                hatBlinkVarianceSeconds,
                defaultLightMode,
                defaultLightCycles)
        {
        }

        public ComLightingOptions(
            string prizeStandIdleLightingCommand,
            string prizeStandHighlightBoxCommandPrefix,
            string boardEffectsCommandPrefix,
            string boardEffectsOffCommand,
            string hatOnCommand,
            string hatOffCommand,
            float hatBlinkIntervalSeconds,
            float hatBlinkVarianceSeconds,
            LightMode defaultLightMode,
            float defaultLightCycles)
        {
            PrizeStandIdleLightingCommand = string.IsNullOrWhiteSpace(prizeStandIdleLightingCommand)
                ? DefaultPrizeStandIdleLightingCommand
                : prizeStandIdleLightingCommand.Trim();
            PrizeStandHighlightBoxCommandPrefix = string.IsNullOrWhiteSpace(prizeStandHighlightBoxCommandPrefix)
                ? DefaultPrizeStandHighlightBoxCommandPrefix
                : prizeStandHighlightBoxCommandPrefix.Trim();
            BoardEffectsCommandPrefix = string.IsNullOrWhiteSpace(boardEffectsCommandPrefix)
                ? DefaultBoardEffectsCommandPrefix
                : boardEffectsCommandPrefix.Trim();
            BoardEffectsOffCommand = string.IsNullOrWhiteSpace(boardEffectsOffCommand)
                ? DefaultBoardEffectsOffCommand
                : boardEffectsOffCommand.Trim();
            HatOnCommand = string.IsNullOrWhiteSpace(hatOnCommand) ? "2131" : hatOnCommand.Trim();
            HatOffCommand = string.IsNullOrWhiteSpace(hatOffCommand) ? "2130" : hatOffCommand.Trim();
            HatBlinkIntervalSeconds = Mathf.Max(0.05f, hatBlinkIntervalSeconds);
            HatBlinkVarianceSeconds = Mathf.Max(0f, hatBlinkVarianceSeconds);
            DefaultLightMode = defaultLightMode;
            DefaultLightCycles = Mathf.Max(0f, defaultLightCycles);
        }
    }
}
