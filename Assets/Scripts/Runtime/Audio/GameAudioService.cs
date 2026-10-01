using System;
using System.Collections.Generic;
using DanroJump.Settings;
using UnityEngine;
using UnityEngine.Audio;
using Zenject;
using Object = UnityEngine.Object;

namespace DanroJump.Audio
{
    /// <summary>
    /// Централизованный проигрыватель звуков: применяет service-volume, routes events в mixer groups и держит источники звука.
    /// </summary>
    public sealed class GameAudioService : IGameAudioService, IInitializable, IDisposable
    {
        public const string MusicVolumeParameter = "MusicVolume";
        public const string SoundsVolumeParameter = "SoundsVolume";
        public const float MutedDecibels = -80f;
        private const int SpatialSourcePoolSize = 8;

        private readonly GameAudioSettings settings;
        private readonly IReadOnlyServiceSettings serviceSettings;
        private readonly List<AudioSource> spatialSources = new();
        private GameObject root;
        private AudioSource musicSource;
        private AudioSource uiSource;
        private AudioSource gameplaySource;
        private int nextSpatialSourceIndex;
        private GameMusicCue? currentMusicCue;
        private bool subscribedToSettings;

        public GameAudioService(
            [InjectOptional] GameAudioSettings settings = null,
            [InjectOptional] IReadOnlyServiceSettings serviceSettings = null)
        {
            this.settings = settings;
            this.serviceSettings = serviceSettings;
        }

        public bool IsReady => HasPlayableConfiguration();

        public bool IsMusicPlaying => musicSource != null && musicSource.isPlaying;

        public GameMusicCue? CurrentMusicCue => currentMusicCue;

        public void Initialize()
        {
            if (!IsReady)
            {
                Debug.LogWarning($"{nameof(GameAudioService)} has no complete playable audio configuration.");
            }

            EnsureSources();
            SubscribeToSettings();
            ApplyServiceVolumes();
            if (settings == null || settings.PlayMusicOnInitialize)
            {
                StartMusic();
            }
        }

        public void Dispose()
        {
            UnsubscribeFromSettings();

            if (root != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }

                root = null;
            }
        }

        public void Play(GameAudioEvent eventId)
        {
            PlayInternal(eventId, null);
        }

        public void Play(GameAudioEvent eventId, Vector3 worldPosition)
        {
            PlayInternal(eventId, worldPosition);
        }

        public void StartMusic()
        {
            if (settings == null)
            {
                return;
            }

            if (settings.TryGetDefaultMusicTrack(out var track))
            {
                currentMusicCue = track.Cue;
                PlayMusicTrack(track);
                return;
            }

            StartLegacyMusicLoop();
        }

        public void PlayMusic(GameMusicCue cue)
        {
            if (settings == null || !settings.TryGetMusicTrack(cue, out var track))
            {
                return;
            }

            currentMusicCue = cue;
            PlayMusicTrack(track);
        }

        public void StopMusic()
        {
            if (musicSource != null)
            {
                musicSource.Stop();
                musicSource.clip = null;
                currentMusicCue = null;
            }
        }

        public void ApplyServiceVolumes()
        {
            if (settings?.Mixer == null)
            {
                UnityEngine.Debug.LogWarning("[GameAudioService] Cannot apply volumes: settings or Mixer is null.");
                return;
            }

            var musicVolume = serviceSettings != null ? serviceSettings.GetFloat(ServiceSettingsKeys.MusicVolume) : 1f;
            var soundsVolume = serviceSettings != null ? serviceSettings.GetFloat(ServiceSettingsKeys.SoundsVolume) : 1f;

            float musicDb = LinearToDecibels(musicVolume);
            float soundsDb = LinearToDecibels(soundsVolume);

            bool musicOk = settings.Mixer.SetFloat(MusicVolumeParameter, musicDb);
            bool soundsOk = settings.Mixer.SetFloat(SoundsVolumeParameter, soundsDb);

            UnityEngine.Debug.Log($"[GameAudioService] Applied volumes: Music = {musicVolume * 100:0}% ({musicDb:0.##} dB) [status: {musicOk}], Sounds = {soundsVolume * 100:0}% ({soundsDb:0.##} dB) [status: {soundsOk}]");
        }

        public static float LinearToDecibels(float value)
        {
            var clamped = Mathf.Clamp01(value);
            return clamped <= 0.0001f ? MutedDecibels : Mathf.Log10(clamped) * 20f;
        }

        private void StartLegacyMusicLoop()
        {
            if (settings == null || settings.MusicLoop == null)
            {
                return;
            }

            EnsureSources();
            musicSource.clip = settings.MusicLoop;
            musicSource.loop = true;
            musicSource.volume = settings.MusicLoopVolume;
            musicSource.pitch = 1f;
            musicSource.outputAudioMixerGroup = settings.MusicGroup;
            currentMusicCue = null;

            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        private void PlayMusicTrack(GameMusicTrack track)
        {
            if (track == null || track.Clip == null)
            {
                return;
            }

            EnsureSources();
            if (musicSource.isPlaying && musicSource.clip == track.Clip)
            {
                musicSource.volume = track.Volume;
                musicSource.loop = track.Loop;
                musicSource.outputAudioMixerGroup = track.OutputGroupOverride != null
                    ? track.OutputGroupOverride
                    : settings.MusicGroup;
                return;
            }

            musicSource.Stop();
            musicSource.clip = track.Clip;
            musicSource.loop = track.Loop;
            musicSource.volume = track.Volume;
            musicSource.pitch = 1f;
            musicSource.outputAudioMixerGroup = track.OutputGroupOverride != null
                ? track.OutputGroupOverride
                : settings.MusicGroup;
            musicSource.Play();
        }

        private void PlayInternal(GameAudioEvent eventId, Vector3? worldPosition)
        {
            if (settings == null || !settings.TryGetBinding(eventId, out var binding))
            {
                return;
            }

            var clip = binding.GetRandomClip();
            if (clip == null)
            {
                return;
            }

            EnsureSources();
            var source = ResolveSource(binding, worldPosition);
            source.outputAudioMixerGroup = binding.OutputGroupOverride != null
                ? binding.OutputGroupOverride
                : settings.ResolveGroup(binding.Channel);
            source.pitch = binding.RandomPitch;
            source.spatialBlend = binding.Spatial || worldPosition.HasValue ? 1f : 0f;

            if (worldPosition.HasValue)
            {
                source.transform.position = worldPosition.Value;
            }

            source.PlayOneShot(clip, binding.Volume);
        }

        private AudioSource ResolveSource(GameAudioClipBinding binding, Vector3? worldPosition)
        {
            if (binding.Spatial || worldPosition.HasValue)
            {
                return ResolveSpatialSource();
            }

            return binding.Channel switch
            {
                GameAudioChannel.Music => musicSource,
                GameAudioChannel.Ui => uiSource,
                _ => gameplaySource
            };
        }

        private void EnsureSources()
        {
            if (root != null)
            {
                return;
            }

            root = new GameObject("Game Audio Service");
            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(root);
            }

            musicSource = CreateSource("Music", GameAudioChannel.Music);
            uiSource = CreateSource("UI", GameAudioChannel.Ui);
            gameplaySource = CreateSource("Gameplay", GameAudioChannel.Gameplay);
            for (var index = 0; index < SpatialSourcePoolSize; index++)
            {
                var spatialSource = CreateSource($"Spatial {index + 1}", GameAudioChannel.Gameplay);
                spatialSource.spatialBlend = 1f;
                spatialSources.Add(spatialSource);
            }
        }

        private AudioSource ResolveSpatialSource()
        {
            if (spatialSources.Count == 0)
            {
                return gameplaySource;
            }

            var source = spatialSources[nextSpatialSourceIndex % spatialSources.Count];
            nextSpatialSourceIndex = (nextSpatialSourceIndex + 1) % spatialSources.Count;
            return source;
        }

        private AudioSource CreateSource(string sourceName, GameAudioChannel channel)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(root.transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.outputAudioMixerGroup = settings != null ? settings.ResolveGroup(channel) : null;
            return source;
        }

        private bool HasPlayableConfiguration()
        {
            if (settings == null ||
                settings.Mixer == null ||
                settings.MusicGroup == null ||
                settings.ResolveGroup(GameAudioChannel.Sfx) == null ||
                settings.ResolveGroup(GameAudioChannel.Ui) == null ||
                settings.ResolveGroup(GameAudioChannel.Gameplay) == null ||
                settings.ResolveGroup(GameAudioChannel.Ambience) == null)
            {
                return false;
            }

            var bindings = settings.EventBindings;
            if (bindings == null)
            {
                return false;
            }

            for (var index = 0; index < bindings.Count; index++)
            {
                if (bindings[index] != null && bindings[index].HasClip)
                {
                    return true;
                }
            }

            return false;
        }

        private void SubscribeToSettings()
        {
            if (subscribedToSettings || serviceSettings == null)
            {
                return;
            }

            serviceSettings.SettingsLoaded += HandleSettingsLoaded;
            serviceSettings.SettingChanged += HandleSettingChanged;
            subscribedToSettings = true;
        }

        private void UnsubscribeFromSettings()
        {
            if (!subscribedToSettings || serviceSettings == null)
            {
                subscribedToSettings = false;
                return;
            }

            serviceSettings.SettingsLoaded -= HandleSettingsLoaded;
            serviceSettings.SettingChanged -= HandleSettingChanged;
            subscribedToSettings = false;
        }

        private void HandleSettingsLoaded()
        {
            ApplyServiceVolumes();
        }

        private void HandleSettingChanged(string key, ServiceSettingValue value)
        {
            if (string.Equals(key, ServiceSettingsKeys.MusicVolume, StringComparison.Ordinal) ||
                string.Equals(key, ServiceSettingsKeys.SoundsVolume, StringComparison.Ordinal))
            {
                ApplyServiceVolumes();
            }
        }
    }
}
