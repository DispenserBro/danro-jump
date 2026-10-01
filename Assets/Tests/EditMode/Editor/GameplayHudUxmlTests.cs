using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class GameplayHudUxmlTests
    {
        private const string GameplayHudPath = "Assets/UI/Gameplay/GameplayHud.uxml";

        [Test]
        public void GameplayHud_ContainsPrizeAndContinueOverlayElements()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GameplayHudPath);
            Assert.That(asset, Is.Not.Null);

            var root = asset.CloneTree();

            AssertElement<VisualElement>(root, "RightStats");

            AssertElement<VisualElement>(root, "PrizeResultOverlay");
            AssertElement<Label>(root, "PrizeResultTitle");
            AssertElement<Label>(root, "PrizeResultSubtitle");
            AssertElement<Label>(root, "PrizeResultDetails");
            AssertElement<VisualElement>(root, "PrizeQrContainer");
            AssertElement<Image>(root, "PrizeQrImage");
            AssertElement<Label>(root, "PrizeQrPhone");
            AssertElement<Label>(root, "PrizeQrMessage");
            AssertElement<Button>(root, "PrizeResultReturnButton");

            AssertElement<VisualElement>(root, "ContinueGameOverlay");
            AssertElement<Label>(root, "ContinueGameTitle");
            AssertElement<Label>(root, "ContinueGameDetails");
            AssertElement<Button>(root, "ContinueGameButton");
            AssertElement<Button>(root, "ContinueMenuButton");
        }

        private static void AssertElement<T>(VisualElement root, string name)
            where T : VisualElement
        {
            Assert.That(root.Q<T>(name), Is.Not.Null, $"Element '{name}' is missing from {GameplayHudPath}.");
        }
    }
}
