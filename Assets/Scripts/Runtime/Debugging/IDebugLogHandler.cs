namespace DanroJump.Debugging
{
    /// <summary>
    /// Дополнительный получатель отладочных сообщений.
    /// </summary>
    public interface IDebugLogHandler
    {
        void Handle(DebugLogEntry entry);
    }
}
