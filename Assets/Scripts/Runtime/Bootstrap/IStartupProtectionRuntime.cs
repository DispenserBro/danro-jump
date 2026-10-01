using UnityEngine;

namespace DanroJump.Bootstrap
{
    public interface IStartupProtectionRuntime
    {
        bool IsEditor { get; }
        bool IsDevelopmentBuild { get; }
        string BaseDirectory { get; }
        string StreamingAssetsPath { get; }

        void ApplyLegacyStartupPresentation(StartupProtectionOptions options);
        void QuitApplication();
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogException(System.Exception exception);
    }
}
