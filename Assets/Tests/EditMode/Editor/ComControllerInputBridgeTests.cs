using DanroJump.Hardware.Com.Input;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace DanroJump.Tests.EditMode
{
    public sealed class ComControllerInputBridgeTests
    {
        private ComControllerDevice device;

        [TearDown]
        public void TearDown()
        {
            if (device != null && device.added)
            {
                InputSystem.RemoveDevice(device);
            }

            device = null;
        }

        [Test]
        public void BuildState_UsesLegacyActiveLowPinMapping()
        {
            ComControllerState state = ComControllerInputBridge.BuildState(
                "0101010111111111",
                ComControllerInputBridge.CreateDefaultBindings(),
                activeLow: true);

            Assert.That(state.IsSet(ComControllerControl.Button00), Is.True);
            Assert.That(state.IsSet(ComControllerControl.Button01), Is.False);
            Assert.That(state.IsSet(ComControllerControl.Button02), Is.True);
            Assert.That(state.IsSet(ComControllerControl.Button03), Is.False);
            Assert.That(state.IsSet(ComControllerControl.Button04), Is.True);
            Assert.That(state.IsSet(ComControllerControl.Button05), Is.False);
            Assert.That(state.IsSet(ComControllerControl.Button06), Is.True);
        }

        [Test]
        public void AddDevice_RegistersComControllerControls()
        {
            device = ComControllerDevice.AddDevice();

            Assert.That(device, Is.Not.Null);
            Assert.That(ComControllerDevice.current, Is.SameAs(device));
            Assert.That(InputSystem.FindControl("<ComController>/button00"), Is.SameAs(device.button00));
            Assert.That(InputSystem.FindControl("<ComController>/button04"), Is.SameAs(device.button04));
            Assert.That(InputSystem.FindControl("<ComController>/button06"), Is.SameAs(device.button06));
            Assert.That(InputSystem.FindControl("<ComController>/button23"), Is.SameAs(device.button23));
            Assert.That(InputSystem.FindControl("<ComController>/adc00"), Is.SameAs(device.adc00));
            Assert.That(InputSystem.FindControl("<ComController>/adc01"), Is.SameAs(device.adc01));
        }

        [Test]
        public void QueuedState_UpdatesComControllerButtons()
        {
            device = ComControllerDevice.AddDevice();

            var state = new ComControllerState();
            state.Set(ComControllerControl.Button00, true);
            state.Set(ComControllerControl.Button04, true);
            state.Set(ComControllerControl.Button06, true);

            InputSystem.QueueStateEvent(device, state);
            InputSystem.Update();

            Assert.That(device.button00.isPressed, Is.True);
            Assert.That(device.button01.isPressed, Is.False);
            Assert.That(device.button04.isPressed, Is.True);
            Assert.That(device.button06.isPressed, Is.True);
        }

        [Test]
        public void BuildAdcState_ReadsTechnicalAdcValues()
        {
            var state = new ComControllerState();
            state.Set(ComControllerControl.Button00, true);

            state = ComControllerInputBridge.BuildAdcState(state, "ADC0=512 ADC1=768");

            Assert.That(state.IsSet(ComControllerControl.Button00), Is.True);
            Assert.That(state.adc00, Is.EqualTo(512f));
            Assert.That(state.adc01, Is.EqualTo(768f));
        }

        [Test]
        public void QueuedState_UpdatesComControllerAdcAxes()
        {
            device = ComControllerDevice.AddDevice();

            var state = new ComControllerState
            {
                adc00 = 0.25f,
                adc01 = 0.75f
            };

            InputSystem.QueueStateEvent(device, state);
            InputSystem.Update();

            Assert.That(device.adc00.ReadValue(), Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(device.adc01.ReadValue(), Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        public void InputActions_PlayerMoveUsesTechnicalComDirectionBindings()
        {
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Assert.That(inputActions, Is.Not.Null);

            InputAction moveAction = inputActions.FindAction("Player/Move", true);

            Assert.That(FindPartPath(moveAction, "up"), Is.EqualTo("<ComController>/button00"));
            Assert.That(FindPartPath(moveAction, "down"), Is.EqualTo("<ComController>/button01"));
            Assert.That(FindPartPath(moveAction, "left"), Is.EqualTo("<ComController>/button02"));
            Assert.That(FindPartPath(moveAction, "right"), Is.EqualTo("<ComController>/button03"));
        }

        [Test]
        public void InputActions_UiUsesTechnicalComNavigationSubmitAndSecretBindings()
        {
            var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Assert.That(inputActions, Is.Not.Null);

            InputAction navigateAction = inputActions.FindAction("UI/Navigate", true);
            InputAction submitAction = inputActions.FindAction("UI/Submit", true);
            InputAction secretAction = inputActions.FindAction("UI/Secret", true);

            Assert.That(FindPartPath(navigateAction, "up"), Is.EqualTo("<ComController>/button00"));
            Assert.That(FindPartPath(navigateAction, "down"), Is.EqualTo("<ComController>/button01"));
            Assert.That(FindPartPath(navigateAction, "left"), Is.EqualTo("<ComController>/button02"));
            Assert.That(FindPartPath(navigateAction, "right"), Is.EqualTo("<ComController>/button03"));
            Assert.That(FindBindingPath(submitAction, "COM"), Is.EqualTo("<ComController>/button04"));
            Assert.That(FindBindingPath(secretAction, "COM"), Is.EqualTo("<ComController>/button05"));
        }

        private static string FindPartPath(InputAction action, string partName)
        {
            foreach (InputBinding binding in action.bindings)
            {
                if (binding.isPartOfComposite && binding.groups == "COM" && binding.name == partName)
                {
                    return binding.path;
                }
            }

            return "";
        }

        private static string FindBindingPath(InputAction action, string group)
        {
            foreach (InputBinding binding in action.bindings)
            {
                if (!binding.isComposite && !binding.isPartOfComposite && binding.groups == group)
                {
                    return binding.path;
                }
            }

            return "";
        }
    }
}
