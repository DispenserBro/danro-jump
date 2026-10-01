using System.Reflection;
using DanroJump.UI.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class ContinueGameOverlayWindowTests
    {
        [Test]
        public void SetOffer_ShowsCostAndLives()
        {
            var root = CreateRoot();
            var window = new ContinueGameOverlayWindow(root);

            window.SetOffer(cost: 30, lives: 2);

            Assert.That(root.Q<Label>("ContinueGameTitle").text, Is.EqualTo("Продолжить игру?"));
            Assert.That(root.Q<Label>("ContinueGameDetails").text, Is.EqualTo("Стоимость: 30. Дополнительные жизни: 2."));
            Assert.That(root.Q<Button>("ContinueGameButton").enabledSelf, Is.True);
        }

        [Test]
        public void SetUnavailable_ShowsGameOverAndDisablesContinueButton()
        {
            var root = CreateRoot();
            var window = new ContinueGameOverlayWindow(root);

            window.SetUnavailable();

            Assert.That(root.Q<Label>("ContinueGameTitle").text, Is.EqualTo("Игра окончена"));
            Assert.That(root.Q<Button>("ContinueGameButton").enabledSelf, Is.False);
        }

        [Test]
        public void Buttons_RaiseRequestedEvents()
        {
            var root = CreateRoot();
            var window = new ContinueGameOverlayWindow(root);
            var continueRaised = false;
            var menuRaised = false;
            window.ContinueRequested += () => continueRaised = true;
            window.MenuRequested += () => menuRaised = true;

            typeof(ContinueGameOverlayWindow)
                .GetMethod("HandleContinueClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(window, null);
            typeof(ContinueGameOverlayWindow)
                .GetMethod("HandleMenuClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(window, null);

            Assert.That(continueRaised, Is.True);
            Assert.That(menuRaised, Is.True);
        }

        [Test]
        public void Open_IgnoresImmediateContinueSubmitFromHeldArcadeButton()
        {
            var root = CreateRoot();
            var window = new ContinueGameOverlayWindow(root);
            var continueRaised = false;
            window.ContinueRequested += () => continueRaised = true;

            window.Open();
            typeof(ContinueGameOverlayWindow)
                .GetMethod("HandleContinueClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(window, null);

            Assert.That(continueRaised, Is.False);

            typeof(ContinueGameOverlayWindow)
                .GetField("inputEnabledAtTime", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(window, Time.unscaledTime - 1f);
            typeof(ContinueGameOverlayWindow)
                .GetMethod("HandleContinueClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(window, null);

            Assert.That(continueRaised, Is.True);
        }

        private static VisualElement CreateRoot()
        {
            var root = new VisualElement { name = "ContinueGameOverlay" };
            root.Add(new Label { name = "ContinueGameTitle" });
            root.Add(new Label { name = "ContinueGameDetails" });
            root.Add(new Button { name = "ContinueGameButton" });
            root.Add(new Button { name = "ContinueMenuButton" });
            return root;
        }
    }
}
