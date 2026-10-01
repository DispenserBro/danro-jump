using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Events;

namespace DanroJump.Hardware.Com
{
    /// <summary>
    /// Единая процедура выдачи капсулы через хоппер: запуск мотора, ожидание датчика и остановка.
    /// </summary>
    public static class ComHopperDispenser
    {
        public const string StartMotorCommand = "301";
        public const string StopMotorCommand = "300";
        public const int DefaultTimeoutSeconds = 10;

        public static UniTask<ComHopperDispenseResult> DispenseAsync(
            IComSystem com,
            CancellationToken cancellationToken = default)
        {
            return DispenseAsync(com, TimeSpan.FromSeconds(DefaultTimeoutSeconds), cancellationToken);
        }

        public static async UniTask<ComHopperDispenseResult> DispenseAsync(
            IComSystem com,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            if (com == null)
            {
                return ComHopperDispenseResult.Failure("COM-система недоступна.");
            }

            if (!com.IsRunning)
            {
                return ComHopperDispenseResult.Failure("COM-система не запущена.");
            }

            if (!com.SendToMain(StartMotorCommand))
            {
                return ComHopperDispenseResult.Failure("Не удалось отправить команду запуска хоппера.");
            }

            var sensorTriggered = new UniTaskCompletionSource();
            UnityAction onTriggered = () => sensorTriggered.TrySetResult();
            com.HopperSensorTriggered.AddListener(onTriggered);

            try
            {
                var winner = await UniTask.WhenAny(
                    sensorTriggered.Task,
                    UniTask.Delay(timeout, DelayType.Realtime, PlayerLoopTiming.Update, cancellationToken));

                return winner == 0
                    ? ComHopperDispenseResult.Success("Капсула выдана успешно.")
                    : ComHopperDispenseResult.Failure($"Датчик хоппера не сработал за {timeout.TotalSeconds:0.#} сек.");
            }
            finally
            {
                com.HopperSensorTriggered.RemoveListener(onTriggered);
                com.SendToMain(StopMotorCommand);
            }
        }
    }

    public readonly struct ComHopperDispenseResult
    {
        private ComHopperDispenseResult(bool isSuccess, string message)
        {
            IsSuccess = isSuccess;
            Message = message ?? string.Empty;
        }

        public bool IsSuccess { get; }
        public string Message { get; }

        public static ComHopperDispenseResult Success(string message)
        {
            return new ComHopperDispenseResult(true, message);
        }

        public static ComHopperDispenseResult Failure(string message)
        {
            return new ComHopperDispenseResult(false, message);
        }
    }
}
