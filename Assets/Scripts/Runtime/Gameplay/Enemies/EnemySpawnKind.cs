namespace DanroJump.Gameplay
{
    /// <summary>
    /// Описывает, в каких местах допустимо создавать врага.
    /// </summary>
    public enum EnemySpawnKind
    {
        /// <summary>
        /// Враг живет на платформе и не должен появляться в воздухе сам по себе.
        /// </summary>
        GroundedOnly = 0,

        /// <summary>
        /// Враг появляется только в свободном поле, отдельно от платформ.
        /// </summary>
        FlyingOnly = 1,

        /// <summary>
        /// Враг подходит и для платформенного, и для процедурного полевого спавна.
        /// </summary>
        Universal = 2
    }
}
