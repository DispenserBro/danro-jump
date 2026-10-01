using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DanroJump.Hardware.Com;
using NUnit.Framework;

namespace DanroJump.Tests.EditMode
{
    public sealed class ComHopperDispenserTests
    {
        [Test]
        public async Task DispenseAsync_StartsAndStopsMotorOnTimeout()
        {
            var com = new StubComSystem { IsRunningValue = true };

            var result = await ComHopperDispenser
                .DispenseAsync(com, TimeSpan.Zero, CancellationToken.None)
                .AsTask();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Does.Contain("Датчик хоппера"));
            Assert.That(com.SentCommands, Is.EqualTo(new[]
            {
                ComHopperDispenser.StartMotorCommand,
                ComHopperDispenser.StopMotorCommand
            }));
        }

        [Test]
        public async Task DispenseAsync_NotRunningComReturnsFailureWithoutCommands()
        {
            var com = new StubComSystem { IsRunningValue = false };

            var result = await ComHopperDispenser
                .DispenseAsync(com, TimeSpan.Zero, CancellationToken.None)
                .AsTask();

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Does.Contain("не запущена"));
            Assert.That(com.SentCommands, Is.Empty);
        }

        private sealed class StubComSystem : IComSystem
        {
            public readonly List<string> SentCommands = new();

            public bool IsRunningValue { get; set; }
            public bool IsRunning => IsRunningValue;
            public string MainDeviceId => "main";
            public string PrizeStandDeviceId => "stand";
            public IReadOnlyCollection<string> ConnectedDeviceIds => new[] { "main" };
            public UnityEngine.Events.UnityEvent<string> DeviceAuthorized { get; } = new();
            public UnityEngine.Events.UnityEvent<string> DeviceDisconnected { get; } = new();
            public UnityEngine.Events.UnityEvent<int> PrizeBoxOpened { get; } = new();
            public int Credits => 0;
            public UnityEngine.Events.UnityEvent<int> CreditAdded { get; } = new();
            public UnityEngine.Events.UnityEvent<int> CreditRemoved { get; } = new();
            public UnityEngine.Events.UnityEvent<int> CreditsChanged { get; } = new();
            public string LastMainInputBits => string.Empty;
            public string LastMainAdcValues => string.Empty;
            public UnityEngine.Events.UnityEvent<string> MainInputReceived { get; } = new();
            public UnityEngine.Events.UnityEvent<string> MainAdcReceived { get; } = new();
            public UnityEngine.Events.UnityEvent HopperSensorTriggered { get; } = new();

            public void StartSystem() { }
            public void StopSystem(bool sendShutdownCommands) { }
            public bool SendToMain(string command)
            {
                SentCommands.Add(command);
                return true;
            }
            public bool SendToDevice(string deviceId, string command) => true;
            public void SendToAll(string command) { }
            public string GetDeviceRole(string deviceId) => string.Empty;
            public string GetDeviceMac(string deviceId) => string.Empty;
            public UniTask<IReadOnlyDictionary<int, PrizeBoxStatus>> GetPrizeBoxStatusesAsync(CancellationToken cancellationToken = default)
            {
                return UniTask.FromResult<IReadOnlyDictionary<int, PrizeBoxStatus>>(new Dictionary<int, PrizeBoxStatus>());
            }
            public UniTask<int[]> GetReadyPrizeBoxesAsync(CancellationToken cancellationToken = default)
            {
                return UniTask.FromResult(Array.Empty<int>());
            }
            public UniTask<PrizeOpenResult> OpenPrizeBoxAsync(int boxNumber, CancellationToken cancellationToken = default)
            {
                return UniTask.FromResult(PrizeOpenResult.Fail(boxNumber, "stub"));
            }
            public UniTask<PrizeOpenResult> ForceOpenPrizeBoxAsync(int boxNumber, CancellationToken cancellationToken = default)
            {
                return UniTask.FromResult(PrizeOpenResult.Fail(boxNumber, "stub"));
            }
            public bool SetPrizeStandIdleLighting() => true;
            public bool HighlightPrizeBox(int boxNumber) => true;
            public void AddCredit(int amount = 1) { }
            public bool TrySpendCredits(int amount = 1) => false;
            public void SetCredits(int value) { }
            public string GetLastInputBits(string deviceId) => string.Empty;
            public string GetLastAdcValues(string deviceId) => string.Empty;
            public void PlayBoardLight(LightMode mode, float cycles = -1f) { }
            public void PlayBoardLightSequence(IReadOnlyList<LightStep> steps, bool loop) { }
            public void PlayDefaultBoardLight() { }
            public void StopBoardLightSequence() { }
            public void StopAllBoardEffects() { }
            public void BlinkHat() { }
            public void BlinkHat(float intervalSeconds) { }
            public void StopHatBlink(bool leaveHatEnabled = true) { }
            public void PauseLighting() { }
            public void ResumeLighting() { }
        }
    }
}
