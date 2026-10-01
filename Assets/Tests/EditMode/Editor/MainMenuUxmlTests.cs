using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class MainMenuUxmlTests
    {
        private const string MainMenuPath = "Assets/UI/MainMenu/MainMenu.uxml";
        private const string MainMenuUssPath = "Assets/UI/MainMenu/MainMenu.uss";
        private const string MainMenuModalsPath = "Assets/UI/MainMenu/Templates/MainMenuModals.uxml";

        [Test]
        public void MainMenu_ContainsModalTemplateElements()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainMenuPath);
            Assert.That(asset, Is.Not.Null);

            var root = asset.CloneTree();

            AssertElement<VisualElement>(root, "ModalLayer");
            AssertElement<VisualElement>(root, "ModalBackdrop");
            AssertElement<VisualElement>(root, "RulesModal");
            AssertElement<Button>(root, "RulesModalCloseButton");
            AssertElement<VisualElement>(root, "SettingsModal");
            AssertElement<ScrollView>(root, "SettingsList");
            AssertElement<Button>(root, "SettingsSaveButton");
            AssertElement<Button>(root, "SettingsCloseButton");
            AssertElement<VisualElement>(root, "ServiceInfoModal");
            AssertElement<Label>(root, "ServiceInfoTitle");
            AssertElement<Label>(root, "ServiceInfoContent");
            AssertElement<Button>(root, "ServiceInfoCloseButton");
            AssertElement<VisualElement>(root, "ServiceTextInputModal");
            AssertElement<Label>(root, "ServiceTextInputTitle");
            AssertElement<Label>(root, "ServiceTextInputSetting");
            AssertElement<Label>(root, "ServiceTextInputPreview");
            AssertElement<VisualElement>(root, "ServiceTextInputSlots");
            AssertElement<Label>(root, "ServiceTextInputSymbol");
            AssertElement<Label>(root, "ServiceTextInputStatus");
            AssertElement<Button>(root, "ServiceTextInputConfirmButton");
            AssertElement<Button>(root, "ServiceTextInputDeleteButton");
            AssertElement<Button>(root, "ServiceTextInputCancelButton");
            AssertElement<VisualElement>(root, "QrSettingsModal");
            AssertElement<Button>(root, "QrSettingsTierPreviousButton");
            AssertElement<Label>(root, "QrSettingsTierValue");
            AssertElement<Button>(root, "QrSettingsTierNextButton");
            AssertElement<Label>(root, "QrSettingsPrizeTextValue");
            AssertElement<Button>(root, "QrSettingsEditTextButton");
            AssertElement<Label>(root, "QrSettingsStatus");
            AssertElement<Button>(root, "QrSettingsCloseButton");
            AssertElement<VisualElement>(root, "PrizeLevelsModal");
            AssertElement<ScrollView>(root, "PrizeLevelsList");
            AssertElement<Label>(root, "PrizeLevelsStatus");
            AssertElement<Button>(root, "PrizeLevelsAddButton");
            AssertElement<Button>(root, "PrizeLevelsCloseButton");
            AssertElement<VisualElement>(root, "PrizeLevelAddModal");
            AssertElement<Button>(root, "PrizeLevelAddScoreMinusButton");
            AssertElement<Label>(root, "PrizeLevelAddScoreValue");
            AssertElement<Button>(root, "PrizeLevelAddScorePlusButton");
            AssertElement<Button>(root, "PrizeLevelAddKindPreviousButton");
            AssertElement<Label>(root, "PrizeLevelAddKindValue");
            AssertElement<Button>(root, "PrizeLevelAddKindNextButton");
            AssertElement<Button>(root, "PrizeLevelAddSaveButton");
            AssertElement<Button>(root, "PrizeLevelAddCancelButton");
            AssertElement<Label>(root, "SupportPhoneText");
        }

        [Test]
        public void MainMenu_ModalButtonsHaveVisibleFocusTint()
        {
            var uss = File.ReadAllText(MainMenuUssPath);

            Assert.That(uss, Does.Contain(".modal-button:focus"));
            Assert.That(uss, Does.Contain(".modal-button--focused"));
            Assert.That(uss, Does.Contain("-unity-background-image-tint-color: rgb(206, 255, 26);"));
        }

        [Test]
        public void MainMenuModals_TemplateLoadsGameStylesheetForStandalonePreview()
        {
            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainMenuModalsPath);
            var uxml = File.ReadAllText(MainMenuModalsPath);

            Assert.That(template, Is.Not.Null);
            Assert.That(uxml, Does.Contain("<Style src="));
            Assert.That(uxml, Does.Contain("MainMenu.uss"));
        }

        private static void AssertElement<T>(VisualElement root, string name)
            where T : VisualElement
        {
            Assert.That(root.Q<T>(name), Is.Not.Null, $"Element '{name}' is missing from {MainMenuPath}.");
        }
    }
}
