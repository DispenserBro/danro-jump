using DanroJump.UI;
using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class UiTemplateAddressablesTests
    {
        private static readonly (string NewPath, string OldPath, string Address)[] SettingsTemplates =
        {
            (
                "Assets/UI/MainMenu/Templates/SettingsGroupHeader.uxml",
                "Assets/Resources/UI/MainMenu/Templates/SettingsGroupHeader.uxml",
                UiTemplateAddressKeys.SettingsGroupHeader),
            (
                "Assets/UI/MainMenu/Templates/SettingsBoolControl.uxml",
                "Assets/Resources/UI/MainMenu/Templates/SettingsBoolControl.uxml",
                UiTemplateAddressKeys.SettingsBoolControl),
            (
                "Assets/UI/MainMenu/Templates/SettingsStepperControl.uxml",
                "Assets/Resources/UI/MainMenu/Templates/SettingsStepperControl.uxml",
                UiTemplateAddressKeys.SettingsStepperControl),
            (
                "Assets/UI/MainMenu/Templates/SettingsTextControl.uxml",
                "Assets/Resources/UI/MainMenu/Templates/SettingsTextControl.uxml",
                UiTemplateAddressKeys.SettingsTextControl),
            (
                "Assets/UI/MainMenu/Templates/SettingsButtonControl.uxml",
                "Assets/Resources/UI/MainMenu/Templates/SettingsButtonControl.uxml",
                UiTemplateAddressKeys.SettingsButtonControl),
        };

        [Test]
        public void SettingsTemplates_AreStoredUnderUiFolderOnly()
        {
            foreach (var template in SettingsTemplates)
            {
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(template.NewPath),
                    Is.Not.Null,
                    $"Template is missing at {template.NewPath}.");
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(template.OldPath),
                    Is.Null,
                    $"Template should not remain in Resources at {template.OldPath}.");
            }
        }

        [Test]
        public void SettingsTemplates_AreRegisteredInAddressables()
        {
            var groupAssetText = File.ReadAllText("Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset");

            foreach (var template in SettingsTemplates)
            {
                Assert.That(groupAssetText, Does.Contain($"m_Address: {template.Address}"));
            }
        }
    }
}
