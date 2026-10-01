using UnityEngine;

namespace DanroJump.Debugging
{
    /// <summary>
    /// Централизованный сервис отладочного вывода с фильтрацией по каналам.
    /// </summary>
    public interface IDebugLogService
    {
        bool IsEnabled { get; set; }
        DebugLogChannel EnabledChannels { get; set; }
        DebugLogOutput EnabledOutputs { get; set; }

        bool IsChannelEnabled(DebugLogChannel channel);
        void Log(DebugLogChannel channel, string message, Object context = null);
        void LogWarning(DebugLogChannel channel, string message, Object context = null);
        void LogError(DebugLogChannel channel, string message, Object context = null);
        void AddHandler(IDebugLogHandler handler);
        void RemoveHandler(IDebugLogHandler handler);
    }
}
