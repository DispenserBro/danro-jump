using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DanroJump.Audio
{
    /// <summary>
    /// Настройки звуковой подсистемы: микшер, группы каналов, музыка и таблица событий.
    /// </summary>
    [CreateAssetMenu(menuName = "Danro Jump/Audio/Game Audio Settings", fileName = "GameAudioSettings")]
    public sealed class GameAudioSettings : ScriptableObject
    {
        [Header("Mixer")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup uiGroup;
        [SerializeField] private AudioMixerGroup gameplayGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;

        [Header("Music")]
        [SerializeField] private AudioClip musicLoop;
        [SerializeField] [Range(0f, 1f)] private float musicLoopVolume = 1f;
        [SerializeField] private bool playMusicOnInitialize = true;
        [SerializeField] private GameMusicCue defaultMusicCue = GameMusicCue.Menu;
        [SerializeField] private List<GameMusicTrack> musicTracks = new();

        [Header("Events")]
        [SerializeField] private List<GameAudioClipBinding> eventBindings = new();

        public AudioMixer Mixer => mixer;

        public AudioMixerGroup MusicGroup => musicGroup;

        public AudioClip MusicLoop => musicLoop;

        public float MusicLoopVolume => Mathf.Clamp01(musicLoopVolume);

        public bool PlayMusicOnInitialize => playMusicOnInitialize;

        public GameMusicCue DefaultMusicCue => defaultMusicCue;

        public IReadOnlyList<GameMusicTrack> MusicTracks => musicTracks;

        public IReadOnlyList<GameAudioClipBinding> EventBindings => eventBindings;

        public bool TryGetBinding(GameAudioEvent eventId, out GameAudioClipBinding binding)
        {
            if (eventBindings != null)
            {
                for (var index = 0; index < eventBindings.Count; index++)
                {
                    var candidate = eventBindings[index];
                    if (candidate != null && candidate.EventId == eventId)
                    {
                        binding = candidate;
                        return true;
                    }
                }
            }

            binding = null;
            return false;
        }

        public bool TryGetMusicTrack(GameMusicCue cue, out GameMusicTrack track)
        {
            if (musicTracks != null)
            {
                for (var index = 0; index < musicTracks.Count; index++)
                {
                    var candidate = musicTracks[index];
                    if (candidate != null && candidate.Cue == cue && candidate.HasClip)
                    {
                        track = candidate;
                        return true;
                    }
                }
            }

            track = null;
            return false;
        }

        public bool TryGetDefaultMusicTrack(out GameMusicTrack track)
        {
            if (TryGetMusicTrack(defaultMusicCue, out track))
            {
                return true;
            }

            if (musicTracks != null)
            {
                for (var index = 0; index < musicTracks.Count; index++)
                {
                    var candidate = musicTracks[index];
                    if (candidate != null && candidate.HasClip)
                    {
                        track = candidate;
                        return true;
                    }
                }
            }

            track = null;
            return false;
        }

        public AudioMixerGroup ResolveGroup(GameAudioChannel channel)
        {
            return channel switch
            {
                GameAudioChannel.Music => musicGroup,
                GameAudioChannel.Ui => uiGroup != null ? uiGroup : sfxGroup,
                GameAudioChannel.Gameplay => gameplayGroup != null ? gameplayGroup : sfxGroup,
                GameAudioChannel.Ambience => ambienceGroup != null ? ambienceGroup : sfxGroup,
                _ => sfxGroup
            };
        }
    }
}
