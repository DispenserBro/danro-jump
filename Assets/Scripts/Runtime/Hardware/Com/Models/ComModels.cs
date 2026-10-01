using UnityEngine;

namespace DanroJump.Hardware.Com
{
    /// <summary>
    /// Набор параметров serial-подключения к одному COM-устройству.
    /// </summary>
    public readonly struct SerialSettings
    {
        /// <summary>
        /// Имя COM-порта.
        /// </summary>
        public readonly string PortName;

        /// <summary>
        /// Скорость serial-подключения.
        /// </summary>
        public readonly int BaudRate;

        /// <summary>
        /// Количество бит данных в serial-пакете.
        /// </summary>
        public readonly int DataBits;

        /// <summary>
        /// Режим четности serial-порта.
        /// </summary>
        public readonly SerialParity Parity;

        /// <summary>
        /// Количество stop bits serial-порта.
        /// </summary>
        public readonly SerialStopBits StopBits;

        /// <summary>
        /// Таймаут чтения из serial-порта в миллисекундах.
        /// </summary>
        public readonly int ReadTimeoutMs;

        /// <summary>
        /// Таймаут записи в serial-порт в миллисекундах.
        /// </summary>
        public readonly int WriteTimeoutMs;

        /// <summary>
        /// Создает настройки порта с минимальной защитой таймаутов от нулевых значений.
        /// </summary>
        public SerialSettings(
            string portName,
            int baudRate,
            int dataBits,
            SerialParity parity,
            SerialStopBits stopBits,
            int readTimeoutMs,
            int writeTimeoutMs)
        {
            PortName = portName ?? "";
            BaudRate = baudRate;
            DataBits = dataBits;
            Parity = parity;
            StopBits = stopBits;
            ReadTimeoutMs = Mathf.Max(1, readTimeoutMs);
            WriteTimeoutMs = Mathf.Max(1, writeTimeoutMs);
        }
    }

    /// <summary>
    /// Сообщение аппаратного ввода, привязанное к конкретному устройству.
    /// </summary>
    public readonly struct ComInputMessage
    {
        /// <summary>
        /// Идентификатор устройства-источника.
        /// </summary>
        public readonly string DeviceId;

        /// <summary>
        /// Биты аппаратного ввода.
        /// </summary>
        public readonly string InputBits;

        /// <summary>
        /// Создает снимок входных битов от устройства.
        /// </summary>
        public ComInputMessage(string deviceId, string inputBits)
        {
            DeviceId = deviceId ?? "";
            InputBits = inputBits ?? "";
        }
    }

    /// <summary>
    /// Сообщение аналоговых ADC-входов, привязанное к конкретному устройству.
    /// </summary>
    public readonly struct ComAdcMessage
    {
        /// <summary>
        /// Идентификатор устройства-источника.
        /// </summary>
        public readonly string DeviceId;

        /// <summary>
        /// Сырые значения ADC после префикса строки.
        /// </summary>
        public readonly string AdcValues;

        /// <summary>
        /// Создает снимок аналоговых ADC-входов от устройства.
        /// </summary>
        public ComAdcMessage(string deviceId, string adcValues)
        {
            DeviceId = deviceId ?? "";
            AdcValues = adcValues ?? "";
        }
    }

    /// <summary>
    /// Результат попытки открыть ячейку призовой витрины.
    /// </summary>
    public readonly struct PrizeOpenResult
    {
        /// <summary>
        /// Была ли выдача приза успешной.
        /// </summary>
        public readonly bool IsSuccess;

        /// <summary>
        /// Номер призовой ячейки.
        /// </summary>
        public readonly int BoxNumber;

        /// <summary>
        /// Сообщение о результате операции.
        /// </summary>
        public readonly string Message;

        private PrizeOpenResult(bool isSuccess, int boxNumber, string message)
        {
            IsSuccess = isSuccess;
            BoxNumber = boxNumber;
            Message = message ?? "";
        }

        /// <summary>
        /// Создает успешный результат выдачи приза.
        /// </summary>
        public static PrizeOpenResult Success(int boxNumber)
        {
            return new PrizeOpenResult(true, boxNumber, $"РЇС‡РµР№РєР° {boxNumber} СѓСЃРїРµС€РЅРѕ РѕС‚РєСЂС‹С‚Р°.");
        }

        /// <summary>
        /// Создает результат с причиной отказа в выдаче.
        /// </summary>
        public static PrizeOpenResult Fail(int boxNumber, string message)
        {
            return new PrizeOpenResult(false, boxNumber, message);
        }
    }

    /// <summary>
    /// Один шаг световой последовательности на аппаратной плате.
    /// </summary>
    public readonly struct LightStep
    {
        /// <summary>
        /// Световой режим платы.
        /// </summary>
        public readonly LightMode Mode;

        /// <summary>
        /// Количество циклов эффекта.
        /// </summary>
        public readonly float Cycles;

        /// <summary>
        /// Создает шаг светового эффекта с безопасным числом циклов.
        /// </summary>
        public LightStep(LightMode mode, float cycles)
        {
            Mode = mode;
            Cycles = Mathf.Max(0f, cycles);
        }
    }

    /// <summary>
    /// Статус ячейки призовой витрины, полученный от контроллера.
    /// </summary>
    public enum PrizeBoxStatus
    {
        Ok,
        OkWithCard,
        Empty,
        Open,
        Opened,
        OpenError,
        ParsingError,
        NotFound,
        Error
    }

    /// <summary>
    /// Значения четности serial-порта, независимые от конкретной реализации System.IO.Ports.
    /// </summary>
    public enum SerialParity
    {
        None,
        Odd,
        Even,
        Mark,
        Space
    }

    /// <summary>
    /// Значения stop bits serial-порта, независимые от конкретной реализации System.IO.Ports.
    /// </summary>
    public enum SerialStopBits
    {
        None,
        One,
        Two,
        OnePointFive
    }

    /// <summary>
    /// Коды световых эффектов, которые понимает аппаратная плата.
    /// </summary>
    public enum LightMode
    {
        Off = -1,
        HorizontalWave = 40,
        VerticalWave = 41,
        DiagonalWave = 42,
        Rainbow = 43,
        WaveUp = 44,
        VerticalWaveUp = 45,
        FinalWave = 46,
        BlueWave = 47,
        DiagonalWave2 = 48,
        FallingRows = 49,
        OrangeWave = 50,
        SeaWave = 51,
        DiagonalWave3 = 52
    }
}
