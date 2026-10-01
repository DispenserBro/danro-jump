using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DanroJump.Bootstrap
{
    public sealed class StartupProtectionService
    {
        private readonly StartupProtectionOptions options;
        private readonly IStartupLicenseChecker licenseChecker;
        private readonly IStartupProtectionRuntime runtime;

        public StartupProtectionService(
            StartupProtectionOptions options,
            IStartupLicenseChecker licenseChecker,
            IStartupProtectionRuntime runtime)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.licenseChecker = licenseChecker ?? throw new ArgumentNullException(nameof(licenseChecker));
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public StartupProtectionState State { get; private set; } = StartupProtectionState.NotStarted;

        public async UniTask<StartupProtectionState> RunAsync(CancellationToken cancellationToken)
        {
            if (State != StartupProtectionState.NotStarted)
            {
                return State;
            }

            if (ShouldSkipLicenseCheck())
            {
                State = StartupProtectionState.Skipped;
                ApplyPresentationIfEnabled();
                return State;
            }

            State = StartupProtectionState.Checking;

            StartupLicenseResult result;
            try
            {
                result = await licenseChecker.CheckAsync(options.LauncherName, options.CheckTimeout, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                runtime.LogException(exception);
                result = StartupLicenseResult.Failed(exception.Message);
            }

            await UniTask.SwitchToMainThread(cancellationToken);

            if (result.IsValid)
            {
                State = StartupProtectionState.Licensed;
                ApplyPresentationIfEnabled();
                return State;
            }

            State = StartupProtectionState.Failed;
            runtime.LogError(string.IsNullOrWhiteSpace(result.Message)
                ? "[StartupProtection] License check failed."
                : $"[StartupProtection] License check failed: {result.Message}");

            if (options.QuitDelay > TimeSpan.Zero)
            {
                await UniTask.Delay(options.QuitDelay, DelayType.DeltaTime, PlayerLoopTiming.Update, cancellationToken);
            }

            runtime.QuitApplication();
            return State;
        }

        private bool ShouldSkipLicenseCheck()
        {
            return options.SkipInEditor && runtime.IsEditor
                || options.SkipInDevelopmentBuild && runtime.IsDevelopmentBuild;
        }

        private void ApplyPresentationIfEnabled()
        {
            if (!options.ApplyLegacyStartupPresentation || runtime.IsEditor)
            {
                return;
            }

            runtime.ApplyLegacyStartupPresentation(options);
        }
    }
}
