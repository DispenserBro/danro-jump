using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace DanroJump.Hardware.Com.Input
{
    /// <summary>
    /// Транслирует входные биты основной COM-платы в кастомное устройство Unity Input System.
    /// </summary>
    [DefaultExecutionOrder(-8500)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hardware/COM Controller Input Bridge")]
    public sealed class ComControllerInputBridge : MonoBehaviour
    {
        private static ComControllerInputBridge instance;

        [SerializeField] private bool createAutomatically = true;
        [SerializeField] private bool useComSystemInstanceFallback = true;
        [SerializeField] private bool activeLow = true;
        [SerializeField] private bool removeDeviceOnDestroy;
        [SerializeField] private List<ComControllerButtonBinding> bindings = CreateDefaultBindings();

        private IComInputSource inputSource;
        private IComSystem comSystemSource;
        private ComControllerDevice device;
        private ComControllerState currentState;
        private bool sourceSubscribed;

        [Inject]
        public void Construct([InjectOptional] IComInputSource injectedInputSource = null)
        {
            if (injectedInputSource != null)
            {
                SetInputSource(injectedInputSource);
            }
        }

        public static ComControllerState BuildState(
            string inputBits,
            IReadOnlyList<ComControllerButtonBinding> buttonBindings,
            bool activeLow)
        {
            var state = new ComControllerState();
            if (string.IsNullOrEmpty(inputBits) || buttonBindings == null)
            {
                return state;
            }

            for (int index = 0; index < buttonBindings.Count; index++)
            {
                ComControllerButtonBinding binding = buttonBindings[index];
                int bitIndex = binding.BitIndex;
                if (bitIndex < 0 || bitIndex >= inputBits.Length)
                {
                    continue;
                }

                char bit = inputBits[bitIndex];
                bool pressed = activeLow ? bit == '0' : bit == '1';
                if (pressed)
                {
                    state.Set(binding.Control, true);
                }
            }

            return state;
        }

        public static ComControllerState BuildAdcState(ComControllerState currentState, string adcValues)
        {
            currentState.adc00 = TryReadAdcValue(adcValues, 0, out float adc00) ? adc00 : 0f;
            currentState.adc01 = TryReadAdcValue(adcValues, 1, out float adc01) ? adc01 : 0f;
            return currentState;
        }

        public static List<ComControllerButtonBinding> CreateDefaultBindings()
        {
            return new List<ComControllerButtonBinding>
            {
                new(0, ComControllerControl.Button00),
                new(1, ComControllerControl.Button01),
                new(2, ComControllerControl.Button02),
                new(3, ComControllerControl.Button03),
                new(4, ComControllerControl.Button04),
                new(5, ComControllerControl.Button05),
                new(6, ComControllerControl.Button06),
                new(7, ComControllerControl.Button07),
                new(8, ComControllerControl.Button08),
                new(9, ComControllerControl.Button09),
                new(10, ComControllerControl.Button10),
                new(11, ComControllerControl.Button11),
                new(12, ComControllerControl.Button12),
                new(13, ComControllerControl.Button13),
                new(14, ComControllerControl.Button14),
                new(15, ComControllerControl.Button15),
                new(16, ComControllerControl.Button16),
                new(17, ComControllerControl.Button17),
                new(18, ComControllerControl.Button18),
                new(19, ComControllerControl.Button19),
                new(20, ComControllerControl.Button20),
                new(21, ComControllerControl.Button21),
                new(22, ComControllerControl.Button22),
                new(23, ComControllerControl.Button23)
            };
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            MoveToPersistentScene(gameObject);

            if (bindings == null || bindings.Count == 0)
            {
                bindings = CreateDefaultBindings();
            }

            if (createAutomatically)
            {
                EnsureDevice();
            }
        }

        private void OnEnable()
        {
            TryAttachSource();
        }

        private void Update()
        {
            if (!sourceSubscribed)
            {
                TryAttachSource();
            }
        }

        private void OnDisable()
        {
            QueueReleasedState();
            DetachSource();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            DetachSource();

            if (removeDeviceOnDestroy && device != null && device.added)
            {
                InputSystem.RemoveDevice(device);
            }

            device = null;
        }

        private static void MoveToPersistentScene(GameObject gameObject)
        {
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void SetInputSource(IComInputSource source)
        {
            if (ReferenceEquals(inputSource, source))
            {
                return;
            }

            DetachSource();
            inputSource = source;
            TryAttachSource();
        }

        private void TryAttachSource()
        {
            if (sourceSubscribed)
            {
                return;
            }

            if (inputSource == null && useComSystemInstanceFallback)
            {
                inputSource = ComSystem.Instance;
            }

            if (inputSource == null)
            {
                return;
            }

            EnsureDevice();
            inputSource.MainInputReceived.AddListener(HandleInputBits);
            inputSource.MainAdcReceived.AddListener(HandleAdcValues);
            sourceSubscribed = true;

            comSystemSource = inputSource as IComSystem;
            if (comSystemSource != null)
            {
                comSystemSource.DeviceDisconnected.AddListener(HandleDeviceDisconnected);
            }

            HandleInputBits(inputSource.LastMainInputBits);
            HandleAdcValues(inputSource.LastMainAdcValues);
        }

        private void DetachSource()
        {
            if (!sourceSubscribed || inputSource == null)
            {
                return;
            }

            inputSource.MainInputReceived.RemoveListener(HandleInputBits);
            inputSource.MainAdcReceived.RemoveListener(HandleAdcValues);

            if (comSystemSource != null)
            {
                comSystemSource.DeviceDisconnected.RemoveListener(HandleDeviceDisconnected);
                comSystemSource = null;
            }

            sourceSubscribed = false;
        }

        private void EnsureDevice()
        {
            device ??= ComControllerDevice.AddDevice();
        }

        private void HandleInputBits(string inputBits)
        {
            EnsureDevice();
            var adc00 = currentState.adc00;
            var adc01 = currentState.adc01;
            currentState = BuildState(inputBits, bindings, activeLow);
            currentState.adc00 = adc00;
            currentState.adc01 = adc01;
            InputSystem.QueueStateEvent(device, currentState);
        }

        private void HandleAdcValues(string adcValues)
        {
            EnsureDevice();
            currentState = BuildAdcState(currentState, adcValues);
            InputSystem.QueueStateEvent(device, currentState);
        }

        private void HandleDeviceDisconnected(string deviceId)
        {
            if (inputSource is IComSystem comSystem && deviceId != comSystem.MainDeviceId)
            {
                return;
            }

            QueueReleasedState();
        }

        private void QueueReleasedState()
        {
            if (device == null)
            {
                return;
            }

            InputSystem.QueueStateEvent(device, default(ComControllerState));
            currentState = default;
        }

        private static bool TryReadAdcValue(string adcValues, int valueIndex, out float value)
        {
            value = 0f;
            if (string.IsNullOrWhiteSpace(adcValues) || valueIndex < 0)
            {
                return false;
            }

            string payload = adcValues.Trim();
            if (payload.StartsWith("ADC", System.StringComparison.OrdinalIgnoreCase))
            {
                payload = payload.Substring(3).Trim();
            }

            if (TryReadKeyValueNumber(payload, valueIndex, out value))
            {
                return true;
            }

            return TryReadNumber(payload, valueIndex, out value);
        }

        private static bool TryReadKeyValueNumber(string payload, int valueIndex, out float value)
        {
            value = 0f;
            var currentIndex = 0;
            for (var index = 0; index < payload.Length; index++)
            {
                if (payload[index] != '=' && payload[index] != ':')
                {
                    continue;
                }

                if (!TryParseNumberAt(payload, index + 1, out value, out _))
                {
                    continue;
                }

                if (currentIndex == valueIndex)
                {
                    return true;
                }

                currentIndex++;
            }

            return false;
        }

        private static bool TryReadNumber(string payload, int valueIndex, out float value)
        {
            value = 0f;
            var currentIndex = 0;
            for (var index = 0; index < payload.Length; index++)
            {
                if (!IsNumberStart(payload, index))
                {
                    continue;
                }

                if (!TryParseNumberAt(payload, index, out value, out int nextIndex))
                {
                    continue;
                }

                if (currentIndex == valueIndex)
                {
                    return true;
                }

                currentIndex++;
                index = nextIndex;
            }

            return false;
        }

        private static bool TryParseNumberAt(string payload, int startIndex, out float value, out int nextIndex)
        {
            value = 0f;
            nextIndex = startIndex;
            while (nextIndex < payload.Length && char.IsWhiteSpace(payload[nextIndex]))
            {
                nextIndex++;
            }

            var numberStart = nextIndex;
            if (nextIndex < payload.Length && (payload[nextIndex] == '-' || payload[nextIndex] == '+'))
            {
                nextIndex++;
            }

            while (nextIndex < payload.Length && (char.IsDigit(payload[nextIndex]) || payload[nextIndex] == '.'))
            {
                nextIndex++;
            }

            if (nextIndex == numberStart || (nextIndex == numberStart + 1 && (payload[numberStart] == '-' || payload[numberStart] == '+')))
            {
                return false;
            }

            return float.TryParse(
                payload.Substring(numberStart, nextIndex - numberStart),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        private static bool IsNumberStart(string payload, int index)
        {
            char character = payload[index];
            if (char.IsDigit(character))
            {
                return true;
            }

            return (character == '-' || character == '+') &&
                index + 1 < payload.Length &&
                char.IsDigit(payload[index + 1]);
        }
    }
}
