using System;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace DanroJump.Debugging
{
    /// <summary>
    /// Единый пакет данных отладочного сообщения для консоли и будущих обработчиков.
    /// </summary>
    public readonly struct DebugLogEntry
    {
        public DebugLogEntry(
            DebugLogChannel channel,
            DebugLogLevel level,
            string message,
            UnityObject context)
        {
            Channel = channel;
            Level = level;
            Message = message;
            Context = context;
            TimestampUtc = DateTime.UtcNow;
        }

        public DebugLogChannel Channel { get; }
        public DebugLogLevel Level { get; }
        public string Message { get; }
        public UnityObject Context { get; }
        public DateTime TimestampUtc { get; }
    }
}
