using System.Collections.Generic;
using UnityEngine;

namespace DanroJump.Hardware.Com
{
    /// <summary>
    /// Справочник длительности аппаратных световых эффектов.
    /// </summary>
    public static class LightTimings
    {
        private static readonly Dictionary<LightMode, int> CycleMs = new()
        {
            { LightMode.HorizontalWave, 1000 },
            { LightMode.VerticalWave, 1000 },
            { LightMode.DiagonalWave, 3000 },
            { LightMode.Rainbow, 3000 },
            { LightMode.WaveUp, 3000 },
            { LightMode.VerticalWaveUp, 1000 },
            { LightMode.FinalWave, 1000 },
            { LightMode.BlueWave, 1000 },
            { LightMode.DiagonalWave2, 5000 },
            { LightMode.FallingRows, 6000 },
            { LightMode.OrangeWave, 3000 },
            { LightMode.SeaWave, 3000 },
            { LightMode.DiagonalWave3, 4000 }
        };

        /// <summary>
        /// Возвращает длительность одного цикла эффекта в миллисекундах.
        /// </summary>
        public static int GetCycleMs(LightMode mode)
        {
            return CycleMs.TryGetValue(mode, out int ms) ? ms : 0;
        }

        /// <summary>
        /// Возвращает длительность одного цикла эффекта в секундах.
        /// </summary>
        public static float GetCycleSeconds(LightMode mode)
        {
            return GetCycleMs(mode) / 1000f;
        }

        /// <summary>
        /// Переводит количество циклов эффекта в длительность команды для платы.
        /// </summary>
        public static int CyclesToCommandSeconds(LightMode mode, float cycles)
        {
            return Mathf.RoundToInt(GetCycleSeconds(mode) * Mathf.Max(0f, cycles));
        }
    }
}
