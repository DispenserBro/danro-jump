using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace DanroJump.Hardware.Com
{
    /// <summary>
    /// Отдельная система аппаратной подсветки: плата, шляпа и призовая витрина.
    /// </summary>
    public sealed class ComLightingSystem : IComLightingController, IDisposable
    {
        private readonly Func<string, bool> sendToMain;
        private readonly Func<string, bool> sendToPrizeStand;
        private readonly ComLightingOptions options;

        private CancellationTokenSource hatBlinkCancellation;
        private CancellationTokenSource lightSequenceCancellation;
        private LightSequenceState pausedLightSequence;
        private bool hatIsOn;
        private bool hatBlinkWasPaused;

        public ComLightingSystem(
            ComLightingOptions options,
            Func<string, bool> sendToMain,
            Func<string, bool> sendToPrizeStand)
        {
            this.options = options;
            this.sendToMain = sendToMain ?? (_ => false);
            this.sendToPrizeStand = sendToPrizeStand ?? (_ => false);
        }

        public bool SetPrizeStandIdleLighting()
        {
            return sendToPrizeStand(options.PrizeStandIdleLightingCommand);
        }

        public bool HighlightPrizeBox(int boxNumber)
        {
            return boxNumber > 0 && sendToPrizeStand($"{options.PrizeStandHighlightBoxCommandPrefix}{boxNumber:D2}");
        }

        public void PlayBoardLight(LightMode mode, float cycles = -1f)
        {
            StopBoardLightSequence();
            float resolvedCycles = cycles > 0f ? cycles : options.DefaultLightCycles;
            StartBoardLightSequenceTask(new[] { new LightStep(mode, resolvedCycles) }, false);
        }

        public void PlayBoardLightSequence(IReadOnlyList<LightStep> steps, bool loop)
        {
            if (steps == null || steps.Count == 0)
            {
                return;
            }

            StopBoardLightSequence();
            StartBoardLightSequenceTask(steps, loop);
        }

        public void PlayDefaultBoardLight()
        {
            PlayBoardLight(options.DefaultLightMode, options.DefaultLightCycles);
        }

        public void StopBoardLightSequence()
        {
            CancelAndDispose(ref lightSequenceCancellation);
            pausedLightSequence = default;
        }

        public void StopAllBoardEffects()
        {
            StopBoardLightSequence();
            sendToMain(options.BoardEffectsOffCommand);
        }

        public void BlinkHat()
        {
            BlinkHat(options.HatBlinkIntervalSeconds);
        }

        public void BlinkHat(float intervalSeconds)
        {
            StopHatBlink(false);
            hatIsOn = true;
            hatBlinkCancellation = new CancellationTokenSource();
            HatBlinkAsync(Mathf.Max(0.05f, intervalSeconds), hatBlinkCancellation).Forget(Debug.LogException);
        }

        public void StopHatBlink(bool leaveHatEnabled = true)
        {
            CancelAndDispose(ref hatBlinkCancellation);
            hatIsOn = leaveHatEnabled;
            sendToMain(leaveHatEnabled ? options.HatOnCommand : options.HatOffCommand);
        }

        public void PauseLighting()
        {
            if (hatBlinkCancellation != null)
            {
                hatBlinkWasPaused = true;
                CancelAndDispose(ref hatBlinkCancellation);
            }
            else
            {
                hatBlinkWasPaused = false;
            }

            if (lightSequenceCancellation != null)
            {
                CancelAndDispose(ref lightSequenceCancellation);
                pausedLightSequence = new LightSequenceState(true);
            }
        }

        public void ResumeLighting()
        {
            if (hatBlinkWasPaused)
            {
                hatBlinkWasPaused = false;
                BlinkHat(options.HatBlinkIntervalSeconds);
            }
            else if (hatIsOn)
            {
                sendToMain(options.HatOnCommand);
            }

            if (pausedLightSequence.HasPausedSequence)
            {
                PlayDefaultBoardLight();
            }
        }

        public void Dispose()
        {
            StopBoardLightSequence();
            CancelAndDispose(ref hatBlinkCancellation);
        }

        private async UniTask HatBlinkAsync(float intervalSeconds, CancellationTokenSource cancellationSource)
        {
            var token = cancellationSource.Token;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    sendToMain(UnityEngine.Random.Range(0f, 1f) < 0.5f ? options.HatOnCommand : options.HatOffCommand);

                    float wait = options.HatBlinkVarianceSeconds > 0f
                        ? Mathf.Max(
                            0.05f,
                            intervalSeconds + UnityEngine.Random.Range(-options.HatBlinkVarianceSeconds, options.HatBlinkVarianceSeconds))
                        : intervalSeconds;

                    await UniTask.Delay(
                        TimeSpan.FromSeconds(wait),
                        DelayType.UnscaledDeltaTime,
                        PlayerLoopTiming.Update,
                        token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // ignored
            }
            finally
            {
                ClearCancellationIfCurrent(ref hatBlinkCancellation, cancellationSource);
            }
        }

        private void StartBoardLightSequenceTask(IReadOnlyList<LightStep> steps, bool loop)
        {
            lightSequenceCancellation = new CancellationTokenSource();
            BoardLightSequenceAsync(steps, loop, lightSequenceCancellation).Forget(Debug.LogException);
        }

        private async UniTask BoardLightSequenceAsync(
            IReadOnlyList<LightStep> steps,
            bool loop,
            CancellationTokenSource cancellationSource)
        {
            var token = cancellationSource.Token;
            try
            {
                do
                {
                    foreach (LightStep step in steps)
                    {
                        token.ThrowIfCancellationRequested();
                        sendToMain(BuildBoardLightCommand(step.Mode, step.Cycles));

                        float waitSeconds = LightTimings.GetCycleSeconds(step.Mode) * step.Cycles;
                        if (step.Mode == LightMode.Off || waitSeconds <= 0f)
                        {
                            continue;
                        }

                        float elapsed = 0f;
                        while (elapsed < waitSeconds)
                        {
                            float chunk = Mathf.Min(2f, waitSeconds - elapsed);
                            await UniTask.Delay(
                                TimeSpan.FromSeconds(chunk),
                                DelayType.UnscaledDeltaTime,
                                PlayerLoopTiming.Update,
                                token);
                            elapsed += chunk;

                            if (elapsed < waitSeconds)
                            {
                                int remainSeconds = Mathf.CeilToInt(waitSeconds - elapsed);
                                sendToMain(BuildBoardLightCommand(step.Mode, remainSeconds));
                            }
                        }
                    }
                }
                while (loop && !token.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // ignored
            }
            finally
            {
                ClearCancellationIfCurrent(ref lightSequenceCancellation, cancellationSource);
            }
        }

        private string BuildBoardLightCommand(LightMode mode, float cycles)
        {
            if (mode == LightMode.Off)
            {
                return options.BoardEffectsOffCommand;
            }

            int seconds = LightTimings.CyclesToCommandSeconds(mode, cycles);
            return BuildBoardLightCommand(mode, seconds);
        }

        private string BuildBoardLightCommand(LightMode mode, int durationSeconds)
        {
            if (mode == LightMode.Off)
            {
                return options.BoardEffectsOffCommand;
            }

            return $"{options.BoardEffectsCommandPrefix}{(int)mode:D2}{Mathf.Clamp(durationSeconds, 0, 99):D2}";
        }

        private static void CancelAndDispose(ref CancellationTokenSource cancellationSource)
        {
            if (cancellationSource == null)
            {
                return;
            }

            var source = cancellationSource;
            cancellationSource = null;

            try
            {
                source.Cancel();
            }
            catch
            {
                // ignored
            }

            source.Dispose();
        }

        private static void ClearCancellationIfCurrent(
            ref CancellationTokenSource currentSource,
            CancellationTokenSource completedSource)
        {
            if (currentSource != completedSource)
            {
                return;
            }

            currentSource = null;
            completedSource.Dispose();
        }

        private readonly struct LightSequenceState
        {
            public readonly bool HasPausedSequence;

            public LightSequenceState(bool hasPausedSequence)
            {
                HasPausedSequence = hasPausedSequence;
            }
        }
    }
}
