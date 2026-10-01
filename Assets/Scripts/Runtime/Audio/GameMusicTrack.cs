using System;
using UnityEngine;
using UnityEngine.Audio;

namespace DanroJump.Audio
{
    /// <summary>
    /// Описывает музыкальный трек для конкретного состояния игры.
    /// </summary>
    [Serializable]
    public sealed class GameMusicTrack
    {
        [SerializeField] private GameMusicCue cue;
        [SerializeField] private AudioClip clip;
        [SerializeField] private AudioMixerGroup outputGroupOverride;
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private bool loop = true;

        public GameMusicCue Cue => cue;

        public AudioClip Clip => clip;

        public AudioMixerGroup OutputGroupOverride => outputGroupOverride;

        public float Volume => Mathf.Clamp01(volume);

        public bool Loop => loop;

        public bool HasClip => clip != null;
    }
}
