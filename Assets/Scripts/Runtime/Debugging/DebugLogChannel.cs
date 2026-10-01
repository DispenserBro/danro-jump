using System;

namespace DanroJump.Debugging
{
    /// <summary>
    /// Каналы отладочного вывода, которые можно включать и выключать независимо.
    /// </summary>
    [Flags]
    public enum DebugLogChannel
    {
        None = 0,
        General = 1 << 0,
        ComLifecycle = 1 << 1,
        ComInput = 1 << 2,
        ComAdc = 1 << 3,
        ComRaw = 1 << 4,
        ComPrize = 1 << 5,
        ComCredits = 1 << 6,
        ComHopper = 1 << 7,
        Gameplay = 1 << 8,
        UI = 1 << 9,
        Settings = 1 << 10,
        All = ~0
    }
}
