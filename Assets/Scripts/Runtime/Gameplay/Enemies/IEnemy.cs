namespace DanroJump.Gameplay
{
    /// <summary>
    /// Контракт противника, которого можно активировать, деактивировать и сбрасывать при рецикле платформы.
    /// </summary>
    public interface IEnemy
    {
        /// <summary>
        /// Переводит противника в активное игровое состояние.
        /// </summary>
        void ActivateEnemy();

        /// <summary>
        /// Выключает игровую логику противника.
        /// </summary>
        void DeactivateEnemy();

        /// <summary>
        /// Возвращает противника к начальному состоянию.
        /// </summary>
        void ResetEnemy();
    }
}
