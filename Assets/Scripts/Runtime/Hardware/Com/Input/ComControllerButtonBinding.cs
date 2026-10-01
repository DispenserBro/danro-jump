using System;
using UnityEngine;

namespace DanroJump.Hardware.Com.Input
{
    /// <summary>
    /// Связывает индекс входного бита COM-платы с логической кнопкой Input System устройства.
    /// </summary>
    [Serializable]
    public struct ComControllerButtonBinding
    {
        [SerializeField] [Min(0)] private int bitIndex;
        [SerializeField] private ComControllerControl control;

        public ComControllerButtonBinding(int bitIndex, ComControllerControl control)
        {
            this.bitIndex = Mathf.Max(0, bitIndex);
            this.control = control;
        }

        public int BitIndex => bitIndex;
        public ComControllerControl Control => control;
    }
}
