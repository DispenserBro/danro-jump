using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DanroJump.Debugging;
using DanroJump.Settings;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using Zenject;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DanroJump.Hardware.Com
{
    /// <summary>
    /// Управляет COM-устройствами автомата: авторизацией плат, кредитами, вводом, подсветкой и призовой витриной.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hardware/COM System")]
    public sealed class ComSystem : MonoBehaviour, IComSystem, IDebugLogConsumer
    {
        private const string DefaultInputBits = "11111111111111111111111111";
        private const string RequestInputCommand = "91";
        private const string RequestStandStatusCommand = "25";
        private const string OpenBoxCommandPrefix = "2501";
        private const string ForceOpenBoxCommandPrefix = "2511";
        private const string PaymentImpulseCostPlayerPrefsSuffix = ".PaymentImpulseCost";
        private static readonly string[] DefaultPrizeStandRoles =
        {
            "prize_stand",
            "prize_stand_controller",
            "vitrina"
        };

        [Header("Lifecycle")]
        [SerializeField] private bool startOnEnable = true;
        [SerializeField] private bool dontDestroyOnLoad = true;
        [SerializeField] private bool stopPortsOnDisable;
        [SerializeField] private bool sendShutdownCommandsOnQuit = true;

        [Header("Port scan")]
        [SerializeField] private bool useAllDetectedPorts = true;
        [SerializeField] private string[] explicitPorts = Array.Empty<string>();
        [SerializeField] private string preferredMainPort = "";

        [Header("Serial settings")]
        [SerializeField] private int baudRate = 115200;
        [SerializeField] private int dataBits = 8;
        [SerializeField] private SerialParity parity = SerialParity.None;
        [SerializeField] private SerialStopBits stopBits = SerialStopBits.One;
        [SerializeField] private int readTimeoutMs = 50;
        [SerializeField] private int writeTimeoutMs = 100;

        [Header("Authorization")]
        [SerializeField] private bool useEncryptedHandshake = true;
        [SerializeField] private int handshakeTimeoutMs = 5000;
        [SerializeField] private string roleRequestCommand = "97";
        [SerializeField] private string roleResponsePrefix = "ROLE";
        [SerializeField] private int roleRequestTimeoutMs = 1500;
        [SerializeField] private string mainControllerRole = "main";

        [Header("Keep alive")]
        [SerializeField] private bool keepAlive = true;
        [SerializeField] private float keepAliveIntervalSeconds = 8f;
        [SerializeField] private int missedInputResponsesBeforeReconnect = 3;

        [Header("Credits")]
        [SerializeField] private bool saveCreditsBetweenSessions;
        [SerializeField] private string creditsPlayerPrefsKey = "Com.Credits";
        [SerializeField] [Min(1)] private int paymentImpulseCostFallback = 1;
        [SerializeField] [FormerlySerializedAs("simulatedCreditAmount")] [Min(1)] private int simulatedPaymentImpulseCount = 1;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private InputActionReference addCreditActionReference;
        [SerializeField] private InputActionAsset inputActionsAsset;
        [SerializeField] private string addCreditActionPath = "UI/AddCredit";
#endif

        [Header("Prize showcase")]
        [SerializeField] private string[] prizeStandRoles = DefaultPrizeStandRoles;
        [SerializeField] [Min(0)] private int prizeStandBoxesCount = 24;
        [SerializeField] private float prizeStatusTimeoutSeconds = 3f;
        [SerializeField] private float prizeOpenTimeoutSeconds = 5f;

        [Header("Lighting")]
        [SerializeField] private string hatOnCommand = "2131";
        [SerializeField] private string hatOffCommand = "2130";
        [SerializeField] [Range(0.05f, 5f)] private float hatBlinkIntervalSeconds = 0.5f;
        [SerializeField] [Range(0f, 2f)] private float hatBlinkVarianceSeconds;
        [SerializeField] private LightMode defaultLightMode = LightMode.Rainbow;
        [SerializeField] private float defaultLightCycles = 1f;

        [Header("Debug")]
        [SerializeField, FormerlySerializedAs("logDebugToConsole")] private bool logLifecycle = true;
        [SerializeField] private bool logInput = true;
        [SerializeField] private bool logAdc = true;
        [SerializeField, FormerlySerializedAs("logRawLinesToConsole")] private bool logRawLines;
        [SerializeField] private bool logPrize = true;
        [SerializeField] private bool logCredits = true;
        [SerializeField] private bool logHopper = true;

        [Header("Events")]
        [SerializeField] private UnityEvent<int> creditAdded = new();
        [SerializeField] private UnityEvent<int> creditRemoved = new();
        [SerializeField] private UnityEvent<int> creditsChanged = new();
        [SerializeField] private UnityEvent<string> mainInputReceived = new();
        [SerializeField] private UnityEvent<string> mainAdcReceived = new();
        [SerializeField] private UnityEvent<string> deviceAuthorized = new();
        [SerializeField] private UnityEvent<string> deviceDisconnected = new();
        [SerializeField] private UnityEvent<int> prizeBoxOpened = new();
        [SerializeField] private UnityEvent hopperSensorTriggered = new();

        private readonly Dictionary<string, ComDeviceConnection> devices = new(StringComparer.OrdinalIgnoreCase);
        private readonly object devicesLock = new();
        private readonly ConcurrentQueue<Action> mainThreadQueue = new();
        private readonly ConcurrentQueue<string> coinQueue = new();
        private readonly ConcurrentQueue<ComInputMessage> inputQueue = new();
        private readonly ConcurrentQueue<ComAdcMessage> adcQueue = new();
        private readonly ConcurrentQueue<PrizeBoxStatusEntry> prizeStatusQueue = new();
        private readonly SemaphoreSlim prizeOperationLock = new(1, 1);
        private IComPortProvider portProvider;
        private IComTransportFactory transportFactory;
        private IDebugLogService debugLogService;
        private IReadOnlyServiceSettings serviceSettings;
        private ComLightingSystem lightingSystem;

        private CancellationTokenSource systemCts;
        private string mainDeviceId = "";
        private string prizeStandDeviceId = "";
        private int credits;
        private int activePaymentImpulseCost = 1;
        private bool serviceSettingsCallbacksRegistered;
        private bool f6WasPressedLastFrame;
#if ENABLE_INPUT_SYSTEM
        private InputAction addCreditAction;
        private bool addCreditActionWasEnabled;
#endif

        /// <summary>
        /// Последний активный экземпляр COM-системы для сцен, где DI еще не подключен.
        /// </summary>
        public static ComSystem Instance { get; private set; }

        /// <summary>
        /// Событие начисления кредитов.
        /// </summary>
        public UnityEvent<int> CreditAdded => creditAdded;

        /// <summary>
        /// Событие списания кредитов.
        /// </summary>
        public UnityEvent<int> CreditRemoved => creditRemoved;

        /// <summary>
        /// Событие любого изменения баланса кредитов.
        /// </summary>
        public UnityEvent<int> CreditsChanged => creditsChanged;

        /// <summary>
        /// Событие получения входных битов от основной платы.
        /// </summary>
        public UnityEvent<string> MainInputReceived => mainInputReceived;

        /// <summary>
        /// Событие получения ADC-значений от основной платы.
        /// </summary>
        public UnityEvent<string> MainAdcReceived => mainAdcReceived;

        /// <summary>
        /// Событие успешной авторизации COM-устройства.
        /// </summary>
        public UnityEvent<string> DeviceAuthorized => deviceAuthorized;

        /// <summary>
        /// Событие отключения COM-устройства.
        /// </summary>
        public UnityEvent<string> DeviceDisconnected => deviceDisconnected;

        /// <summary>
        /// Событие подтвержденного открытия призовой ячейки.
        /// </summary>
        public UnityEvent<int> PrizeBoxOpened => prizeBoxOpened;

        /// <summary>
        /// Событие срабатывания датчика хоппера на основной плате.
        /// </summary>
        public UnityEvent HopperSensorTriggered => hopperSensorTriggered;

        /// <summary>
        /// Показывает, запущена ли COM-подсистема.
        /// </summary>
        public bool IsRunning => systemCts != null && !systemCts.IsCancellationRequested;

        /// <summary>
        /// Текущий баланс аппаратных кредитов.
        /// </summary>
        public int Credits => credits;

        /// <summary>
        /// Идентификатор устройства, выбранного как основная плата.
        /// </summary>
        public string MainDeviceId => CurrentMainDeviceId;

        /// <summary>
        /// Идентификатор устройства, выбранного как контроллер призовой витрины.
        /// </summary>
        public string PrizeStandDeviceId => CurrentPrizeStandDeviceId;

        /// <summary>
        /// Последние входные биты от основной платы.
        /// </summary>
        public string LastMainInputBits => GetLastInputBits(CurrentMainDeviceId);

        /// <summary>
        /// Последние ADC-значения от основной платы.
        /// </summary>
        public string LastMainAdcValues => GetLastAdcValues(CurrentMainDeviceId);
        public IReadOnlyCollection<string> ConnectedDeviceIds
        {
            get
            {
                lock (devicesLock)
                {
                    return new List<string>(devices.Keys);
                }
            }
        }

        /// <summary>
        /// Получает сервис отладочного вывода из DI, если объект создан внутри Zenject-контекста.
        /// </summary>
        [Inject]
        public void Construct(
            [InjectOptional] IDebugLogService injectedDebugLogService,
            [InjectOptional] IReadOnlyServiceSettings injectedServiceSettings)
        {
            debugLogService = injectedDebugLogService;

            if (serviceSettingsCallbacksRegistered && !ReferenceEquals(serviceSettings, injectedServiceSettings))
            {
                UnregisterServiceSettingsCallbacks();
            }

            serviceSettings = injectedServiceSettings;

            if (isActiveAndEnabled)
            {
                RegisterServiceSettingsCallbacks();
            }
        }

        /// <summary>
        /// Инициализирует singleton-ссылку, инфраструктуру транспорта и сохраненный баланс кредитов.
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                UnityEngine.Debug.LogWarning("[ComSystem] Another instance already exists. Destroying duplicate scene instance.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureInfrastructure();
            EnsureLightingSystem();

            if (dontDestroyOnLoad)
            {
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                }
            }

            credits = saveCreditsBetweenSessions ? PlayerPrefs.GetInt(creditsPlayerPrefsKey, 0) : 0;
            activePaymentImpulseCost = LoadStoredPaymentImpulseCost();

            if (!saveCreditsBetweenSessions)
            {
                PlayerPrefs.SetInt(creditsPlayerPrefsKey, 0);
                PlayerPrefs.SetInt(GetPaymentImpulseCostPlayerPrefsKey(), activePaymentImpulseCost);
            }
        }

        /// <summary>
        /// Подменяет поставщика портов и фабрику транспорта до запуска системы.
        /// </summary>
        public void ConfigureInfrastructure(IComPortProvider customPortProvider, IComTransportFactory customTransportFactory)
        {
            if (IsRunning)
            {
                UnityEngine.Debug.LogWarning("[ComSystem] Infrastructure cannot be changed while the COM system is running.", this);
                return;
            }

            portProvider = customPortProvider ?? CreateDefaultPortProvider();
            transportFactory = customTransportFactory ?? CreateDefaultTransportFactory();
        }

        /// <summary>
        /// Автоматически запускает COM-подсистему при включении объекта, если это разрешено в инспекторе.
        /// </summary>
        private void OnEnable()
        {
            RegisterServiceSettingsCallbacks();

#if ENABLE_INPUT_SYSTEM
            RegisterAddCreditAction();
#endif

            if (startOnEnable)
            {
                StartSystem();
            }
        }

        /// <summary>
        /// Переносит события из фоновых потоков COM-подключений в главный поток Unity.
        /// </summary>
        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool f6Pressed = false;
            if (Keyboard.current != null)
            {
                f6Pressed = Keyboard.current.f6Key.isPressed;
            }
            else
            {
                try
                {
                    f6Pressed = UnityEngine.Input.GetKey(UnityEngine.KeyCode.F6);
                }
                catch
                {
                    // В случае, если старый ввод полностью отключен
                }
            }

            if (f6Pressed && !f6WasPressedLastFrame)
            {
                AddPaymentImpulse(simulatedPaymentImpulseCount);
                LogDebug(
                    DebugLogChannel.ComCredits,
                    logCredits,
                    $"Simulated F6 keyboard payment impulse input accepted. impulses={simulatedPaymentImpulseCount} balance={credits}");
            }
            f6WasPressedLastFrame = f6Pressed;
#endif

            while (mainThreadQueue.TryDequeue(out Action action))
            {
                action?.Invoke();
            }

            while (coinQueue.TryDequeue(out _))
            {
                AddPaymentImpulse(1);
            }

            while (inputQueue.TryDequeue(out ComInputMessage message))
            {
                if (message.DeviceId == CurrentMainDeviceId)
                {
                    mainInputReceived.Invoke(message.InputBits);
                }
            }

            while (adcQueue.TryDequeue(out ComAdcMessage message))
            {
                if (message.DeviceId == CurrentMainDeviceId)
                {
                    mainAdcReceived.Invoke(message.AdcValues);
                }
            }
        }

        /// <summary>
        /// Останавливает порты при выключении компонента, если включен соответствующий режим.
        /// </summary>
        private void OnDisable()
        {
            UnregisterServiceSettingsCallbacks();

#if ENABLE_INPUT_SYSTEM
            UnregisterAddCreditAction();
#endif

            if (stopPortsOnDisable)
            {
                StopSystem(false);
            }
        }

        /// <summary>
        /// Закрывает устройства при выходе из приложения и отправляет shutdown-команды.
        /// </summary>
        private void OnApplicationQuit()
        {
            StopSystem(sendShutdownCommandsOnQuit);
        }

        /// <summary>
        /// Освобождает singleton-ссылку, фоновые подключения и semaphore призовых операций.
        /// </summary>
        private void OnDestroy()
        {
            UnregisterServiceSettingsCallbacks();

            if (Instance == this)
            {
                Instance = null;
            }

            StopSystem(sendShutdownCommandsOnQuit);
            lightingSystem?.Dispose();
            lightingSystem = null;
            prizeOperationLock.Dispose();
        }

        /// <summary>
        /// Запускает сканирование портов и авторизацию найденных устройств.
        /// </summary>
        public void StartSystem()
        {
            if (IsRunning)
            {
                return;
            }

            systemCts = new CancellationTokenSource();
            EnsureInfrastructure();
            ResetDeviceSelections();
            ClearQueues();

            string[] detectedPorts = portProvider.GetPortNames();
            if (ShouldLog(DebugLogChannel.ComLifecycle, logLifecycle))
            {
                LogDebug(
                    DebugLogChannel.ComLifecycle,
                    $"Detected ports: {(detectedPorts.Length > 0 ? string.Join(", ", detectedPorts) : "<none>")}. " +
                    $"scanAll={useAllDetectedPorts} explicit=[{FormatList(explicitPorts)}] preferredMain={FormatDebugValue(preferredMainPort)}");
            }

            string[] ports = SelectPorts(detectedPorts);
            if (ShouldLog(DebugLogChannel.ComLifecycle, logLifecycle))
            {
                LogDebug(
                    DebugLogChannel.ComLifecycle,
                    $"Starting. Selected ports: {(ports.Length > 0 ? string.Join(", ", ports) : "<none>")}. " +
                    $"serial={baudRate}/{dataBits}/{parity}/{stopBits} readTimeoutMs={readTimeoutMs} writeTimeoutMs={writeTimeoutMs} " +
                    $"encryptedHandshake={useEncryptedHandshake} handshakeTimeoutMs={handshakeTimeoutMs} " +
                    $"roleRequest={FormatDebugValue(roleRequestCommand)} rolePrefix={FormatDebugValue(roleResponsePrefix)}");
            }
            if (ports.Length == 0)
            {
                UnityEngine.Debug.LogWarning("[ComSystem] COM ports were not found.");
                ResetSystemToken();
                return;
            }

            int startedDevices = 0;
            foreach (string portName in ports)
            {
                if (TryStartDevice(portName))
                {
                    startedDevices++;
                }
            }

            if (startedDevices == 0)
            {
                UnityEngine.Debug.LogWarning("[ComSystem] COM system did not start because all selected ports failed to open.");
                ResetSystemToken();
                return;
            }

            LogDebug(DebugLogChannel.ComLifecycle, logLifecycle, $"Started {startedDevices}/{ports.Length} device connection(s).");
        }

        private void EnsureInfrastructure()
        {
            portProvider ??= CreateDefaultPortProvider();
            transportFactory ??= CreateDefaultTransportFactory();
        }

        private static IComPortProvider CreateDefaultPortProvider()
        {
            return new FallbackComPortProvider(
                new ReflectionComPortProvider(),
                new NativeComPortProvider());
        }

        private static IComTransportFactory CreateDefaultTransportFactory()
        {
            return new FallbackComTransportFactory(
                new ReflectionSerialComTransportFactory(),
                new NativeComTransportFactory());
        }

        private void EnsureLightingSystem()
        {
            lightingSystem ??= new ComLightingSystem(
                CreateLightingOptions(),
                SendToMain,
                SendToPrizeStand);
        }

        private ComLightingOptions CreateLightingOptions()
        {
            return new ComLightingOptions(
                hatOnCommand,
                hatOffCommand,
                hatBlinkIntervalSeconds,
                hatBlinkVarianceSeconds,
                defaultLightMode,
                defaultLightCycles);
        }

        /// <summary>
        /// Останавливает все фоновые COM-операции и закрывает открытые порты.
        /// </summary>
        public void StopSystem(bool sendShutdownCommands)
        {
            if (systemCts == null)
            {
                return;
            }

            LogDebug(DebugLogChannel.ComLifecycle, logLifecycle, $"Stopping. sendShutdownCommands={sendShutdownCommands}");
            try
            {
                systemCts.Cancel();
            }
            catch
            {
                // ignored
            }

            ComDeviceConnection[] deviceSnapshot;
            lock (devicesLock)
            {
                deviceSnapshot = CopyDeviceValues();
                devices.Clear();
            }

            foreach (ComDeviceConnection device in deviceSnapshot)
            {
                Unsubscribe(device);
                device.Dispose(sendShutdownCommands);
            }

            systemCts.Dispose();
            systemCts = null;
            ResetDeviceSelections();
            lightingSystem?.StopBoardLightSequence();
            lightingSystem?.StopHatBlink(false);
            ClearQueues();
        }

        /// <summary>
        /// Отправляет команду на основную плату управления.
        /// </summary>
        public bool SendToMain(string command)
        {
            return SendToDevice(CurrentMainDeviceId, command);
        }

        /// <summary>
        /// Отправляет команду на контроллер призовой витрины.
        /// </summary>
        public bool SendToPrizeStand(string command)
        {
            return SendToDevice(CurrentPrizeStandDeviceId, command);
        }

        /// <summary>
        /// Отправляет команду конкретному авторизованному устройству.
        /// </summary>
        public bool SendToDevice(string deviceId, string command)
        {
            if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(command))
            {
                return false;
            }

            lock (devicesLock)
            {
                return devices.TryGetValue(deviceId, out ComDeviceConnection device) && device.Send(command);
            }
        }

        /// <summary>
        /// Рассылает команду всем подключенным устройствам.
        /// </summary>
        public void SendToAll(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            ComDeviceConnection[] deviceSnapshot;
            lock (devicesLock)
            {
                deviceSnapshot = CopyDeviceValues();
            }

            foreach (ComDeviceConnection device in deviceSnapshot)
            {
                device.Send(command);
            }
        }

        /// <summary>
        /// Возвращает последние входные биты устройства или безопасное значение по умолчанию.
        /// </summary>
        public string GetLastInputBits(string deviceId)
        {
            lock (devicesLock)
            {
                return !string.IsNullOrWhiteSpace(deviceId) && devices.TryGetValue(deviceId, out ComDeviceConnection device)
                    ? device.LastInputBits
                    : DefaultInputBits;
            }
        }

        /// <summary>
        /// Возвращает последние ADC-значения устройства или пустую строку.
        /// </summary>
        public string GetLastAdcValues(string deviceId)
        {
            lock (devicesLock)
            {
                return !string.IsNullOrWhiteSpace(deviceId) && devices.TryGetValue(deviceId, out ComDeviceConnection device)
                    ? device.LastAdcValues
                    : "";
            }
        }

        /// <summary>
        /// Возвращает роль авторизованного устройства.
        /// </summary>
        public string GetDeviceRole(string deviceId)
        {
            lock (devicesLock)
            {
                return !string.IsNullOrWhiteSpace(deviceId) && devices.TryGetValue(deviceId, out ComDeviceConnection device)
                    ? device.Role
                    : "";
            }
        }

        /// <summary>
        /// Возвращает MAC-адрес авторизованного устройства.
        /// </summary>
        public string GetDeviceMac(string deviceId)
        {
            lock (devicesLock)
            {
                return !string.IsNullOrWhiteSpace(deviceId) && devices.TryGetValue(deviceId, out ComDeviceConnection device)
                    ? device.MacAddress
                    : "";
            }
        }

        /// <summary>
        /// Увеличивает баланс кредитов, пришедших от аппаратной периферии.
        /// </summary>
        public void AddCredit(int amount = 1)
        {
            if (amount <= 0)
            {
                return;
            }

            credits += amount;
            SaveCredits();
            creditAdded.Invoke(credits);
            creditsChanged.Invoke(credits);
        }

        private void AddPaymentImpulse(int impulseCount = 1)
        {
            if (impulseCount <= 0)
            {
                return;
            }

            var impulseCost = ResolvePaymentImpulseCost();
            ApplyPaymentImpulseCostChange(impulseCost);
            AddCredit(impulseCount * impulseCost);
            LogDebug(
                DebugLogChannel.ComCredits,
                logCredits,
                $"Payment impulse accepted. impulses={impulseCount} impulseCost={impulseCost} balance={credits}");
        }

        private int ResolvePaymentImpulseCost()
        {
            ResolveServiceSettingsIfNeeded();
            return serviceSettings != null
                ? Mathf.Max(1, serviceSettings.GetInt(ServiceSettingsKeys.PaymentImpulseCost))
                : Mathf.Max(1, paymentImpulseCostFallback);
        }

        private void ResolveServiceSettingsIfNeeded()
        {
            if (serviceSettings != null)
            {
                return;
            }

            try
            {
                var projectContext = ProjectContext.Instance;
                if (projectContext != null &&
                    projectContext.Container != null &&
                    projectContext.Container.HasBinding<IReadOnlyServiceSettings>())
                {
                    serviceSettings = projectContext.Container.Resolve<IReadOnlyServiceSettings>();
                }
            }
            catch (ZenjectException exception)
            {
                LogWarning(
                    DebugLogChannel.ComLifecycle,
                    logLifecycle,
                    $"Service settings are not available for payment impulse cost: {exception.Message}");
            }
        }

        private void RegisterServiceSettingsCallbacks()
        {
            ResolveServiceSettingsIfNeeded();

            if (serviceSettings == null || serviceSettingsCallbacksRegistered)
            {
                return;
            }

            serviceSettings.SettingChanged += HandleServiceSettingChanged;
            serviceSettings.SettingsLoaded += HandleServiceSettingsLoaded;
            serviceSettingsCallbacksRegistered = true;

            ApplyPaymentImpulseCostChange(ResolvePaymentImpulseCost());
        }

        private void UnregisterServiceSettingsCallbacks()
        {
            if (!serviceSettingsCallbacksRegistered || serviceSettings == null)
            {
                return;
            }

            serviceSettings.SettingChanged -= HandleServiceSettingChanged;
            serviceSettings.SettingsLoaded -= HandleServiceSettingsLoaded;
            serviceSettingsCallbacksRegistered = false;
        }

        private void HandleServiceSettingsLoaded()
        {
            ApplyPaymentImpulseCostChange(ResolvePaymentImpulseCost());
        }

        private void HandleServiceSettingChanged(string key, ServiceSettingValue value)
        {
            if (!string.Equals(key, ServiceSettingsKeys.PaymentImpulseCost, StringComparison.Ordinal))
            {
                return;
            }

            var newImpulseCost = value.type == ServiceSettingValueType.Int
                ? value.intValue
                : ResolvePaymentImpulseCost();
            ApplyPaymentImpulseCostChange(newImpulseCost);
        }

        private void ApplyPaymentImpulseCostChange(int newImpulseCost)
        {
            newImpulseCost = Mathf.Max(1, newImpulseCost);
            var oldImpulseCost = Mathf.Max(1, activePaymentImpulseCost);

            if (oldImpulseCost == newImpulseCost)
            {
                activePaymentImpulseCost = newImpulseCost;
                SaveActivePaymentImpulseCost();
                return;
            }

            var oldCredits = credits;
            credits = RecalculateCreditsForPaymentImpulseCost(credits, oldImpulseCost, newImpulseCost);
            activePaymentImpulseCost = newImpulseCost;

            SaveCredits();
            SaveActivePaymentImpulseCost();

            if (credits > oldCredits)
            {
                creditAdded.Invoke(credits);
            }
            else if (credits < oldCredits)
            {
                creditRemoved.Invoke(credits);
            }

            creditsChanged.Invoke(credits);
            LogDebug(
                DebugLogChannel.ComCredits,
                logCredits,
                $"Payment impulse cost changed. oldCost={oldImpulseCost} newCost={newImpulseCost} oldBalance={oldCredits} balance={credits}");
        }

        private static int RecalculateCreditsForPaymentImpulseCost(int currentCredits, int oldImpulseCost, int newImpulseCost)
        {
            if (currentCredits <= 0)
            {
                return 0;
            }

            oldImpulseCost = Mathf.Max(1, oldImpulseCost);
            newImpulseCost = Mathf.Max(1, newImpulseCost);

            long numerator = (long)currentCredits * newImpulseCost;
            long rounded = (numerator + oldImpulseCost / 2L) / oldImpulseCost;
            return (int)Math.Min(int.MaxValue, rounded);
        }

        private int LoadStoredPaymentImpulseCost()
        {
            var currentImpulseCost = ResolvePaymentImpulseCost();
            return saveCreditsBetweenSessions
                ? Mathf.Max(1, PlayerPrefs.GetInt(GetPaymentImpulseCostPlayerPrefsKey(), currentImpulseCost))
                : currentImpulseCost;
        }

        private void SaveActivePaymentImpulseCost()
        {
            if (!saveCreditsBetweenSessions)
            {
                return;
            }

            PlayerPrefs.SetInt(GetPaymentImpulseCostPlayerPrefsKey(), Mathf.Max(1, activePaymentImpulseCost));
        }

        private string GetPaymentImpulseCostPlayerPrefsKey()
        {
            return creditsPlayerPrefsKey + PaymentImpulseCostPlayerPrefsSuffix;
        }

#if ENABLE_INPUT_SYSTEM
        private void RegisterAddCreditAction()
        {
            if (addCreditAction != null)
            {
                return;
            }

            addCreditAction = ResolveAddCreditAction();
            if (addCreditAction == null)
            {
                return;
            }

            addCreditActionWasEnabled = addCreditAction.enabled;
            addCreditAction.performed += HandleAddCreditActionPerformed;

            if (!addCreditAction.enabled)
            {
                addCreditAction.Enable();
            }
        }

        private void UnregisterAddCreditAction()
        {
            if (addCreditAction == null)
            {
                return;
            }

            addCreditAction.performed -= HandleAddCreditActionPerformed;

            if (!addCreditActionWasEnabled)
            {
                addCreditAction.Disable();
            }

            addCreditAction = null;
        }

        private InputAction ResolveAddCreditAction()
        {
            if (addCreditActionReference != null && addCreditActionReference.action != null)
            {
                return addCreditActionReference.action;
            }

            return inputActionsAsset != null && !string.IsNullOrWhiteSpace(addCreditActionPath)
                ? inputActionsAsset.FindAction(addCreditActionPath, false)
                : null;
        }

        private void HandleAddCreditActionPerformed(InputAction.CallbackContext context)
        {
            AddPaymentImpulse(simulatedPaymentImpulseCount);
            LogDebug(
                DebugLogChannel.ComCredits,
                logCredits,
                $"Simulated board payment impulse input accepted. impulses={simulatedPaymentImpulseCount} balance={credits}");
        }
#endif

        /// <summary>
        /// Пытается списать кредиты, не позволяя балансу уйти ниже нуля.
        /// </summary>
        public bool TrySpendCredits(int amount = 1)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (credits < amount)
            {
                return false;
            }

            credits -= amount;
            SaveCredits();
            creditRemoved.Invoke(credits);
            creditsChanged.Invoke(credits);
            return true;
        }

        /// <summary>
        /// Принудительно задает баланс кредитов и рассылает события изменения.
        /// </summary>
        public void SetCredits(int value)
        {
            int oldCredits = credits;
            credits = Mathf.Max(0, value);
            SaveCredits();

            if (credits > oldCredits)
            {
                creditAdded.Invoke(credits);
            }
            else if (credits < oldCredits)
            {
                creditRemoved.Invoke(credits);
            }

            creditsChanged.Invoke(credits);
        }

        /// <summary>
        /// Асинхронно запрашивает статусы ячеек призовой витрины.
        /// </summary>
        public async UniTask<IReadOnlyDictionary<int, PrizeBoxStatus>> GetPrizeBoxStatusesAsync(CancellationToken cancellationToken = default)
        {
            if (prizeStandBoxesCount <= 0 || string.IsNullOrWhiteSpace(CurrentPrizeStandDeviceId))
            {
                return new Dictionary<int, PrizeBoxStatus>();
            }

            ClearPrizeStatusQueue();
            if (!SendToPrizeStand(RequestStandStatusCommand))
            {
                return new Dictionary<int, PrizeBoxStatus>();
            }

            Dictionary<int, PrizeBoxStatus> statuses = new();
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalSeconds < prizeStatusTimeoutSeconds && statuses.Count < prizeStandBoxesCount)
            {
                cancellationToken.ThrowIfCancellationRequested();

                while (prizeStatusQueue.TryDequeue(out PrizeBoxStatusEntry entry))
                {
                    statuses[entry.BoxNumber] = entry.Status;

                    if (statuses.Count >= prizeStandBoxesCount)
                    {
                        break;
                    }
                }

                await UniTask.Delay(10, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }

            return statuses;
        }

        /// <summary>
        /// Возвращает номера ячеек, из которых можно безопасно выдать приз.
        /// </summary>
        public async UniTask<int[]> GetReadyPrizeBoxesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyDictionary<int, PrizeBoxStatus> statuses = await GetPrizeBoxStatusesAsync(cancellationToken);
            List<int> readyBoxes = new();
            foreach (var pair in statuses)
            {
                if (IsReadyToOpen(pair.Value))
                {
                    readyBoxes.Add(pair.Key);
                }
            }

            readyBoxes.Sort();
            return readyBoxes.ToArray();
        }

        /// <summary>
        /// Открывает призовую ячейку только если контроллер сообщает, что она готова.
        /// </summary>
        public async UniTask<PrizeOpenResult> OpenPrizeBoxAsync(int boxNumber, CancellationToken cancellationToken = default)
        {
            return await OpenPrizeBoxInternalAsync(boxNumber, false, cancellationToken);
        }

        /// <summary>
        /// Открывает призовую ячейку без предварительной проверки статуса.
        /// </summary>
        public async UniTask<PrizeOpenResult> ForceOpenPrizeBoxAsync(int boxNumber, CancellationToken cancellationToken = default)
        {
            return await OpenPrizeBoxInternalAsync(boxNumber, true, cancellationToken);
        }

        /// <summary>
        /// Переводит подсветку призовой витрины в режим ожидания.
        /// </summary>
        public bool SetPrizeStandIdleLighting()
        {
            EnsureLightingSystem();
            return lightingSystem.SetPrizeStandIdleLighting();
        }

        /// <summary>
        /// Подсвечивает конкретную призовую ячейку на витрине.
        /// </summary>
        public bool HighlightPrizeBox(int boxNumber)
        {
            EnsureLightingSystem();
            return lightingSystem.HighlightPrizeBox(boxNumber);
        }

        /// <summary>
        /// Запускает один световой эффект на основной плате.
        /// </summary>
        public void PlayBoardLight(LightMode mode, float cycles = -1f)
        {
            EnsureLightingSystem();
            lightingSystem.PlayBoardLight(mode, cycles);
        }

        /// <summary>
        /// Запускает последовательность световых эффектов на основной плате.
        /// </summary>
        public void PlayBoardLightSequence(IReadOnlyList<LightStep> steps, bool loop)
        {
            EnsureLightingSystem();
            lightingSystem.PlayBoardLightSequence(steps, loop);
        }

        /// <summary>
        /// Запускает световой эффект платы, выбранный как эффект по умолчанию.
        /// </summary>
        public void PlayDefaultBoardLight()
        {
            EnsureLightingSystem();
            lightingSystem.PlayDefaultBoardLight();
        }

        /// <summary>
        /// Останавливает текущую последовательность световых эффектов платы.
        /// </summary>
        public void StopBoardLightSequence()
        {
            EnsureLightingSystem();
            lightingSystem.StopBoardLightSequence();
        }

        /// <summary>
        /// Останавливает все эффекты платы и отправляет аппаратную команду выключения.
        /// </summary>
        public void StopAllBoardEffects()
        {
            EnsureLightingSystem();
            lightingSystem.StopAllBoardEffects();
        }

        /// <summary>
        /// Запускает мигание шляпы с интервалом из инспектора.
        /// </summary>
        public void BlinkHat()
        {
            EnsureLightingSystem();
            lightingSystem.BlinkHat();
        }

        /// <summary>
        /// Запускает мигание шляпы с указанным интервалом.
        /// </summary>
        public void BlinkHat(float intervalSeconds)
        {
            EnsureLightingSystem();
            lightingSystem.BlinkHat(intervalSeconds);
        }

        /// <summary>
        /// Останавливает мигание шляпы и оставляет ее включенной или выключенной.
        /// </summary>
        public void StopHatBlink(bool leaveHatEnabled = true)
        {
            EnsureLightingSystem();
            lightingSystem.StopHatBlink(leaveHatEnabled);
        }

        /// <summary>
        /// Приостанавливает активные световые сценарии без сброса игрового состояния.
        /// </summary>
        public void PauseLighting()
        {
            EnsureLightingSystem();
            lightingSystem.PauseLighting();
        }

        /// <summary>
        /// Возобновляет световые сценарии после паузы.
        /// </summary>
        public void ResumeLighting()
        {
            EnsureLightingSystem();
            lightingSystem.ResumeLighting();
        }

        /// <summary>
        /// Общий сценарий выдачи приза с сериализацией операций через semaphore.
        /// </summary>
        private async UniTask<PrizeOpenResult> OpenPrizeBoxInternalAsync(
            int boxNumber,
            bool force,
            CancellationToken cancellationToken)
        {
            if (!await prizeOperationLock.WaitAsync(0, cancellationToken))
            {
                return PrizeOpenResult.Fail(boxNumber, "Р’С‹РґР°С‡Р° СѓР¶Рµ РІС‹РїРѕР»РЅСЏРµС‚СЃСЏ.");
            }

            try
            {
                if (boxNumber <= 0)
                {
                    return PrizeOpenResult.Fail(boxNumber, "РќРѕРјРµСЂ СЏС‡РµР№РєРё РґРѕР»Р¶РµРЅ Р±С‹С‚СЊ Р±РѕР»СЊС€Рµ 0.");
                }

                if (prizeStandBoxesCount > 0 && boxNumber > prizeStandBoxesCount)
                {
                    return PrizeOpenResult.Fail(boxNumber, $"РќРѕРјРµСЂ СЏС‡РµР№РєРё РґРѕР»Р¶РµРЅ Р±С‹С‚СЊ РѕС‚ 1 РґРѕ {prizeStandBoxesCount}.");
                }

                if (string.IsNullOrWhiteSpace(CurrentPrizeStandDeviceId))
                {
                    return PrizeOpenResult.Fail(boxNumber, "РџСЂРёР·РѕРІР°СЏ РІРёС‚СЂРёРЅР° РЅРµ РїРѕРґРєР»СЋС‡РµРЅР°.");
                }

                if (!force)
                {
                    PrizeBoxStatus status = await ReadPrizeBoxStatusAsync(boxNumber, prizeStatusTimeoutSeconds, cancellationToken);
                    if (!IsReadyToOpen(status))
                    {
                        return PrizeOpenResult.Fail(boxNumber, $"РЇС‡РµР№РєР° {boxNumber} РЅРµ РіРѕС‚РѕРІР° Рє РІС‹РґР°С‡Рµ. РЎС‚Р°С‚СѓСЃ: {status}.");
                    }
                }

                ClearPrizeStatusQueue();
                string commandPrefix = force ? ForceOpenBoxCommandPrefix : OpenBoxCommandPrefix;
                if (!SendToPrizeStand($"{commandPrefix}{boxNumber:D2}"))
                {
                    return PrizeOpenResult.Fail(boxNumber, "РќРµ СѓРґР°Р»РѕСЃСЊ РѕС‚РїСЂР°РІРёС‚СЊ РєРѕРјР°РЅРґСѓ РѕС‚РєСЂС‹С‚РёСЏ.");
                }

                PrizeBoxStatus openedStatus = await WaitPrizeBoxStatusAsync(
                    boxNumber,
                    PrizeBoxStatus.Opened,
                    prizeOpenTimeoutSeconds,
                    cancellationToken);

                if (openedStatus == PrizeBoxStatus.Opened)
                {
                    mainThreadQueue.Enqueue(() => prizeBoxOpened.Invoke(boxNumber));
                    return PrizeOpenResult.Success(boxNumber);
                }

                return PrizeOpenResult.Fail(boxNumber, $"РџР»Р°С‚Р° РЅРµ РїРѕРґС‚РІРµСЂРґРёР»Р° РѕС‚РєСЂС‹С‚РёРµ. РџРѕСЃР»РµРґРЅРёР№ СЃС‚Р°С‚СѓСЃ: {openedStatus}.");
            }
            finally
            {
                prizeOperationLock.Release();
            }
        }

        /// <summary>
        /// Запрашивает статусы витрины и возвращает статус одной ячейки.
        /// </summary>
        private async UniTask<PrizeBoxStatus> ReadPrizeBoxStatusAsync(
            int boxNumber,
            float timeoutSeconds,
            CancellationToken cancellationToken)
        {
            ClearPrizeStatusQueue();
            if (!SendToPrizeStand(RequestStandStatusCommand))
            {
                return PrizeBoxStatus.NotFound;
            }

            return await WaitPrizeBoxStatusAsync(boxNumber, null, timeoutSeconds, cancellationToken);
        }

        /// <summary>
        /// Ожидает статус нужной ячейки до таймаута, опционально до конкретного ожидаемого состояния.
        /// </summary>
        private async UniTask<PrizeBoxStatus> WaitPrizeBoxStatusAsync(
            int boxNumber,
            PrizeBoxStatus? expectedStatus,
            float timeoutSeconds,
            CancellationToken cancellationToken)
        {
            PrizeBoxStatus lastStatus = PrizeBoxStatus.NotFound;
            Stopwatch stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed.TotalSeconds < timeoutSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                while (prizeStatusQueue.TryDequeue(out PrizeBoxStatusEntry entry))
                {
                    if (entry.BoxNumber != boxNumber)
                    {
                        continue;
                    }

                    lastStatus = entry.Status;
                    if (!expectedStatus.HasValue || entry.Status == expectedStatus.Value)
                    {
                        return entry.Status;
                    }
                }

                await UniTask.Delay(10, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }

            return lastStatus;
        }

        /// <summary>
        /// Создает подключение к одному порту и добавляет устройство после успешного старта.
        /// </summary>
        private bool TryStartDevice(string portName)
        {
            var device = new ComDeviceConnection(
                transportFactory,
                portName,
                baudRate,
                dataBits,
                parity,
                stopBits,
                readTimeoutMs,
                writeTimeoutMs,
                useEncryptedHandshake,
                handshakeTimeoutMs,
                keepAlive,
                keepAliveIntervalSeconds,
                missedInputResponsesBeforeReconnect,
                roleRequestCommand,
                roleResponsePrefix,
                roleRequestTimeoutMs,
                message => LogDebug(DebugLogChannel.ComLifecycle, logLifecycle, message),
                message => LogWarning(DebugLogChannel.ComLifecycle, logLifecycle, message));

            Subscribe(device);

            if (!device.Start(systemCts.Token))
            {
                Unsubscribe(device);
                device.Dispose(false);
                return false;
            }

            lock (devicesLock)
            {
                devices[device.DeviceId] = device;
            }

            return true;
        }

        /// <summary>
        /// Подписывает COM-систему на события одного подключения.
        /// </summary>
        private void Subscribe(ComDeviceConnection device)
        {
            device.Authorized += OnDeviceAuthorized;
            device.Disconnected += OnDeviceDisconnected;
            device.CoinAccepted += OnCoinAccepted;
            device.InputReceived += OnInputReceived;
            device.AdcReceived += OnAdcReceived;
            device.RawLineReceived += OnRawLineReceived;
            device.HopperSensorTriggered += OnHopperSensorTriggered;
        }

        /// <summary>
        /// Снимает подписки с одного подключения.
        /// </summary>
        private void Unsubscribe(ComDeviceConnection device)
        {
            device.Authorized -= OnDeviceAuthorized;
            device.Disconnected -= OnDeviceDisconnected;
            device.CoinAccepted -= OnCoinAccepted;
            device.InputReceived -= OnInputReceived;
            device.AdcReceived -= OnAdcReceived;
            device.RawLineReceived -= OnRawLineReceived;
            device.HopperSensorTriggered -= OnHopperSensorTriggered;
        }

        /// <summary>
        /// Назначает роли авторизованному устройству и отправляет событие в главный поток.
        /// </summary>
        private void OnDeviceAuthorized(ComDeviceConnection device)
        {
            lock (devicesLock)
            {
                if (string.IsNullOrWhiteSpace(mainDeviceId) ||
                    device.PortName == preferredMainPort ||
                    IsRole(device.Role, mainControllerRole))
                {
                    mainDeviceId = device.DeviceId;
                }

                if (IsPrizeStandRole(device.Role))
                {
                    prizeStandDeviceId = device.DeviceId;
                }
            }

            if (ShouldLog(DebugLogChannel.ComLifecycle, logLifecycle))
            {
                LogDebug(DebugLogChannel.ComLifecycle, $"Authorized {FormatDeviceDebug(device)}. main={CurrentMainDeviceId} prize={CurrentPrizeStandDeviceId}");
            }
            mainThreadQueue.Enqueue(() => deviceAuthorized.Invoke(device.DeviceId));
        }

        /// <summary>
        /// Удаляет отключенное устройство из реестра и освобождает его ресурсы.
        /// </summary>
        private void OnDeviceDisconnected(ComDeviceConnection device)
        {
            Unsubscribe(device);

            lock (devicesLock)
            {
                devices.Remove(device.DeviceId);

                if (device.DeviceId == mainDeviceId)
                {
                    mainDeviceId = GetFirstDeviceId();
                }

                if (device.DeviceId == prizeStandDeviceId)
                {
                    prizeStandDeviceId = "";
                }
            }

            device.Dispose(false);

            if (ShouldLog(DebugLogChannel.ComLifecycle, logLifecycle))
            {
                LogDebug(DebugLogChannel.ComLifecycle, $"Disconnected {FormatDeviceDebug(device)}. main={CurrentMainDeviceId} prize={CurrentPrizeStandDeviceId}");
            }
            mainThreadQueue.Enqueue(() => deviceDisconnected.Invoke(device.DeviceId));
        }

        /// <summary>
        /// Переносит событие принятой монеты в очередь главного потока.
        /// </summary>
        private void OnCoinAccepted(string deviceId)
        {
            if (deviceId == CurrentPrizeStandDeviceId)
            {
                return;
            }

            coinQueue.Enqueue(deviceId);
            LogDebug(DebugLogChannel.ComCredits, logCredits, $"Coin accepted from {deviceId}.");
        }

        /// <summary>
        /// Переносит входные биты основной платы в очередь главного потока.
        /// </summary>
        private void OnInputReceived(ComInputMessage message)
        {
            if (message.DeviceId == CurrentPrizeStandDeviceId)
            {
                return;
            }

            inputQueue.Enqueue(message);
            LogDebug(DebugLogChannel.ComInput, logInput, $"INPUT {message.DeviceId}: {message.InputBits}");
        }

        /// <summary>
        /// Переносит ADC-значения основной платы в очередь главного потока.
        /// </summary>
        private void OnAdcReceived(ComAdcMessage message)
        {
            if (message.DeviceId == CurrentPrizeStandDeviceId)
            {
                return;
            }

            adcQueue.Enqueue(message);
            LogDebug(DebugLogChannel.ComAdc, logAdc, $"ADC {message.DeviceId}: {message.AdcValues}");
        }

        /// <summary>
        /// Разбирает сырые строки призовой витрины в статусы ячеек.
        /// </summary>
        private void OnRawLineReceived(string deviceId, string line)
        {
            LogDebug(DebugLogChannel.ComRaw, logRawLines, $"RAW {deviceId}: {line}");

            if (deviceId == CurrentPrizeStandDeviceId && TryParsePrizeBoxStatus(line, out PrizeBoxStatusEntry status))
            {
                prizeStatusQueue.Enqueue(status);
                LogDebug(DebugLogChannel.ComPrize, logPrize, $"Prize status {deviceId}: box={status.BoxNumber} status={status.Status}");
            }
        }

        /// <summary>
        /// Публикует срабатывание датчика хоппера только от основной платы.
        /// </summary>
        private void OnHopperSensorTriggered(string deviceId)
        {
            if (deviceId == CurrentMainDeviceId)
            {
                LogDebug(DebugLogChannel.ComHopper, logHopper, $"Hopper sensor triggered by {deviceId}.");
                mainThreadQueue.Enqueue(() => hopperSensorTriggered.Invoke());
            }
        }

        /// <summary>
        /// Выбирает COM-порты для подключения с учетом режима auto-scan или явного списка.
        /// </summary>
        private string[] SelectPorts(string[] availablePorts)
        {
            if (availablePorts == null || availablePorts.Length == 0)
            {
                return Array.Empty<string>();
            }

            if (useAllDetectedPorts)
            {
                var sortedPorts = (string[])availablePorts.Clone();
                Array.Sort(sortedPorts, StringComparer.OrdinalIgnoreCase);
                return sortedPorts;
            }

            HashSet<string> explicitSet = new(StringComparer.OrdinalIgnoreCase);
            foreach (var explicitPort in explicitPorts ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(explicitPort))
                {
                    explicitSet.Add(explicitPort);
                }
            }

            List<string> selectedPorts = new();
            foreach (var availablePort in availablePorts)
            {
                if (explicitSet.Contains(availablePort))
                {
                    selectedPorts.Add(availablePort);
                }
            }

            return selectedPorts.ToArray();
        }

        /// <summary>
        /// Проверяет, относится ли роль устройства к призовой витрине.
        /// </summary>
        private bool IsPrizeStandRole(string role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return false;
            }

            string[] roles = prizeStandRoles == null || prizeStandRoles.Length == 0 ? DefaultPrizeStandRoles : prizeStandRoles;
            foreach (var candidate in roles)
            {
                if (IsRole(role, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private ComDeviceConnection[] CopyDeviceValues()
        {
            var snapshot = new ComDeviceConnection[devices.Count];
            var index = 0;
            foreach (var device in devices.Values)
            {
                snapshot[index++] = device;
            }

            return snapshot;
        }

        private string CurrentMainDeviceId
        {
            get
            {
                lock (devicesLock)
                {
                    return mainDeviceId;
                }
            }
        }

        private string CurrentPrizeStandDeviceId
        {
            get
            {
                lock (devicesLock)
                {
                    return prizeStandDeviceId;
                }
            }
        }

        private void ResetDeviceSelections()
        {
            lock (devicesLock)
            {
                mainDeviceId = "";
                prizeStandDeviceId = "";
            }
        }

        private string GetFirstDeviceId()
        {
            foreach (var deviceId in devices.Keys)
            {
                return deviceId;
            }

            return string.Empty;
        }

        private static void RunLoggedTask(Func<UniTask> taskFactory, CancellationToken token, string operationName)
        {
            RunLoggedTaskAsync(taskFactory, token, operationName).Forget(UnityEngine.Debug.LogException);
        }

        private bool ShouldLog(DebugLogChannel channel, bool localSwitchEnabled)
        {
            if (!localSwitchEnabled)
            {
                return false;
            }

            var service = debugLogService ?? DebugLogService.Instance;
            return service == null || service.IsChannelEnabled(channel);
        }

        private void LogDebug(DebugLogChannel channel, bool localSwitchEnabled, string message)
        {
            if (localSwitchEnabled)
            {
                LogDebug(channel, message);
            }
        }

        private void LogDebug(DebugLogChannel channel, string message)
        {
            var service = debugLogService ?? DebugLogService.Instance;
            if (service != null)
            {
                service.Log(channel, $"[ComSystem] {message}", this);
                return;
            }

            UnityEngine.Debug.Log($"[{channel}] [ComSystem] {message}", this);
        }

        private void LogWarning(DebugLogChannel channel, bool localSwitchEnabled, string message)
        {
            if (!localSwitchEnabled)
            {
                return;
            }

            var service = debugLogService ?? DebugLogService.Instance;
            if (service != null)
            {
                service.LogWarning(channel, $"[ComSystem] {message}", this);
                return;
            }

            UnityEngine.Debug.LogWarning($"[{channel}] [ComSystem] {message}", this);
        }

        private static string FormatDeviceDebug(ComDeviceConnection device)
        {
            if (device == null)
            {
                return "<null>";
            }

            return $"{device.DeviceId} port={device.PortName} role={FormatDebugValue(device.Role)} mac={FormatDebugValue(device.MacAddress)}";
        }

        private static string FormatDebugValue(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<unknown>" : value;
        }

        private static string FormatList(IEnumerable<string> values)
        {
            if (values == null)
            {
                return "";
            }

            List<string> filtered = new();
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    filtered.Add(value.Trim());
                }
            }

            return filtered.Count > 0 ? string.Join(", ", filtered) : "";
        }

        private static async UniTask RunLoggedTaskAsync(Func<UniTask> taskFactory, CancellationToken token, string operationName)
        {
            try
            {
                await UniTask.RunOnThreadPool(taskFactory, configureAwait: false, cancellationToken: token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning($"[ComSystem] Background task '{operationName}' failed: {exception.Message}");
            }
        }

        /// <summary>
        /// Сравнивает роли устройств без учета регистра и лишних пробелов.
        /// </summary>
        private static bool IsRole(string role, string expected)
        {
            return !string.IsNullOrWhiteSpace(role) &&
                   !string.IsNullOrWhiteSpace(expected) &&
                   string.Equals(role.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Проверяет, можно ли выдавать приз из ячейки с таким статусом.
        /// </summary>
        private static bool IsReadyToOpen(PrizeBoxStatus status)
        {
            return status == PrizeBoxStatus.Ok || status == PrizeBoxStatus.OkWithCard;
        }

        /// <summary>
        /// Пытается разобрать строку вида BOX{номер}_{статус} от призовой витрины.
        /// </summary>
        private static bool TryParsePrizeBoxStatus(string line, out PrizeBoxStatusEntry status)
        {
            status = default;

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("BOX", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] parts = line.Substring(3).Split('_');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int boxNumber))
            {
                return false;
            }

            status = new PrizeBoxStatusEntry(boxNumber, MapPrizeStatus(parts[1]));
            return true;
        }

        /// <summary>
        /// Преобразует строковый статус прошивки в enum проекта.
        /// </summary>
        private static PrizeBoxStatus MapPrizeStatus(string status)
        {
            return status switch
            {
                "OK" => PrizeBoxStatus.Ok,
                "OK+CARD" => PrizeBoxStatus.OkWithCard,
                "EMPTY" => PrizeBoxStatus.Empty,
                "OPEN" => PrizeBoxStatus.Open,
                "OPENED" => PrizeBoxStatus.Opened,
                "OPEN_ERROR" => PrizeBoxStatus.OpenError,
                "ERROR" => PrizeBoxStatus.Error,
                _ => PrizeBoxStatus.ParsingError
            };
        }

        /// <summary>
        /// Сохраняет баланс кредитов, если включено сохранение между сессиями.
        /// </summary>
        private void SaveCredits()
        {
            if (!saveCreditsBetweenSessions)
            {
                return;
            }

            PlayerPrefs.SetInt(creditsPlayerPrefsKey, credits);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Освобождает токен запуска, если COM-система не смогла перейти в рабочее состояние.
        /// </summary>
        private void ResetSystemToken()
        {
            systemCts?.Cancel();
            systemCts?.Dispose();
            systemCts = null;
        }

        /// <summary>
        /// Очищает очереди событий, накопленные между потоками.
        /// </summary>
        private void ClearQueues()
        {
            while (mainThreadQueue.TryDequeue(out _)) { }
            while (coinQueue.TryDequeue(out _)) { }
            while (inputQueue.TryDequeue(out _)) { }
            while (adcQueue.TryDequeue(out _)) { }
            ClearPrizeStatusQueue();
        }

        /// <summary>
        /// Очищает очередь статусов призовой витрины перед новым запросом.
        /// </summary>
        private void ClearPrizeStatusQueue()
        {
            while (prizeStatusQueue.TryDequeue(out _)) { }
        }

        /// <summary>
        /// Инкапсулирует одно физическое COM-подключение, авторизацию и фоновый обмен с платой.
        /// </summary>
        private sealed class ComDeviceConnection
        {
            private const long CoinDebounceMs = 250;

            private static readonly byte[] AesEncryptedKey =
            {
            0x37, 0x36, 0x35, 0x34, 0x33, 0x32, 0x31, 0x30, 0x3F, 0x3E, 0x3D, 0x3C, 0x3B, 0x3A, 0x39, 0x38, 0x27, 0x26, 0x25, 0x24, 0x23, 0x22, 0x21, 0x20, 0x2F, 0x2E, 0x2D, 0x2C, 0x2B, 0x2A, 0x29, 0x28
        };

            private readonly object ioLock = new();
            private readonly IComTransportFactory transportFactory;
            private readonly int baudRate;
            private readonly int dataBits;
            private readonly SerialParity parity;
            private readonly SerialStopBits stopBits;
            private readonly int readTimeoutMs;
            private readonly int writeTimeoutMs;
            private readonly bool useEncryptedHandshake;
            private readonly int handshakeTimeoutMs;
            private readonly bool keepAlive;
            private readonly float keepAliveIntervalSeconds;
            private readonly int missedInputResponsesBeforeReconnect;
            private readonly string roleRequestCommand;
            private readonly string roleResponsePrefix;
            private readonly int roleRequestTimeoutMs;
            private readonly Action<string> logInfo;
            private readonly Action<string> logWarning;
            private readonly byte[] aesKey;

            private IComTransport serialPort;
            private CancellationTokenSource localCts;
            private CancellationToken parentToken;
            private UniTaskCompletionSource<bool> handshakeCompletion;
            private UniTaskCompletionSource<bool> chalCompletion;
            private UniTaskCompletionSource<string> roleCompletion;
            private UniTaskCompletionSource<string> macCompletion;
            private HandshakeStage handshakeStage = HandshakeStage.None;
            private string requestHex = "";
            private string lastHandshakeResponse = "";
            private long lastCoinTime;
            private int inputReceivedInCycle;
            private int missedInputResponses;
            private bool isOperational;
            private bool disposed;

            /// <summary>
            /// Создает подключение к одному физическому порту с настройками авторизации и keep-alive.
            /// </summary>
            public ComDeviceConnection(
                IComTransportFactory transportFactory,
                string portName,
                int baudRate,
                int dataBits,
                SerialParity parity,
                SerialStopBits stopBits,
                int readTimeoutMs,
                int writeTimeoutMs,
                bool useEncryptedHandshake,
                int handshakeTimeoutMs,
                bool keepAlive,
                float keepAliveIntervalSeconds,
                int missedInputResponsesBeforeReconnect,
                string roleRequestCommand,
                string roleResponsePrefix,
                int roleRequestTimeoutMs,
                Action<string> logInfo,
                Action<string> logWarning)
            {
                this.transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
                PortName = portName;
                DeviceId = portName;
                this.baudRate = baudRate;
                this.dataBits = dataBits;
                this.parity = parity;
                this.stopBits = stopBits;
                this.readTimeoutMs = Mathf.Max(1, readTimeoutMs);
                this.writeTimeoutMs = Mathf.Max(1, writeTimeoutMs);
                this.useEncryptedHandshake = useEncryptedHandshake;
                this.handshakeTimeoutMs = Mathf.Max(500, handshakeTimeoutMs);
                this.keepAlive = keepAlive;
                this.keepAliveIntervalSeconds = Mathf.Max(1f, keepAliveIntervalSeconds);
                this.missedInputResponsesBeforeReconnect = Mathf.Max(1, missedInputResponsesBeforeReconnect);
                this.roleRequestCommand = roleRequestCommand?.Trim() ?? "";
                this.roleResponsePrefix = string.IsNullOrWhiteSpace(roleResponsePrefix) ? "ROLE" : roleResponsePrefix.Trim();
                this.roleRequestTimeoutMs = Mathf.Max(200, roleRequestTimeoutMs);
                this.logInfo = logInfo;
                this.logWarning = logWarning;
                aesKey = BuildAesKey();
            }

            /// <summary>
            /// Внутренний идентификатор устройства, сейчас совпадает с именем порта.
            /// </summary>
            public string DeviceId { get; }

            /// <summary>
            /// Имя COM-порта, через который подключено устройство.
            /// </summary>
            public string PortName { get; }

            /// <summary>
            /// Роль устройства, полученная после авторизации.
            /// </summary>
            public string Role { get; private set; } = "";

            /// <summary>
            /// MAC-адрес устройства, если прошивка его возвращает.
            /// </summary>
            public string MacAddress { get; private set; } = "";

            /// <summary>
            /// Последние входные биты, полученные от устройства.
            /// </summary>
            public string LastInputBits { get; private set; } = DefaultInputBits;

            /// <summary>
            /// Последние ADC-значения, полученные от устройства.
            /// </summary>
            public string LastAdcValues { get; private set; } = "";

            /// <summary>
            /// Событие успешной авторизации подключения.
            /// </summary>
            public event Action<ComDeviceConnection> Authorized;

            /// <summary>
            /// Событие потери подключения.
            /// </summary>
            public event Action<ComDeviceConnection> Disconnected;

            /// <summary>
            /// Событие принятой монеты.
            /// </summary>
            public event Action<string> CoinAccepted;

            /// <summary>
            /// Событие получения аппаратного ввода.
            /// </summary>
            public event Action<ComInputMessage> InputReceived;

            /// <summary>
            /// Событие получения аналоговых ADC-входов.
            /// </summary>
            public event Action<ComAdcMessage> AdcReceived;

            /// <summary>
            /// Событие сырой строки, которую не обработали стандартные парсеры.
            /// </summary>
            public event Action<string, string> RawLineReceived;

            /// <summary>
            /// Событие срабатывания датчика хоппера.
            /// </summary>
            public event Action<string> HopperSensorTriggered;

            /// <summary>
            /// Открывает порт, запускает чтение и при необходимости проходит защищенное рукопожатие.
            /// </summary>
            public bool Start(CancellationToken token)
            {
                parentToken = token;
                localCts = CancellationTokenSource.CreateLinkedTokenSource(token);

                if (!OpenPort())
                {
                    return false;
                }

                LogInfo($"{PortName} read loop starting.");
                RunLoggedTask(() => ReadLoopAsync(localCts.Token), localCts.Token, $"{PortName} read loop");

                if (useEncryptedHandshake)
                {
                    LogInfo($"{PortName} encrypted handshake starting.");
                    RunLoggedTask(() => RunHandshakeAsync(localCts.Token), localCts.Token, $"{PortName} handshake");
                }
                else
                {
                    LogInfo($"{PortName} encrypted handshake disabled; marking operational.");
                    MarkOperational();
                }

                return true;
            }

            /// <summary>
            /// Отправляет команду только после перехода устройства в рабочее состояние.
            /// </summary>
            public bool Send(string command)
            {
                return Send(command, false);
            }

            /// <summary>
            /// Закрывает подключение и опционально отправляет команды безопасного выключения.
            /// </summary>
            public void Dispose(bool sendShutdownCommands)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;

                try
                {
                    localCts?.Cancel();
                }
                catch
                {
                    // ignored
                }

                if (sendShutdownCommands)
                {
                    Send("2170", true);
                    Send("2130", true);
                }

                // Даем фоновому потоку чтения время выйти из ReadLine по таймауту (который равен 50мс)
                System.Threading.Thread.Sleep(80);

                ClosePort();
                localCts?.Dispose();
                localCts = null;
            }

            /// <summary>
            /// Создает serial-транспорт, открывает порт и очищает его буферы.
            /// </summary>
            private bool OpenPort()
            {
                try
                {
                    LogInfo($"{PortName} opening serial port. settings={baudRate}/{dataBits}/{parity}/{stopBits} readTimeoutMs={readTimeoutMs} writeTimeoutMs={writeTimeoutMs}");
                    lock (ioLock)
                    {
                        serialPort = transportFactory.Create(new SerialSettings(
                            PortName,
                            baudRate,
                            dataBits,
                            parity,
                            stopBits,
                            readTimeoutMs,
                            writeTimeoutMs));

                        serialPort.Open();
                        serialPort.DiscardInBuffer();
                        serialPort.DiscardOutBuffer();
                    }

                    lastCoinTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    LogInfo($"{PortName} serial port opened successfully.");
                    return true;
                }
                catch (Exception exception)
                {
                    LogWarning($"Failed to open {PortName}: {exception.GetType().Name}: {exception.Message}");
                    return false;
                }
            }

            /// <summary>
            /// Фоновый цикл чтения строк из serial-порта.
            /// </summary>
            private async UniTask ReadLoopAsync(CancellationToken token)
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        string line;
                        IComTransport port;
                        lock (ioLock)
                        {
                            port = serialPort;
                        }

                        if (port == null || !port.IsOpen)
                        {
                            await UniTask.Delay(10, DelayType.Realtime, PlayerLoopTiming.Update, token);
                            continue;
                        }

                        line = port.ReadLine();
                        ProcessLine(line);
                    }
                    catch (TimeoutException)
                    {
                        await UniTask.Delay(1, DelayType.Realtime, PlayerLoopTiming.Update, token);
                        await UniTask.SwitchToThreadPool();
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception exception) when (exception is IOException || exception is InvalidOperationException)
                    {
                        LogWarning($"{PortName} connection lost: {exception.Message}. Reconnecting...");
                        await ReconnectAsync(token);
                    }
                    catch (Exception exception)
                    {
                        LogWarning($"{PortName} read error: {exception.Message}");
                    }
                }
            }

            /// <summary>
            /// Маршрутизирует входящую строку в handshake, идентификацию, ввод или сырые события.
            /// </summary>
            private void ProcessLine(string line)
            {
                line = (line ?? "").Trim();
                if (string.IsNullOrEmpty(line))
                {
                    return;
                }

                if (handshakeStage == HandshakeStage.WaitingChal || handshakeStage == HandshakeStage.WaitingRaes)
                {
                    ProcessHandshakeLine(line);
                    return;
                }

                if (!isOperational)
                {
                    return;
                }

                if (TryCompleteRole(line) || TryCompleteMac(line))
                {
                    return;
                }

                if (string.Equals(line, "COIN", StringComparison.OrdinalIgnoreCase))
                {
                    HandleCoin();
                    return;
                }

                if (line.StartsWith("INPUT", StringComparison.Ordinal))
                {
                    HandleInput(line);
                    return;
                }

                if (line.StartsWith("ADC", StringComparison.OrdinalIgnoreCase))
                {
                    HandleAdc(line);
                    return;
                }

                RawLineReceived?.Invoke(DeviceId, line);
            }

            /// <summary>
            /// Выполняет CHAL/RAES-авторизацию, совместимую с текущей прошивкой устройства.
            /// </summary>
            private async UniTask RunHandshakeAsync(CancellationToken token)
            {
                handshakeStage = HandshakeStage.WaitingChal;
                handshakeCompletion = NewCompletion<bool>();
                chalCompletion = NewCompletion<bool>();
                requestHex = GenerateRandomBlock16Hex();
                LogInfo($"{PortName} handshake: sending challenge request command 07<random16>.");
                Send("07" + requestHex, true);

                var (chalReceived, _) = await UniTask.WhenAny(
                    chalCompletion.Task,
                    UniTask.Delay(Mathf.Min(1200, Mathf.Max(250, handshakeTimeoutMs / 4)), DelayType.Realtime, PlayerLoopTiming.Update, token));
                await UniTask.SwitchToThreadPool();
                if (!chalReceived)
                {
                    FailHandshake("No CHAL response.");
                    return;
                }

                LogInfo($"{PortName} handshake: CHAL received, waiting RAES.");
                var (handshakeFinished, handshakeSucceeded) = await UniTask.WhenAny(
                    handshakeCompletion.Task,
                    UniTask.Delay(handshakeTimeoutMs, DelayType.Realtime, PlayerLoopTiming.Update, token));
                await UniTask.SwitchToThreadPool();
                if (handshakeFinished && handshakeSucceeded)
                {
                    LogInfo($"{PortName} handshake: RAES verified.");
                    MarkOperational();
                    return;
                }

                FailHandshake($"Handshake timeout. Last response: {lastHandshakeResponse}");
            }

            /// <summary>
            /// Обрабатывает строку текущего этапа защищенного рукопожатия.
            /// </summary>
            private void ProcessHandshakeLine(string line)
            {
                lastHandshakeResponse = line;

                if (handshakeStage == HandshakeStage.WaitingChal && line.StartsWith("CHAL", StringComparison.Ordinal))
                {
                    LogInfo($"{PortName} handshake line received: CHAL length={line.Length}.");
                    HandleChal(line);
                    return;
                }

                if (handshakeStage == HandshakeStage.WaitingRaes && line.StartsWith("RAES", StringComparison.Ordinal))
                {
                    LogInfo($"{PortName} handshake line received: RAES length={line.Length}.");
                    HandleRaes(line);
                }
            }

            /// <summary>
            /// Обрабатывает CHAL-вызов устройства и отправляет зашифрованный ответ.
            /// </summary>
            private void HandleChal(string line)
            {
                if (line.Length < 36)
                {
                    FailHandshake("Invalid CHAL length.");
                    return;
                }

                string hex = line.Substring(4, 32);
                if (!IsHexString(hex, 32))
                {
                    FailHandshake("Invalid CHAL hex.");
                    return;
                }

                chalCompletion?.TrySetResult(true);
                Send("08" + EncryptHexBlock16(hex), true);
                handshakeStage = HandshakeStage.WaitingRaes;
            }

            /// <summary>
            /// Проверяет RAES-ответ устройства на соответствие исходному запросу.
            /// </summary>
            private void HandleRaes(string line)
            {
                if (line.Length < 36)
                {
                    FailHandshake("Invalid RAES length.");
                    return;
                }

                string hex = line.Substring(4, 32);
                if (!IsHexString(hex, 32))
                {
                    FailHandshake("Invalid RAES hex.");
                    return;
                }

                string decrypted = DecryptHexBlock16(hex);
                if (!string.Equals(decrypted, requestHex, StringComparison.OrdinalIgnoreCase))
                {
                    FailHandshake("RAES does not match request.");
                    return;
                }

                handshakeStage = HandshakeStage.Completed;
                handshakeCompletion?.TrySetResult(true);
            }

            /// <summary>
            /// Завершает авторизацию ошибкой и помечает устройство отключенным.
            /// </summary>
            private void FailHandshake(string message)
            {
                LogWarning($"{PortName} authorization failed: {message}");
                handshakeStage = HandshakeStage.Failed;
                handshakeCompletion?.TrySetResult(false);
                NotifyDisconnected();
            }

            /// <summary>
            /// Переводит устройство в рабочее состояние и запускает keep-alive/идентификацию.
            /// </summary>
            private void MarkOperational()
            {
                isOperational = true;
                handshakeStage = HandshakeStage.Completed;
                missedInputResponses = 0;
                LogInfo($"{PortName} marked operational. Sending startup commands.");

                Send("2171", true);
                Send("890003", true);
                Send("8600", true);

                if (keepAlive)
                {
                    RunLoggedTask(() => KeepAliveLoopAsync(localCts.Token), localCts.Token, $"{PortName} keep alive");
                }

                RunLoggedTask(() => ResolveIdentityAsync(localCts.Token), localCts.Token, $"{PortName} identity resolution");
            }

            /// <summary>
            /// Запрашивает роль и MAC-адрес устройства после успешной авторизации.
            /// </summary>
            private async UniTask ResolveIdentityAsync(CancellationToken token)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(roleRequestCommand))
                    {
                        roleCompletion = NewCompletion<string>();
                        LogInfo($"{PortName} identity: requesting role with command {roleRequestCommand}, expecting prefix {roleResponsePrefix}.");
                        Send(roleRequestCommand);

                        var (roleReceived, role) = await UniTask.WhenAny(
                            roleCompletion.Task,
                            UniTask.Delay(roleRequestTimeoutMs, DelayType.Realtime, PlayerLoopTiming.Update, token));
                        await UniTask.SwitchToThreadPool();
                        if (roleReceived)
                        {
                            Role = role;
                            LogInfo($"{PortName} identity: role received '{Role}'.");
                        }
                        else
                        {
                            LogInfo($"{PortName} identity: role request timed out after {roleRequestTimeoutMs} ms.");
                        }
                    }

                    macCompletion = NewCompletion<string>();
                    LogInfo($"{PortName} identity: requesting MAC with command 06.");
                    Send("06");

                    var (macReceived, macAddress) = await UniTask.WhenAny(
                        macCompletion.Task,
                        UniTask.Delay(roleRequestTimeoutMs, DelayType.Realtime, PlayerLoopTiming.Update, token));
                    await UniTask.SwitchToThreadPool();
                    if (macReceived)
                    {
                        MacAddress = macAddress;
                        LogInfo($"{PortName} identity: MAC received {MacAddress}.");
                    }
                    else
                    {
                        LogInfo($"{PortName} identity: MAC request timed out after {roleRequestTimeoutMs} ms.");
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                finally
                {
                    Authorized?.Invoke(this);
                }
            }

            /// <summary>
            /// Периодически проверяет, что плата отвечает на запросы ввода, и инициирует переподключение.
            /// </summary>
            private async UniTask KeepAliveLoopAsync(CancellationToken token)
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(keepAliveIntervalSeconds), DelayType.Realtime, PlayerLoopTiming.Update, token);
                        await UniTask.SwitchToThreadPool();
                        inputReceivedInCycle = 0;
                        Send(RequestInputCommand);
                        await UniTask.Delay(1000, DelayType.Realtime, PlayerLoopTiming.Update, token);
                        await UniTask.SwitchToThreadPool();

                        if (inputReceivedInCycle == 0)
                        {
                            missedInputResponses++;
                            if (missedInputResponses >= missedInputResponsesBeforeReconnect)
                            {
                                await ReconnectAsync(token);
                            }
                        }
                        else
                        {
                            missedInputResponses = 0;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        LogWarning($"{PortName} keep alive error: {exception.Message}");
                    }
                }
            }

            /// <summary>
            /// Переоткрывает порт и повторно проходит авторизацию после потери keep-alive.
            /// </summary>
            private async UniTask ReconnectAsync(CancellationToken token)
            {
                isOperational = false;
                Role = "";
                MacAddress = "";
                LastInputBits = DefaultInputBits;
                ClosePort();

                int retryCount = 0;
                const int maxRetries = 10;

                while (!token.IsCancellationRequested && retryCount < maxRetries)
                {
                    await UniTask.Delay(1000, DelayType.Realtime, PlayerLoopTiming.Update, token);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    await UniTask.SwitchToThreadPool();
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    if (OpenPort())
                    {
                        LogWarning($"{PortName} reconnected successfully after {retryCount + 1} retries.");
                        if (useEncryptedHandshake)
                        {
                            await RunHandshakeAsync(token);
                        }
                        else
                        {
                            MarkOperational();
                        }
                        return;
                    }

                    retryCount++;
                    LogWarning($"{PortName} reconnect attempt {retryCount} failed. Retrying...");
                }

                if (!token.IsCancellationRequested)
                {
                    NotifyDisconnected();
                }
            }

            /// <summary>
            /// Обрабатывает событие монеты с debounce и подтверждением плате.
            /// </summary>
            private void HandleCoin()
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if (now - lastCoinTime < CoinDebounceMs)
                {
                    return;
                }

                lastCoinTime = now;
                Send("10", true);
                CoinAccepted?.Invoke(DeviceId);
            }

            /// <summary>
            /// Разбирает строку INPUT, обновляет биты ввода и отдельно отслеживает датчик хоппера.
            /// </summary>
            private void HandleInput(string line)
            {
                inputReceivedInCycle = 1;

                if (line.Length <= 5)
                {
                    return;
                }

                string rawData = line.Substring(5);
                if (string.IsNullOrWhiteSpace(rawData))
                {
                    return;
                }

                if (long.TryParse(rawData, out long rawValue))
                {
                    string rawBinary = Convert.ToString(rawValue, 2);
                    if (rawBinary.Length >= 2 && rawBinary[1] == '0')
                    {
                        HopperSensorTriggered?.Invoke(DeviceId);
                    }
                }

                LastInputBits = DecimalToReversedBinary(rawData);
                InputReceived?.Invoke(new ComInputMessage(DeviceId, LastInputBits));
            }

            /// <summary>
            /// Разбирает строку ADC и публикует сырые аналоговые значения.
            /// </summary>
            private void HandleAdc(string line)
            {
                string rawData = line.Length > 3 ? line.Substring(3).Trim() : "";
                if (rawData.StartsWith(":", StringComparison.Ordinal) || rawData.StartsWith("=", StringComparison.Ordinal))
                {
                    rawData = rawData.Substring(1).Trim();
                }

                LastAdcValues = rawData;
                AdcReceived?.Invoke(new ComAdcMessage(DeviceId, LastAdcValues));
            }

            /// <summary>
            /// Отправляет команду в порт, опционально обходя проверку рабочего состояния для handshake.
            /// </summary>
            private bool Send(string command, bool bypassOperational)
            {
                if (string.IsNullOrWhiteSpace(command))
                {
                    return false;
                }

                if (!bypassOperational && !isOperational)
                {
                    return false;
                }

                try
                {
                    lock (ioLock)
                    {
                        if (serialPort == null || !serialPort.IsOpen)
                        {
                            return false;
                        }

                        serialPort.WriteLine(command);
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    LogWarning($"Failed to write '{command}' to {PortName}: {exception.Message}");
                    return false;
                }
            }

            private void LogWarning(string message)
            {
                if (logWarning != null)
                {
                    logWarning(message);
                    return;
                }

                UnityEngine.Debug.LogWarning($"[ComSystem] {message}");
            }

            /// <summary>
            /// Пытается завершить ожидающий запрос роли устройства.
            /// </summary>
            private bool TryCompleteRole(string line)
            {
                if (roleCompletion == null || roleCompletion.Task.Status.IsCompleted())
                {
                    return false;
                }

                string trimmed = line.Trim();
                if (!trimmed.StartsWith(roleResponsePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string role = trimmed.Substring(roleResponsePrefix.Length).Trim();
                if (role.StartsWith(":", StringComparison.Ordinal) || role.StartsWith("=", StringComparison.Ordinal))
                {
                    role = role.Substring(1).Trim();
                }

                if (string.IsNullOrWhiteSpace(role))
                {
                    return false;
                }

                Role = role;
                roleCompletion.TrySetResult(role);
                return true;
            }

            /// <summary>
            /// Пытается завершить ожидающий запрос MAC-адреса устройства.
            /// </summary>
            private bool TryCompleteMac(string line)
            {
                if (macCompletion == null || macCompletion.Task.Status.IsCompleted())
                {
                    return false;
                }

                string trimmed = line.Trim();
                if (trimmed.Length != 12 || !IsHexString(trimmed))
                {
                    return false;
                }

                MacAddress = trimmed.ToUpperInvariant();
                macCompletion.TrySetResult(MacAddress);
                return true;
            }

            /// <summary>
            /// Единообразно переводит подключение в отключенное состояние и публикует событие.
            /// </summary>
            private void NotifyDisconnected()
            {
                if (disposed)
                {
                    return;
                }

                isOperational = false;
                LogInfo($"{PortName} notifying disconnected.");
                localCts?.Cancel();
                ClosePort();
                Disconnected?.Invoke(this);
            }

            /// <summary>
            /// Закрывает и освобождает serial-транспорт под lock.
            /// </summary>
            private void ClosePort()
            {
                lock (ioLock)
                {
                    if (serialPort == null)
                    {
                        return;
                    }

                    try
                    {
                        if (serialPort.IsOpen)
                        {
                            serialPort.Close();
                        }
                    }
                    catch
                    {
                        // ignored
                    }

                    serialPort.Dispose();
                    serialPort = null;
                    LogInfo($"{PortName} serial port closed.");
                }
            }

            private void LogInfo(string message)
            {
                logInfo?.Invoke(message);
            }

            private string EncryptHexBlock16(string plainHex)
            {
                byte[] plain = HexToBytes(plainHex);
                using Aes aes = Aes.Create();
                aes.Key = aesKey;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                using ICryptoTransform encryptor = aes.CreateEncryptor();
                return BytesToHex(encryptor.TransformFinalBlock(plain, 0, plain.Length));
            }

            /// <summary>
            /// Расшифровывает 16-байтовый AES-блок в hex-формате.
            /// </summary>
            private string DecryptHexBlock16(string cipherHex)
            {
                byte[] cipher = HexToBytes(cipherHex);
                using Aes aes = Aes.Create();
                aes.Key = aesKey;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                using ICryptoTransform decryptor = aes.CreateDecryptor();
                return BytesToHex(decryptor.TransformFinalBlock(cipher, 0, cipher.Length));
            }

            private static UniTaskCompletionSource<T> NewCompletion<T>()
            {
                return new UniTaskCompletionSource<T>();
            }

            /// <summary>
            /// Восстанавливает AES-ключ из обфусцированного массива.
            /// </summary>
            private static byte[] BuildAesKey()
            {
                byte[] key = new byte[32];
                for (int i = 0; i < key.Length; i++)
                {
                    key[i] = (byte)(AesEncryptedKey[i] ^ 0x37);
                }

                return key;
            }

            /// <summary>
            /// Переводит десятичное значение платы в обратный набор битов, удобный для игровых проверок.
            /// </summary>
            private static string DecimalToReversedBinary(string decimalString)
            {
                if (!long.TryParse(decimalString, out long value))
                {
                    return DefaultInputBits;
                }

                string binary = Convert.ToString(value, 2);
                char[] chars = binary.ToCharArray();
                Array.Reverse(chars);
                string reversed = new string(chars);
                return reversed.Length >= DefaultInputBits.Length
                    ? reversed
                    : reversed.PadRight(DefaultInputBits.Length, '0');
            }

            /// <summary>
            /// Генерирует случайный 16-байтовый блок для handshake.
            /// </summary>
            private static string GenerateRandomBlock16Hex()
            {
                byte[] buffer = new byte[16];
                RandomNumberGenerator.Fill(buffer);
                return BytesToHex(buffer);
            }

            /// <summary>
            /// Преобразует hex-строку в массив байтов.
            /// </summary>
            private static byte[] HexToBytes(string hex)
            {
                if (hex == null)
                {
                    throw new ArgumentNullException(nameof(hex));
                }

                hex = hex.Trim();
                if (hex.Length % 2 != 0)
                {
                    throw new InvalidOperationException("Hex string length must be even.");
                }

                byte[] result = new byte[hex.Length / 2];
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                }

                return result;
            }

            /// <summary>
            /// Преобразует байты в uppercase hex-строку.
            /// </summary>
            private static string BytesToHex(byte[] bytes)
            {
                StringBuilder builder = new(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.AppendFormat("{0:X2}", bytes[i]);
                }

                return builder.ToString();
            }

            /// <summary>
            /// Проверяет hex-строку с требуемой длиной.
            /// </summary>
            private static bool IsHexString(string value, int expectedLength)
            {
                return !string.IsNullOrWhiteSpace(value) && value.Length == expectedLength && IsHexString(value);
            }

            /// <summary>
            /// Проверяет, состоит ли строка только из hex-символов.
            /// </summary>
            private static bool IsHexString(string value)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    if (!Uri.IsHexDigit(value[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Состояния защищенного рукопожатия с контроллером.
            /// </summary>
            private enum HandshakeStage
            {
                None,
                WaitingChal,
                WaitingRaes,
                Completed,
                Failed
            }
        }

        /// <summary>
        /// Сырые данные о статусе одной призовой ячейки из очереди ответов витрины.
        /// </summary>
        private readonly struct PrizeBoxStatusEntry
        {
            /// <summary>
            /// Номер ячейки витрины.
            /// </summary>
            public readonly int BoxNumber;

            /// <summary>
            /// Последний полученный статус ячейки.
            /// </summary>
            public readonly PrizeBoxStatus Status;

            /// <summary>
            /// Создает запись статуса одной ячейки.
            /// </summary>
            public PrizeBoxStatusEntry(int boxNumber, PrizeBoxStatus status)
            {
                BoxNumber = boxNumber;
                Status = status;
            }
        }
    }

}
