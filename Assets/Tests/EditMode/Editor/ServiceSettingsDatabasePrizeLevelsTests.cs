using DanroJump.Settings;
using NUnit.Framework;
using UnityEditor;

namespace DanroJump.Tests.EditMode
{
    public sealed class ServiceSettingsDatabasePrizeLevelsTests
    {
        private const string DatabasePath = "Assets/Settings/ServiceSettingsDatabase.asset";

        [Test]
        public void ServiceSettingsDatabase_ContainsPrizeLevelsStorageKey()
        {
            var database = AssetDatabase.LoadAssetAtPath<ServiceSettingsDatabase>(DatabasePath);

            Assert.That(database, Is.Not.Null);
            Assert.That(database.TryFind(ServiceSettingsKeys.PrizeLevels, out var definition), Is.True);
            Assert.That(definition.ValueType, Is.EqualTo(ServiceSettingValueType.String));
            Assert.That(definition.ShownInServiceMenu, Is.False);
            Assert.That(definition.DefaultValue.stringValue, Is.EqualTo("{\"levels\":[]}"));
        }

        [Test]
        public void ServiceSettingsDatabase_ContainsQrMessageTemplateStorageKey()
        {
            var database = AssetDatabase.LoadAssetAtPath<ServiceSettingsDatabase>(DatabasePath);

            Assert.That(database, Is.Not.Null);
            Assert.That(database.TryFind(ServiceSettingsKeys.QrMessageTemplate, out var definition), Is.True);
            Assert.That(definition.ValueType, Is.EqualTo(ServiceSettingValueType.String));
            Assert.That(definition.ShownInServiceMenu, Is.False);
            Assert.That(definition.DefaultValue.stringValue, Is.EqualTo("{0}"));
        }

        [Test]
        public void ServiceSettingsDatabase_ContainsQrPrizeTextStorageKeys()
        {
            var database = AssetDatabase.LoadAssetAtPath<ServiceSettingsDatabase>(DatabasePath);

            Assert.That(database, Is.Not.Null);
            AssertQrTextDefinition(database, ServiceSettingsKeys.QrPrizeTextBronze);
            AssertQrTextDefinition(database, ServiceSettingsKeys.QrPrizeTextSilver);
            AssertQrTextDefinition(database, ServiceSettingsKeys.QrPrizeTextGold);
        }

        [Test]
        public void ServiceSettingsDatabase_ContainsVisibleQrSettingsButton()
        {
            var database = AssetDatabase.LoadAssetAtPath<ServiceSettingsDatabase>(DatabasePath);

            Assert.That(database, Is.Not.Null);
            Assert.That(database.TryFind(ServiceSettingsKeys.QrSettingsButton, out var definition), Is.True);
            Assert.That(definition.ValueType, Is.EqualTo(ServiceSettingValueType.Button));
            Assert.That(definition.ShownInServiceMenu, Is.True);
        }

        [Test]
        public void ServiceSettingsDatabase_VolumeDefaultsAreAudible()
        {
            var database = AssetDatabase.LoadAssetAtPath<ServiceSettingsDatabase>(DatabasePath);

            Assert.That(database, Is.Not.Null);
            Assert.That(database.TryFind(ServiceSettingsKeys.MusicVolume, out var music), Is.True);
            Assert.That(database.TryFind(ServiceSettingsKeys.SoundsVolume, out var sounds), Is.True);
            Assert.That(music.DefaultValue.floatValue, Is.EqualTo(1f));
            Assert.That(sounds.DefaultValue.floatValue, Is.EqualTo(1f));
        }

        private static void AssertQrTextDefinition(ServiceSettingsDatabase database, string key)
        {
            Assert.That(database.TryFind(key, out var definition), Is.True);
            Assert.That(definition.ValueType, Is.EqualTo(ServiceSettingValueType.String));
            Assert.That(definition.ShownInServiceMenu, Is.False);
            Assert.That(definition.DefaultValue.stringValue, Is.Empty);
            Assert.That(definition.MaxInt, Is.EqualTo(12));
        }
    }
}
