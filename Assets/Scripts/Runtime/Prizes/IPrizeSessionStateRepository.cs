namespace DanroJump.Prizes
{
    /// <summary>
    /// Читает и сохраняет snapshot призовых сессий для восстановления после перезапуска.
    /// </summary>
    public interface IPrizeSessionStateRepository
    {
        bool TryLoad(out PrizeSessionStateSnapshot snapshot);
        bool Save(PrizeSessionStateSnapshot snapshot);
    }
}
