namespace DanroJump.Gameplay
{
    /// <summary>
    /// Контракт компонентов платформы, которым нужно знать о спавне, рецикле и сбросе платформы.
    /// </summary>
    public interface IPlatformLifecycle
    {
        /// <summary>
        /// Вызывается при первом создании платформы.
        /// </summary>
        void Initialize(PlatformSpawnContext context);

        /// <summary>
        /// Вызывается при переиспользовании платформы выше камеры.
        /// </summary>
        void Recycle(PlatformSpawnContext context);

        /// <summary>
        /// Возвращает компонент платформы к начальному состоянию.
        /// </summary>
        void ResetPlatform();
    }
}
