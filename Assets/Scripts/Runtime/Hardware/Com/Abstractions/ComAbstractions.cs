using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Events;

namespace DanroJump.Hardware.Com
{
    /// <summary>
    /// Единая точка доступа к COM-подсистеме автомата: кредиты, ввод, витрина призов и подсветка.
    /// </summary>
    public interface IComSystem :
        IComCreditWallet,
        IComInputSource,
        IPrizeShowcaseGateway,
        IComLightingController
    {
        bool IsRunning { get; }
        string MainDeviceId { get; }
        string PrizeStandDeviceId { get; }
        IReadOnlyCollection<string> ConnectedDeviceIds { get; }

        UnityEvent<string> DeviceAuthorized { get; }
        UnityEvent<string> DeviceDisconnected { get; }

        /// <summary>
        /// Запускает поиск, авторизацию и фоновые циклы обмена данными с COM-устройствами.
        /// </summary>
        void StartSystem();

        /// <summary>
        /// Останавливает COM-подсистему и при необходимости отправляет устройствам команды выключения.
        /// </summary>
        void StopSystem(bool sendShutdownCommands);

        bool SendToMain(string command);
        bool SendToDevice(string deviceId, string command);
        void SendToAll(string command);
        string GetDeviceRole(string deviceId);
        string GetDeviceMac(string deviceId);
    }

    /// <summary>
    /// Хранилище кредитов, полученных от монетоприемника или другой платежной периферии.
    /// </summary>
    public interface IComCreditWallet
    {
        int Credits { get; }
        UnityEvent<int> CreditAdded { get; }
        UnityEvent<int> CreditRemoved { get; }
        UnityEvent<int> CreditsChanged { get; }

        /// <summary>
        /// Добавляет кредиты и уведомляет подписчиков об изменении баланса.
        /// </summary>
        void AddCredit(int amount = 1);

        /// <summary>
        /// Пытается списать кредиты без ухода в отрицательный баланс.
        /// </summary>
        bool TrySpendCredits(int amount = 1);

        /// <summary>
        /// Принудительно выставляет текущий баланс кредитов.
        /// </summary>
        void SetCredits(int value);
    }

    /// <summary>
    /// Источник аппаратного ввода с основной платы: кнопки, сенсоры и последние биты состояния.
    /// </summary>
    public interface IComInputSource
    {
        string LastMainInputBits { get; }
        string LastMainAdcValues { get; }
        UnityEvent<string> MainInputReceived { get; }
        UnityEvent<string> MainAdcReceived { get; }
        UnityEvent HopperSensorTriggered { get; }

        /// <summary>
        /// Возвращает последнее известное состояние входов конкретного устройства.
        /// </summary>
        string GetLastInputBits(string deviceId);

        /// <summary>
        /// Возвращает последние известные ADC-значения конкретного устройства.
        /// </summary>
        string GetLastAdcValues(string deviceId);
    }

    /// <summary>
    /// Шлюз для работы с призовой витриной и выдачей призов через аппаратные ячейки.
    /// </summary>
    public interface IPrizeShowcaseGateway
    {
        UnityEvent<int> PrizeBoxOpened { get; }

        /// <summary>
        /// Запрашивает текущие статусы всех ячеек призовой витрины.
        /// </summary>
        UniTask<IReadOnlyDictionary<int, PrizeBoxStatus>> GetPrizeBoxStatusesAsync(
            CancellationToken cancellationToken = default);

        UniTask<int[]> GetReadyPrizeBoxesAsync(CancellationToken cancellationToken = default);
        UniTask<PrizeOpenResult> OpenPrizeBoxAsync(int boxNumber, CancellationToken cancellationToken = default);
        UniTask<PrizeOpenResult> ForceOpenPrizeBoxAsync(int boxNumber, CancellationToken cancellationToken = default);
        bool SetPrizeStandIdleLighting();
        bool HighlightPrizeBox(int boxNumber);
    }

    /// <summary>
    /// Контроллер аппаратной подсветки платы, шляпы и связанных световых сценариев.
    /// </summary>
    public interface IComLightingController
    {
        void PlayBoardLight(LightMode mode, float cycles = -1f);
        void PlayBoardLightSequence(IReadOnlyList<LightStep> steps, bool loop);
        void PlayDefaultBoardLight();
        void StopBoardLightSequence();
        void StopAllBoardEffects();
        void BlinkHat();
        void BlinkHat(float intervalSeconds);
        void StopHatBlink(bool leaveHatEnabled = true);
        void PauseLighting();
        void ResumeLighting();
    }

    /// <summary>
    /// Поставщик имен доступных COM-портов, вынесенный для тестов и подмены транспорта.
    /// </summary>
    public interface IComPortProvider
    {
        string[] GetPortNames();
    }

    /// <summary>
    /// Фабрика транспорта поверх конкретных serial-настроек.
    /// </summary>
    public interface IComTransportFactory
    {
        IComTransport Create(SerialSettings settings);
    }

    /// <summary>
    /// Минимальный serial-транспорт, который скрывает реализацию System.IO.Ports от остального кода.
    /// </summary>
    public interface IComTransport : IDisposable
    {
        bool IsOpen { get; }
        void Open();
        void Close();
        void DiscardInBuffer();
        void DiscardOutBuffer();
        string ReadLine();
        void WriteLine(string value);
    }
}
