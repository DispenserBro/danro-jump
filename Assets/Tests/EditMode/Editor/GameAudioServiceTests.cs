using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DanroJump.Audio;
using DanroJump.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DanroJump.Tests.EditMode
{
    public sealed class GameAudioServiceTests
    {
        private const string AudioSettingsPath = "Assets/Audio/Settings/GameAudioSettings.asset";
        private const string ProjectContextPath = "Assets/Resources/ProjectContext.prefab";

        [Test]
        public void LinearToDecibels_MapsUnitySliderValuesToMixerDecibels()
        {
            Assert.That(GameAudioService.LinearToDecibels(1f), Is.EqualTo(0f).Within(0.001f));
            Assert.That(GameAudioService.LinearToDecibels(0.5f), Is.EqualTo(-6.0206f).Within(0.001f));
            Assert.That(GameAudioService.LinearToDecibels(0f), Is.EqualTo(GameAudioService.MutedDecibels));
        }

        [Test]
        public void Settings_ReturnsBindingForConfiguredEvent()
        {
            var settings = ScriptableObject.CreateInstance<GameAudioSettings>();
            var binding = new GameAudioClipBinding();
            var clip = AudioClip.Create("test-coin", 8, 1, 8000, false);

            TestObjectFactory.SetPrivateField(binding, "eventId", GameAudioEvent.CoinCollected);
            TestObjectFactory.SetPrivateField(binding, "clips", new[] { clip });
            TestObjectFactory.SetPrivateField(settings, "eventBindings", new List<GameAudioClipBinding> { binding });

            try
            {
                Assert.That(settings.TryGetBinding(GameAudioEvent.CoinCollected, out var resolved), Is.True);
                Assert.That(resolved, Is.SameAs(binding));
                Assert.That(resolved.GetRandomClip(), Is.SameAs(clip));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Settings_ResolvesConfiguredMusicCue()
        {
            var settings = ScriptableObject.CreateInstance<GameAudioSettings>();
            var track = new GameMusicTrack();
            var clip = AudioClip.Create("test-gameplay-music", 8, 1, 8000, false);

            TestObjectFactory.SetPrivateField(track, "cue", GameMusicCue.Gameplay);
            TestObjectFactory.SetPrivateField(track, "clip", clip);
            TestObjectFactory.SetPrivateField(settings, "defaultMusicCue", GameMusicCue.Gameplay);
            TestObjectFactory.SetPrivateField(settings, "musicTracks", new List<GameMusicTrack> { track });

            try
            {
                Assert.That(settings.TryGetMusicTrack(GameMusicCue.Gameplay, out var resolved), Is.True);
                Assert.That(resolved, Is.SameAs(track));
                Assert.That(settings.TryGetDefaultMusicTrack(out var defaultTrack), Is.True);
                Assert.That(defaultTrack, Is.SameAs(track));
            }
            finally
            {
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ProjectContext_ReferencesGameAudioSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameAudioSettings>(AudioSettingsPath);
            var projectContext = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectContextPath);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Mixer, Is.Not.Null);
            Assert.That(settings.MusicGroup, Is.Not.Null);
            Assert.That(projectContext, Is.Not.Null);

            var installer = projectContext.GetComponentInChildren<AppServicesInstaller>(true);
            var field = typeof(AppServicesInstaller).GetField(
                "audioSettings",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(installer, Is.Not.Null);
            Assert.That(field?.GetValue(installer), Is.SameAs(settings));
        }

        [Test]
        public void AudioSettings_HasAudibleClipBindingForEveryGameEvent()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameAudioSettings>(AudioSettingsPath);

            Assert.That(settings, Is.Not.Null);

            foreach (GameAudioEvent eventId in System.Enum.GetValues(typeof(GameAudioEvent)))
            {
                Assert.That(settings.TryGetBinding(eventId, out var binding), Is.True, $"{eventId} has no binding.");
                Assert.That(binding.HasClip, Is.True, $"{eventId} has no assigned clip.");
                Assert.That(binding.GetRandomClip(), Is.Not.Null, $"{eventId} cannot resolve a playable clip.");
            }
        }

        [Test]
        public void AudioMixer_RoutesNonMusicGroupsThroughSfxGroup()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameAudioSettings>(AudioSettingsPath);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Mixer, Is.Not.Null);
            Assert.That(settings.MusicGroup, Is.Not.Null);

            var masterChildren = GetMixerGroupChildren(settings.Mixer, "Master");
            var sfxChildren = GetMixerGroupChildren(settings.Mixer, "SFX");

            Assert.That(masterChildren, Is.EquivalentTo(new[] { "Music", "SFX" }));
            Assert.That(sfxChildren, Is.EquivalentTo(new[] { "UI", "Gameplay", "Ambience" }));
        }

        [Test]
        public void ApplyServiceVolumes_UsesConfiguredExposedMixerParameters()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameAudioSettings>(AudioSettingsPath);
            var serviceSettings = new StubReadOnlySettings(0.25f, 0.5f);
            var service = new GameAudioService(settings, serviceSettings);

            Assert.That(HasExposedParameter(settings.Mixer, GameAudioService.MusicVolumeParameter), Is.True);
            Assert.That(HasExposedParameter(settings.Mixer, GameAudioService.SoundsVolumeParameter), Is.True);
            Assert.DoesNotThrow(() => service.ApplyServiceVolumes());
        }

        [Test]
        public void Initialize_CreatesAudioSourcesAndDisposeDestroysRoot()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameAudioSettings>(AudioSettingsPath);
            var service = new GameAudioService(settings, new StubReadOnlySettings(1f, 1f));
            var rootField = typeof(GameAudioService).GetField(
                "root",
                BindingFlags.Instance | BindingFlags.NonPublic);

            service.Initialize();
            service.Play(GameAudioEvent.UiSubmit);
            service.Play(GameAudioEvent.CoinCollected, new Vector3(1f, 2f, 0f));

            var root = (GameObject)rootField.GetValue(service);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.name, Is.EqualTo("Game Audio Service"));
            Assert.That(root.GetComponentsInChildren<AudioSource>(true), Has.Length.GreaterThanOrEqualTo(11));

            service.Dispose();

            Assert.That(root == null, Is.True);
            Assert.That(rootField.GetValue(service), Is.Null);
        }

        [Test]
        public void AudioEmitter_PlaysNamedAnimationEvent()
        {
            var root = new GameObject("Audio Emitter Test");
            var service = new RecordingAudioService();
            var emitter = root.AddComponent<GameAudioEmitter>();

            try
            {
                emitter.Construct(service);
                emitter.PlayByName(nameof(GameAudioEvent.EnemyAttack));
                emitter.PlayGlobalByName(nameof(GameAudioEvent.PlayerHurt));

                Assert.That(service.PlayedEvents, Is.EqualTo(new[]
                {
                    GameAudioEvent.EnemyAttack,
                    GameAudioEvent.PlayerHurt
                }));
                Assert.That(service.PlayedSpatialFlags, Is.EqualTo(new[] { true, false }));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SettingChanged_ReappliesMixerVolumesWithoutThrowing()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameAudioSettings>(AudioSettingsPath);
            var serviceSettings = new StubReadOnlySettings(1f, 1f);
            var service = new GameAudioService(settings, serviceSettings);

            try
            {
                service.Initialize();
                serviceSettings.SetVolumes(0.2f, 0.4f);
                serviceSettings.RaiseSettingChanged(ServiceSettingsKeys.MusicVolume);
                serviceSettings.RaiseSettingChanged(ServiceSettingsKeys.SoundsVolume);

                Assert.That(HasExposedParameter(settings.Mixer, GameAudioService.MusicVolumeParameter), Is.True);
                Assert.That(HasExposedParameter(settings.Mixer, GameAudioService.SoundsVolumeParameter), Is.True);

                settings.Mixer.GetFloat(GameAudioService.MusicVolumeParameter, out float musicDb);
                settings.Mixer.GetFloat(GameAudioService.SoundsVolumeParameter, out float soundsDb);
                Assert.That(musicDb, Is.EqualTo(GameAudioService.LinearToDecibels(0.2f)).Within(0.001f));
                Assert.That(soundsDb, Is.EqualTo(GameAudioService.LinearToDecibels(0.4f)).Within(0.001f));
            }
            finally
            {
                service.Dispose();
                settings.Mixer.SetFloat(GameAudioService.MusicVolumeParameter, 0f);
                settings.Mixer.SetFloat(GameAudioService.SoundsVolumeParameter, 0f);
            }
        }

        private static IReadOnlyList<string> GetMixerGroupChildren(UnityEngine.Audio.AudioMixer mixer, string groupName)
        {
            var group = mixer.FindMatchingGroups(groupName).First(candidate => candidate.name == groupName);
            var childrenProperty = group.GetType().GetProperty(
                "children",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var children = (System.Array)childrenProperty.GetValue(group);
            return children
                .Cast<UnityEngine.Audio.AudioMixerGroup>()
                .Select(child => child.name)
                .ToArray();
        }

        private static bool HasExposedParameter(UnityEngine.Audio.AudioMixer mixer, string parameterName)
        {
            var controllerType = typeof(Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
            var exposedProperty = controllerType.GetProperty(
                "exposedParameters",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var exposed = (System.Array)exposedProperty.GetValue(mixer);
            var elementType = exposed.GetType().GetElementType();
            var nameField = elementType.GetField(
                "name",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return exposed
                .Cast<object>()
                .Any(item => string.Equals((string)nameField.GetValue(item), parameterName, System.StringComparison.Ordinal));
        }

        private sealed class StubReadOnlySettings : IReadOnlyServiceSettings
        {
            private float musicVolume;
            private float soundsVolume;

            public StubReadOnlySettings(float musicVolume, float soundsVolume)
            {
                this.musicVolume = musicVolume;
                this.soundsVolume = soundsVolume;
            }

            public ServiceSettingsDatabase Database => null;

            public event System.Action SettingsLoaded;
            public event System.Action SettingsSaved;
            public event System.Action<string, ServiceSettingValue> SettingChanged;

            public void SetVolumes(float musicVolume, float soundsVolume)
            {
                this.musicVolume = musicVolume;
                this.soundsVolume = soundsVolume;
            }

            public void RaiseSettingChanged(string key)
            {
                SettingChanged?.Invoke(key, ServiceSettingValue.Float(GetFloat(key)));
            }

            public bool TryGetDefinition(string key, out ServiceSettingDefinition definition)
            {
                definition = null;
                return false;
            }

            public IReadOnlyDictionary<string, ServiceSettingValue> Snapshot()
            {
                return new Dictionary<string, ServiceSettingValue>
                {
                    [ServiceSettingsKeys.MusicVolume] = ServiceSettingValue.Float(musicVolume),
                    [ServiceSettingsKeys.SoundsVolume] = ServiceSettingValue.Float(soundsVolume)
                };
            }

            public ServiceSettingValue Get(string key)
            {
                return ServiceSettingValue.Float(GetFloat(key));
            }

            public bool GetBool(string key)
            {
                return false;
            }

            public int GetInt(string key)
            {
                return 0;
            }

            public float GetFloat(string key)
            {
                return key == ServiceSettingsKeys.MusicVolume
                    ? musicVolume
                    : key == ServiceSettingsKeys.SoundsVolume
                        ? soundsVolume
                        : 0f;
            }

            public string GetString(string key)
            {
                return string.Empty;
            }

            public int GetOptionIndex(string key)
            {
                return 0;
            }
        }

        private sealed class RecordingAudioService : IGameAudioService
        {
            public readonly List<GameAudioEvent> PlayedEvents = new();
            public readonly List<bool> PlayedSpatialFlags = new();

            public bool IsReady => true;

            public bool IsMusicPlaying => false;

            public GameMusicCue? CurrentMusicCue => null;

            public void Play(GameAudioEvent eventId)
            {
                PlayedEvents.Add(eventId);
                PlayedSpatialFlags.Add(false);
            }

            public void Play(GameAudioEvent eventId, Vector3 worldPosition)
            {
                PlayedEvents.Add(eventId);
                PlayedSpatialFlags.Add(true);
            }

            public void StartMusic()
            {
            }

            public void PlayMusic(GameMusicCue cue)
            {
            }

            public void StopMusic()
            {
            }

            public void ApplyServiceVolumes()
            {
            }
        }
    }
}
