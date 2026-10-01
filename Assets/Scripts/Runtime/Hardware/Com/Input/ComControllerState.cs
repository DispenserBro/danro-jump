using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace DanroJump.Hardware.Com.Input
{
    /// <summary>
    /// Состояние входов основной COM-платы.
    /// Строка INPUT публикует 24 кнопочных входа: button00...button23.
    /// Строка ADC публикует аналоговые входы: adc00...adc01.
    /// </summary>
    public struct ComControllerState : IInputStateTypeInfo
    {
        public FourCC format => ComControllerDevice.StateFormat;

        [InputControl(name = "button00", layout = "Button", bit = 0, displayName = "Button 00")]
        [InputControl(name = "button01", layout = "Button", bit = 1, displayName = "Button 01")]
        [InputControl(name = "button02", layout = "Button", bit = 2, displayName = "Button 02")]
        [InputControl(name = "button03", layout = "Button", bit = 3, displayName = "Button 03")]
        [InputControl(name = "button04", layout = "Button", bit = 4, displayName = "Button 04")]
        [InputControl(name = "button05", layout = "Button", bit = 5, displayName = "Button 05")]
        [InputControl(name = "button06", layout = "Button", bit = 6, displayName = "Button 06")]
        [InputControl(name = "button07", layout = "Button", bit = 7, displayName = "Button 07")]
        [InputControl(name = "button08", layout = "Button", bit = 8, displayName = "Button 08")]
        [InputControl(name = "button09", layout = "Button", bit = 9, displayName = "Button 09")]
        [InputControl(name = "button10", layout = "Button", bit = 10, displayName = "Button 10")]
        [InputControl(name = "button11", layout = "Button", bit = 11, displayName = "Button 11")]
        [InputControl(name = "button12", layout = "Button", bit = 12, displayName = "Button 12")]
        [InputControl(name = "button13", layout = "Button", bit = 13, displayName = "Button 13")]
        [InputControl(name = "button14", layout = "Button", bit = 14, displayName = "Button 14")]
        [InputControl(name = "button15", layout = "Button", bit = 15, displayName = "Button 15")]
        [InputControl(name = "button16", layout = "Button", bit = 16, displayName = "Button 16")]
        [InputControl(name = "button17", layout = "Button", bit = 17, displayName = "Button 17")]
        [InputControl(name = "button18", layout = "Button", bit = 18, displayName = "Button 18")]
        [InputControl(name = "button19", layout = "Button", bit = 19, displayName = "Button 19")]
        [InputControl(name = "button20", layout = "Button", bit = 20, displayName = "Button 20")]
        [InputControl(name = "button21", layout = "Button", bit = 21, displayName = "Button 21")]
        [InputControl(name = "button22", layout = "Button", bit = 22, displayName = "Button 22")]
        [InputControl(name = "button23", layout = "Button", bit = 23, displayName = "Button 23")]
        public uint buttons;

        [InputControl(name = "adc00", layout = "Axis", displayName = "ADC 00")]
        public float adc00;

        [InputControl(name = "adc01", layout = "Axis", displayName = "ADC 01")]
        public float adc01;

        public void Set(ComControllerControl control, bool pressed)
        {
            uint mask = 1u << (int)control;
            if (pressed)
            {
                buttons |= mask;
                return;
            }

            buttons &= ~mask;
        }

        public bool IsSet(ComControllerControl control)
        {
            return (buttons & (1u << (int)control)) != 0;
        }
    }
}
