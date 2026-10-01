namespace DanroJump.Audio
{
    /// <summary>
    /// Логический канал звука, который выбирает AudioSource и mixer group.
    /// </summary>
    public enum GameAudioChannel
    {
        Sfx = 0,
        Ui = 1,
        Gameplay = 2,
        Music = 3,
        Ambience = 4
    }
}
