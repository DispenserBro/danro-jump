using DanroJump.Prizes;
using DanroJump.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class PrizeLevelAddModalWindowTests
    {
        [Test]
        public void KeyboardHorizontalInput_AdjustsFocusedScoreBeforeSave()
        {
            var root = CreateRoot();
            var modal = new PrizeLevelAddModalWindow(root);
            var savedScore = -1;
            modal.Saved += (score, _) => savedScore = score;

            modal.SetInitialScore(100);
            modal.HandleNavigationKey(root.Q<Button>("PrizeLevelAddScorePlusButton"), KeyCode.RightArrow);
            modal.HandleNavigationKey(root.Q<Button>("PrizeLevelAddSaveButton"), KeyCode.Return);

            Assert.That(savedScore, Is.EqualTo(110));
            modal.Dispose();
        }

        [Test]
        public void JoystickSubmit_SavesSelectedPrizeKind()
        {
            var root = CreateRoot();
            var modal = new PrizeLevelAddModalWindow(root);
            PrizeRewardKind savedKind = PrizeRewardKind.None;
            modal.Saved += (_, kind) => savedKind = kind;

            modal.SetInitialScore(100);
            modal.HandleNavigationKey(root.Q<Button>("PrizeLevelAddKindNextButton"), KeyCode.RightArrow);
            modal.HandleNavigationKey(root.Q<Button>("PrizeLevelAddSaveButton"), KeyCode.JoystickButton0);

            Assert.That(savedKind, Is.EqualTo(PrizeRewardKind.Hopper));
            modal.Dispose();
        }

        [Test]
        public void WasdInput_AdjustsFocusedPrizeKind()
        {
            var root = CreateRoot();
            var modal = new PrizeLevelAddModalWindow(root);

            modal.SetInitialScore(100);
            Assert.That(modal.HandleNavigationKey(root.Q<Button>("PrizeLevelAddKindNextButton"), KeyCode.D), Is.True);

            Assert.That(root.Q<Label>("PrizeLevelAddKindValue").text, Is.EqualTo("Хоппер"));
            modal.Dispose();
        }

        private static VisualElement CreateRoot()
        {
            var root = new VisualElement();
            root.Add(new Label { name = "PrizeLevelAddScoreValue" });
            root.Add(new Label { name = "PrizeLevelAddKindValue" });
            root.Add(new Label { name = "PrizeLevelAddStatus" });
            root.Add(new Button { name = "PrizeLevelAddScoreMinusButton" });
            root.Add(new Button { name = "PrizeLevelAddScorePlusButton" });
            root.Add(new Button { name = "PrizeLevelAddKindPreviousButton" });
            root.Add(new Button { name = "PrizeLevelAddKindNextButton" });
            root.Add(new Button { name = "PrizeLevelAddSaveButton" });
            root.Add(new Button { name = "PrizeLevelAddCancelButton" });
            return root;
        }
    }
}
