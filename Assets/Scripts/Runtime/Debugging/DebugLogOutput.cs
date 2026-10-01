using System;

namespace DanroJump.Debugging
{
    /// <summary>
    /// Направления, куда сервис отправляет отладочные сообщения.
    /// </summary>
    [Flags]
    public enum DebugLogOutput
    {
        None = 0,
        UnityConsole = 1 << 0,
        CustomHandlers = 1 << 1,
        All = UnityConsole | CustomHandlers
    }
}
