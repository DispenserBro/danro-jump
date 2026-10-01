using System.Collections.Generic;
using DanroJump.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DanroJump.Tests.EditMode
{
    public sealed class ServiceSettingsServiceTests
    {
        private readonly List<Object> createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in createdAssets)
            {
                if (asset != null)
                {
                    Object.DestroyImmediate(asset);
                }
            }

            createdAssets.Clear();
        }

        [Test]
        public void Load_SanitizesSavedValuesAndFillsMissingDefaults()
        {
            var lives = TestObjectFactory.CreateSettingDefinition(
                "gameplay.lives",
                ServiceSettingValueType.Int,
                ServiceSettingValue.Int(3),
                minInt: 1,
                maxInt: 5);
            var sound = TestObjectFactory.CreateSettingDefinition(
                "audio.enabled",
                ServiceSettingValueType.Bool,
                ServiceSettingValue.Bool(true));
            var button = TestObjectFactory.CreateSettingDefinition(
                "actions.reset",
                ServiceSettingValueType.Button,
                ServiceSettingValue.Button());
            var database = Track(TestObjectFactory.CreateSettingsDatabase(lives, sound, button));
            var repository = new InMemoryRepository(new Dictionary<string, ServiceSettingValue>
            {
                ["gameplay.lives"] = ServiceSettingValue.Int(99),
                ["unknown"] = ServiceSettingValue.Int(7),
            });
            var applier = new RecordingApplier();
            var service = new ServiceSettingsService(database, repository, applier);

            service.Load();

            Assert.That(service.GetInt("gameplay.lives"), Is.EqualTo(5));
            Assert.That(service.GetBool("audio.enabled"), Is.True);
            Assert.That(service.Snapshot(), Does.Not.ContainKey("actions.reset"));
            Assert.That(service.Snapshot(), Does.Not.ContainKey("unknown"));
            Assert.That(applier.AppliedKeys, Is.EquivalentTo(new[] { "gameplay.lives", "audio.enabled" }));
        }

        [Test]
        public void SetExternal_OnlyAllowsExplicitlyExposedDefinitions()
        {
            var internalSetting = TestObjectFactory.CreateSettingDefinition(
                "internal.speed",
                ServiceSettingValueType.Float,
                ServiceSettingValue.Float(1f),
                exposedForExternalChanges: false,
                minFloat: 0.5f,
                maxFloat: 2f);
            var externalSetting = TestObjectFactory.CreateSettingDefinition(
                "public.volume",
                ServiceSettingValueType.Float,
                ServiceSettingValue.Float(1f),
                exposedForExternalChanges: true,
                minFloat: 0f,
                maxFloat: 1f);
            var database = Track(TestObjectFactory.CreateSettingsDatabase(internalSetting, externalSetting));
            var service = new ServiceSettingsService(database, new InMemoryRepository());
            service.Load();

            LogAssert.Expect(LogType.Warning, "[ServiceSettingsService] Setting 'internal.speed' is not exposed for external changes.");
            Assert.That(service.SetExternal("internal.speed", ServiceSettingValue.Float(1.5f)), Is.False);
            Assert.That(service.SetExternal("public.volume", ServiceSettingValue.Float(5f)), Is.True);

            Assert.That(service.GetFloat("internal.speed"), Is.EqualTo(1f));
            Assert.That(service.GetFloat("public.volume"), Is.EqualTo(1f));
        }

        [Test]
        public void Save_PassesCurrentSnapshotToRepositoryAndRaisesEvent()
        {
            var setting = TestObjectFactory.CreateSettingDefinition(
                "credits.price",
                ServiceSettingValueType.Int,
                ServiceSettingValue.Int(10),
                minInt: 1,
                maxInt: 100);
            var database = Track(TestObjectFactory.CreateSettingsDatabase(setting));
            var repository = new InMemoryRepository();
            var service = new ServiceSettingsService(database, repository);
            var saveEvents = 0;
            service.SettingsSaved += () => saveEvents++;

            service.Load();
            service.SetInt("credits.price", 25);
            service.Save();

            Assert.That(saveEvents, Is.EqualTo(1));
            Assert.That(repository.LastSaved["credits.price"].intValue, Is.EqualTo(25));
        }

        private T Track<T>(T asset) where T : Object
        {
            createdAssets.Add(asset);
            return asset;
        }

        private sealed class InMemoryRepository : IServiceSettingsRepository
        {
            private readonly Dictionary<string, ServiceSettingValue> loadedValues;

            public InMemoryRepository(Dictionary<string, ServiceSettingValue> loadedValues = null)
            {
                this.loadedValues = loadedValues ?? new Dictionary<string, ServiceSettingValue>();
            }

            public string Path => "memory";
            public bool Exists => loadedValues.Count > 0;
            public Dictionary<string, ServiceSettingValue> LastSaved { get; private set; }

            public bool TryLoad(out Dictionary<string, ServiceSettingValue> values)
            {
                values = new Dictionary<string, ServiceSettingValue>(loadedValues);
                return true;
            }

            public bool Save(IReadOnlyDictionary<string, ServiceSettingValue> values)
            {
                LastSaved = new Dictionary<string, ServiceSettingValue>(values);
                return true;
            }
        }

        private sealed class RecordingApplier : IServiceSettingApplier
        {
            public readonly List<string> AppliedKeys = new();

            public void Apply(ServiceSettingDefinition definition, ServiceSettingValue value)
            {
                AppliedKeys.Add(definition.Key);
            }
        }
    }
}
