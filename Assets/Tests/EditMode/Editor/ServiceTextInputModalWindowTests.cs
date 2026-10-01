using DanroJump.Settings;
using DanroJump.UI.ArcadeInput;
using DanroJump.UI.MainMenu;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanroJump.Tests.EditMode
{
    public sealed class ServiceTextInputModalWindowTests
    {
        [Test]
        public void SetContext_BuildsSlotsAndPreview()
        {
            var root = CreateRoot();
            var modal = new ServiceTextInputModalWindow(root);
            var definition = CreateStringDefinition("payment.supportNumber");

            modal.SetContext(definition, "+799", ArcadeStringInputProfile.Phone(maxLength: 5), _ => { });

            Assert.That(root.Q<Label>("ServiceTextInputSetting").text, Is.EqualTo("payment.supportNumber"));
            Assert.That(root.Q<Label>("ServiceTextInputPreview").text, Is.EqualTo("+799"));
            Assert.That(root.Q<VisualElement>("ServiceTextInputSlots").childCount, Is.EqualTo(5));

            modal.Dispose();
        }

        [Test]
        public void Confirm_InvokesCallbackOnlyOnce()
        {
            var root = CreateRoot();
            var modal = new ServiceTextInputModalWindow(root);
            var definition = CreateStringDefinition("payment.supportNumber");
            var calls = 0;
            var confirmedValue = "";

            modal.SetContext(
                definition,
                "+799",
                ArcadeStringInputProfile.Phone(maxLength: 5),
                value =>
                {
                    calls++;
                    confirmedValue = value;
                });

            Assert.That(modal.Confirm(), Is.True);
            Assert.That(modal.Confirm(), Is.False);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(confirmedValue, Is.EqualTo("+799"));

            modal.Dispose();
        }

        [Test]
        public void DeleteCurrentCharacter_ClearsSelectedSlotBeforeConfirm()
        {
            var root = CreateRoot();
            var modal = new ServiceTextInputModalWindow(root);
            var definition = CreateStringDefinition("payment.supportNumber");
            var confirmedValue = "";

            modal.SetContext(
                definition,
                "79",
                ArcadeStringInputProfile.Phone(maxLength: 3),
                value => confirmedValue = value);

            modal.HandleNavigationKey(root, KeyCode.RightArrow);
            modal.DeleteCurrentCharacter();
            modal.Confirm();

            Assert.That(confirmedValue, Is.EqualTo("7"));

            modal.Dispose();
        }

        [Test]
        public void KeyboardSubmit_MovesAcrossCharactersAndConfirmsOnAction()
        {
            var root = CreateRoot();
            var modal = new ServiceTextInputModalWindow(root);
            var definition = CreateStringDefinition("payment.supportNumber");
            var confirmedValue = "";

            modal.SetContext(
                definition,
                "79",
                ArcadeStringInputProfile.Phone(maxLength: 2),
                value => confirmedValue = value);

            modal.HandleNavigationKey(root, KeyCode.Return);
            modal.HandleNavigationKey(root, KeyCode.Return);
            modal.HandleNavigationKey(root, KeyCode.Return);

            Assert.That(confirmedValue, Is.EqualTo("79"));

            modal.Dispose();
        }

        [Test]
        public void NavigationSubmit_DoesNotMoveTwiceInSameFrame()
        {
            var root = CreateRoot();
            var modal = new ServiceTextInputModalWindow(root);
            var definition = CreateStringDefinition("payment.supportNumber");

            modal.SetContext(
                definition,
                "+79",
                ArcadeStringInputProfile.Phone(maxLength: 3),
                _ => { });

            InvokeNavigationSubmit(modal);
            InvokeNavigationSubmit(modal);

            Assert.That(GetActiveSlotText(root), Is.EqualTo("7"));

            modal.Dispose();
        }

        [Test]
        public void NavigationSubmit_DoesNotMoveTwiceInsideDebounceWindow()
        {
            var root = CreateRoot();
            var modal = new ServiceTextInputModalWindow(root);
            var definition = CreateStringDefinition("payment.supportNumber");

            modal.SetContext(
                definition,
                "+79",
                ArcadeStringInputProfile.Phone(maxLength: 3),
                _ => { });

            InvokeNavigationSubmit(modal);
            SetPrivateField(modal, "lastNavigationSubmitFrame", -1);
            SetPrivateField(modal, "lastNavigationSubmitTime", Time.unscaledTime);
            InvokeNavigationSubmit(modal);

            Assert.That(GetActiveSlotText(root), Is.EqualTo("7"));

            modal.Dispose();
        }

        [Test]
        public void NavigationMoveRight_DoesNotSkipSlotInSameFrame()
        {
            var root = CreateRoot();
            var modal = new ServiceTextInputModalWindow(root);
            var definition = CreateStringDefinition("payment.supportNumber");

            modal.SetContext(
                definition,
                "+79",
                ArcadeStringInputProfile.Phone(maxLength: 3),
                _ => { });

            modal.HandleNavigationMove(root, NavigationMoveEvent.Direction.Right);
            modal.HandleNavigationMove(root, NavigationMoveEvent.Direction.Right);

            Assert.That(GetActiveSlotText(root), Is.EqualTo("7"));

            modal.Dispose();
        }

        [Test]
        public void NavigationMoveRight_DoesNotSkipSlotInsideDebounceWindow()
        {
            var root = CreateRoot();
            var modal = new ServiceTextInputModalWindow(root);
            var definition = CreateStringDefinition("payment.supportNumber");

            modal.SetContext(
                definition,
                "+79",
                ArcadeStringInputProfile.Phone(maxLength: 3),
                _ => { });

            modal.HandleNavigationMove(root, NavigationMoveEvent.Direction.Right);
            SetPrivateField(modal, "lastDirectionalStepFrame", -1);
            SetPrivateField(modal, "lastDirectionalStepBlockedUntil", Time.unscaledTime + 1f);
            modal.HandleNavigationMove(root, NavigationMoveEvent.Direction.Right);

            Assert.That(GetActiveSlotText(root), Is.EqualTo("7"));

            modal.Dispose();
        }

        [Test]
        public void KeyboardMoveRight_DoesNotSkipSlotInsideDirectionalGate()
        {
            var root = CreateRoot();
            var modal = new ServiceTextInputModalWindow(root);
            var definition = CreateStringDefinition("payment.supportNumber");

            modal.SetContext(
                definition,
                "+79",
                ArcadeStringInputProfile.Phone(maxLength: 3),
                _ => { });

            modal.HandleNavigationKey(root, KeyCode.RightArrow);
            modal.HandleNavigationKey(root, KeyCode.RightArrow);

            Assert.That(GetActiveSlotText(root), Is.EqualTo("7"));

            modal.Dispose();
        }

        [Test]
        public void KeyboardMoveRight_CanRepeatAfterDirectionalGateExpires()
        {
            var root = CreateRoot();
            var modal = new ServiceTextInputModalWindow(root);
            var definition = CreateStringDefinition("payment.supportNumber");

            modal.SetContext(
                definition,
                "+79",
                ArcadeStringInputProfile.Phone(maxLength: 3),
                _ => { });

            modal.HandleNavigationKey(root, KeyCode.RightArrow);
            SetPrivateField(modal, "lastDirectionalStepFrame", -1);
            SetPrivateField(modal, "lastDirectionalStepBlockedUntil", Time.unscaledTime - 1f);
            modal.HandleNavigationKey(root, KeyCode.RightArrow);

            Assert.That(GetActiveSlotText(root), Is.EqualTo("9"));

            modal.Dispose();
        }

        [Test]
        public void KeyboardAndNavigationMoveRight_DoNotCombineIntoDoubleStep()
        {
            var root = CreateRoot();
            var modal = new ServiceTextInputModalWindow(root);
            var definition = CreateStringDefinition("payment.supportNumber");

            modal.SetContext(
                definition,
                "+79",
                ArcadeStringInputProfile.Phone(maxLength: 3),
                _ => { });

            modal.HandleNavigationKey(root, KeyCode.RightArrow);
            SetPrivateField(modal, "lastDirectionalStepFrame", -1);
            SetPrivateField(modal, "lastDirectionalStepBlockedUntil", Time.unscaledTime + 1f);
            modal.HandleNavigationMove(root, NavigationMoveEvent.Direction.Right);

            Assert.That(GetActiveSlotText(root), Is.EqualTo("7"));

            modal.Dispose();
        }

        [Test]
        public void NavigationMove_AllowsDifferentDirectionInsideDebounceWindow()
        {
            var root = CreateRoot();
            var modal = new ServiceTextInputModalWindow(root);
            var definition = CreateStringDefinition("payment.supportNumber");

            modal.SetContext(
                definition,
                "+79",
                ArcadeStringInputProfile.Phone(maxLength: 3),
                _ => { });

            modal.HandleNavigationMove(root, NavigationMoveEvent.Direction.Right);
            SetPrivateField(modal, "lastDirectionalStepFrame", -1);
            SetPrivateField(modal, "lastDirectionalStepBlockedUntil", Time.unscaledTime + 1f);
            modal.HandleNavigationMove(root, NavigationMoveEvent.Direction.Left);

            Assert.That(GetActiveSlotText(root), Is.EqualTo("+"));

            modal.Dispose();
        }

        private static ServiceSettingDefinition CreateStringDefinition(string key)
        {
            return TestObjectFactory.CreateSettingDefinition(
                key,
                ServiceSettingValueType.String,
                ServiceSettingValue.String(""));
        }

        private static void InvokeNavigationSubmit(ServiceTextInputModalWindow modal)
        {
            using var evt = NavigationSubmitEvent.GetPooled();
            typeof(ServiceTextInputModalWindow)
                .GetMethod("HandleNavigationSubmit", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(modal, new object[] { evt });
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            typeof(ServiceTextInputModalWindow)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static string GetActiveSlotText(VisualElement root)
        {
            var slots = root.Q<VisualElement>("ServiceTextInputSlots");
            foreach (var child in slots.Children())
            {
                if (child is Label label && label.ClassListContains("service-text-input-slot--active"))
                {
                    return label.text;
                }
            }

            return "";
        }

        private static VisualElement CreateRoot()
        {
            var root = new VisualElement();
            root.Add(new Label { name = "ServiceTextInputTitle" });
            root.Add(new Label { name = "ServiceTextInputSetting" });
            root.Add(new Label { name = "ServiceTextInputPreview" });
            root.Add(new VisualElement { name = "ServiceTextInputSlots" });
            root.Add(new Label { name = "ServiceTextInputSymbol" });
            root.Add(new Label { name = "ServiceTextInputStatus" });
            root.Add(new Button { name = "ServiceTextInputConfirmButton" });
            root.Add(new Button { name = "ServiceTextInputDeleteButton" });
            root.Add(new Button { name = "ServiceTextInputCancelButton" });
            return root;
        }
    }
}
