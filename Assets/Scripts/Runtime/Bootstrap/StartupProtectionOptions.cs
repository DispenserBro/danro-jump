using System;
using UnityEngine;

namespace DanroJump.Bootstrap
{
    /// <summary>
    /// Настройки проверки запуска приложения через внешний launcher.
    /// </summary>
    [Serializable]
    public sealed class StartupProtectionOptions
    {
        [SerializeField] private string launcherName = "RRLauncher.exe";
        [SerializeField] [Min(0.1f)] private float checkTimeoutSeconds = 30f;
        [SerializeField] [Min(0f)] private float quitDelaySeconds = 3f;
        [SerializeField] private bool skipInEditor = true;
        [SerializeField] private bool skipInDevelopmentBuild = true;
        [SerializeField] private bool applyLegacyStartupPresentation;
        [SerializeField] [Min(1)] private int legacyTargetWidth = 1920;
        [SerializeField] [Min(1)] private int legacyTargetHeight = 1080;
        [SerializeField] private FullScreenMode legacyFullScreenMode = FullScreenMode.ExclusiveFullScreen;
        [SerializeField] private bool clickCenterOnSuccess = true;
        [SerializeField] private bool focusWindowOnSuccess = true;

        public StartupProtectionOptions()
        {
        }

        public StartupProtectionOptions(
            string launcherName = "RRLauncher.exe",
            float checkTimeoutSeconds = 30f,
            float quitDelaySeconds = 3f,
            bool skipInEditor = true,
            bool skipInDevelopmentBuild = true,
            bool applyLegacyStartupPresentation = false,
            int legacyTargetWidth = 1920,
            int legacyTargetHeight = 1080,
            FullScreenMode legacyFullScreenMode = FullScreenMode.ExclusiveFullScreen,
            bool clickCenterOnSuccess = true,
            bool focusWindowOnSuccess = true)
        {
            this.launcherName = launcherName;
            this.checkTimeoutSeconds = checkTimeoutSeconds;
            this.quitDelaySeconds = quitDelaySeconds;
            this.skipInEditor = skipInEditor;
            this.skipInDevelopmentBuild = skipInDevelopmentBuild;
            this.applyLegacyStartupPresentation = applyLegacyStartupPresentation;
            this.legacyTargetWidth = legacyTargetWidth;
            this.legacyTargetHeight = legacyTargetHeight;
            this.legacyFullScreenMode = legacyFullScreenMode;
            this.clickCenterOnSuccess = clickCenterOnSuccess;
            this.focusWindowOnSuccess = focusWindowOnSuccess;
        }

        public string LauncherName => string.IsNullOrWhiteSpace(launcherName) ? "RRLauncher.exe" : launcherName;
        public TimeSpan CheckTimeout => TimeSpan.FromSeconds(Mathf.Max(0.1f, checkTimeoutSeconds));
        public TimeSpan QuitDelay => TimeSpan.FromSeconds(Mathf.Max(0f, quitDelaySeconds));
        public bool SkipInEditor => skipInEditor;
        public bool SkipInDevelopmentBuild => skipInDevelopmentBuild;
        public bool ApplyLegacyStartupPresentation => applyLegacyStartupPresentation;
        public int LegacyTargetWidth => Mathf.Max(1, legacyTargetWidth);
        public int LegacyTargetHeight => Mathf.Max(1, legacyTargetHeight);
        public FullScreenMode LegacyFullScreenMode => legacyFullScreenMode;
        public bool ClickCenterOnSuccess => clickCenterOnSuccess;
        public bool FocusWindowOnSuccess => focusWindowOnSuccess;
    }
}
