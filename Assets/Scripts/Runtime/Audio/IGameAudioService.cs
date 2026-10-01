using UnityEngine;

namespace DanroJump.Audio
{
    /// <summary>
    /// Runtime API для проигрывания музыки и событийных звуков игры.
    /// </summary>
    public interface IGameAudioService
    {
        bool IsReady { get; }

        bool IsMusicPlaying { get; }

        GameMusicCue? CurrentMusicCue { get; }

        void Play(GameAudioEvent eventId);

        void Play(GameAudioEvent eventId, Vector3 worldPosition);

        void StartMusic();

        void PlayMusic(GameMusicCue cue);

        void StopMusic();

        void ApplyServiceVolumes();
    }
}
