using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Utilities;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DanroJump.Hardware.Com.Input
{
    /// <summary>
    /// Кастомное Input System устройство для входов основной COM-платы автомата.
    /// </summary>
    [InputControlLayout(
        stateType = typeof(ComControllerState),
        displayName = "COM Controller")]
    public sealed class ComControllerDevice : InputDevice
    {
        public const string LayoutName = "ComController";
        public const string InterfaceName = "COM";
        public const string ProductName = "Danro Jump COM Controller";
        public static readonly FourCC StateFormat = new FourCC('C', 'O', 'M', 'C');

        public static ComControllerDevice current { get; private set; }

        public ButtonControl button00 { get; private set; }
        public ButtonControl button01 { get; private set; }
        public ButtonControl button02 { get; private set; }
        public ButtonControl button03 { get; private set; }
        public ButtonControl button04 { get; private set; }
        public ButtonControl button05 { get; private set; }
        public ButtonControl button06 { get; private set; }
        public ButtonControl button07 { get; private set; }
        public ButtonControl button08 { get; private set; }
        public ButtonControl button09 { get; private set; }
        public ButtonControl button10 { get; private set; }
        public ButtonControl button11 { get; private set; }
        public ButtonControl button12 { get; private set; }
        public ButtonControl button13 { get; private set; }
        public ButtonControl button14 { get; private set; }
        public ButtonControl button15 { get; private set; }
        public ButtonControl button16 { get; private set; }
        public ButtonControl button17 { get; private set; }
        public ButtonControl button18 { get; private set; }
        public ButtonControl button19 { get; private set; }
        public ButtonControl button20 { get; private set; }
        public ButtonControl button21 { get; private set; }
        public ButtonControl button22 { get; private set; }
        public ButtonControl button23 { get; private set; }
        public AxisControl adc00 { get; private set; }
        public AxisControl adc01 { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void RegisterLayout()
        {
            current = null;

            InputSystem.RegisterLayout<ComControllerDevice>(
                LayoutName,
                matches: new InputDeviceMatcher()
                    .WithInterface(InterfaceName)
                    .WithDeviceClass(LayoutName));
        }

        public static ComControllerDevice AddDevice()
        {
            RegisterLayout();

            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is ComControllerDevice comDevice)
                {
                    comDevice.MakeCurrent();
                    return comDevice;
                }
            }

            var description = new InputDeviceDescription
            {
                interfaceName = InterfaceName,
                deviceClass = LayoutName,
                product = ProductName,
                manufacturer = "Robotic Retailers"
            };

            return (ComControllerDevice)InputSystem.AddDevice(description);
        }

        protected override void FinishSetup()
        {
            base.FinishSetup();

            button00 = GetChildControl<ButtonControl>(nameof(button00));
            button01 = GetChildControl<ButtonControl>(nameof(button01));
            button02 = GetChildControl<ButtonControl>(nameof(button02));
            button03 = GetChildControl<ButtonControl>(nameof(button03));
            button04 = GetChildControl<ButtonControl>(nameof(button04));
            button05 = GetChildControl<ButtonControl>(nameof(button05));
            button06 = GetChildControl<ButtonControl>(nameof(button06));
            button07 = GetChildControl<ButtonControl>(nameof(button07));
            button08 = GetChildControl<ButtonControl>(nameof(button08));
            button09 = GetChildControl<ButtonControl>(nameof(button09));
            button10 = GetChildControl<ButtonControl>(nameof(button10));
            button11 = GetChildControl<ButtonControl>(nameof(button11));
            button12 = GetChildControl<ButtonControl>(nameof(button12));
            button13 = GetChildControl<ButtonControl>(nameof(button13));
            button14 = GetChildControl<ButtonControl>(nameof(button14));
            button15 = GetChildControl<ButtonControl>(nameof(button15));
            button16 = GetChildControl<ButtonControl>(nameof(button16));
            button17 = GetChildControl<ButtonControl>(nameof(button17));
            button18 = GetChildControl<ButtonControl>(nameof(button18));
            button19 = GetChildControl<ButtonControl>(nameof(button19));
            button20 = GetChildControl<ButtonControl>(nameof(button20));
            button21 = GetChildControl<ButtonControl>(nameof(button21));
            button22 = GetChildControl<ButtonControl>(nameof(button22));
            button23 = GetChildControl<ButtonControl>(nameof(button23));
            adc00 = GetChildControl<AxisControl>(nameof(adc00));
            adc01 = GetChildControl<AxisControl>(nameof(adc01));
        }

        public override void MakeCurrent()
        {
            base.MakeCurrent();
            current = this;
        }

        protected override void OnRemoved()
        {
            if (current == this)
            {
                current = null;
            }

            base.OnRemoved();
        }
    }

#if UNITY_EDITOR
    [InitializeOnLoad]
    internal static class ComControllerDeviceEditorRegistration
    {
        static ComControllerDeviceEditorRegistration()
        {
            ComControllerDevice.RegisterLayout();
        }
    }
#endif
}
