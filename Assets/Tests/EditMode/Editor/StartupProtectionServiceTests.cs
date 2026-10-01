using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DanroJump.Bootstrap;
using NUnit.Framework;

namespace DanroJump.Tests.EditMode
{
    public sealed class StartupProtectionServiceTests
    {
        [Test]
        public void RunAsync_SkipsCheckerInEditor()
        {
            var runtime = new FakeStartupProtectionRuntime
            {
                IsEditorValue = true,
                IsDevelopmentBuildValue = false
            };
            var checker = new FakeStartupLicenseChecker(StartupLicenseResult.Failed("Should not be called."));
            var service = new StartupProtectionService(new StartupProtectionOptions(), checker, runtime);

            StartupProtectionState state = service.RunAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(state, Is.EqualTo(StartupProtectionState.Skipped));
            Assert.That(checker.CallCount, Is.Zero);
            Assert.That(runtime.LegacyPresentationApplyCount, Is.Zero);
            Assert.That(runtime.QuitCount, Is.Zero);
        }

        [Test]
        public void RunAsync_AppliesLegacyPresentationWhenEnabledAndLicenseIsValid()
        {
            var runtime = new FakeStartupProtectionRuntime();
            var checker = new FakeStartupLicenseChecker(StartupLicenseResult.Valid());
            StartupProtectionOptions options = CreateOptions(applyLegacyPresentation: true, quitDelaySeconds: 0f);
            var service = new StartupProtectionService(options, checker, runtime);

            StartupProtectionState state = service.RunAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(state, Is.EqualTo(StartupProtectionState.Licensed));
            Assert.That(checker.CallCount, Is.EqualTo(1));
            Assert.That(runtime.LegacyPresentationApplyCount, Is.EqualTo(1));
            Assert.That(runtime.QuitCount, Is.Zero);
        }

        [Test]
        public void RunAsync_QuitsApplicationWhenLicenseIsInvalid()
        {
            var runtime = new FakeStartupProtectionRuntime();
            var checker = new FakeStartupLicenseChecker(StartupLicenseResult.Failed("Invalid license."));
            StartupProtectionOptions options = CreateOptions(applyLegacyPresentation: false, quitDelaySeconds: 0f);
            var service = new StartupProtectionService(options, checker, runtime);

            StartupProtectionState state = service.RunAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(state, Is.EqualTo(StartupProtectionState.Failed));
            Assert.That(checker.CallCount, Is.EqualTo(1));
            Assert.That(runtime.QuitCount, Is.EqualTo(1));
            Assert.That(runtime.Errors.Count, Is.EqualTo(1));
            Assert.That(runtime.Errors[0], Does.Contain("Invalid license."));
        }

        [Test]
        public void RunAsync_SkipsCheckerInDevelopmentBuild()
        {
            var runtime = new FakeStartupProtectionRuntime
            {
                IsDevelopmentBuildValue = true
            };
            var checker = new FakeStartupLicenseChecker(StartupLicenseResult.Failed("Should not be called."));
            var service = new StartupProtectionService(new StartupProtectionOptions(), checker, runtime);

            StartupProtectionState state = service.RunAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(state, Is.EqualTo(StartupProtectionState.Skipped));
            Assert.That(checker.CallCount, Is.Zero);
            Assert.That(runtime.QuitCount, Is.Zero);
        }

        private static StartupProtectionOptions CreateOptions(bool applyLegacyPresentation, float quitDelaySeconds)
        {
            return new StartupProtectionOptions(
                quitDelaySeconds: quitDelaySeconds,
                applyLegacyStartupPresentation: applyLegacyPresentation);
        }

        private sealed class FakeStartupLicenseChecker : IStartupLicenseChecker
        {
            private readonly StartupLicenseResult result;

            public FakeStartupLicenseChecker(StartupLicenseResult result)
            {
                this.result = result;
            }

            public int CallCount { get; private set; }

            public UniTask<StartupLicenseResult> CheckAsync(
                string launcherName,
                TimeSpan timeout,
                CancellationToken cancellationToken)
            {
                CallCount++;
                return UniTask.FromResult(result);
            }
        }

        private sealed class FakeStartupProtectionRuntime : IStartupProtectionRuntime
        {
            public bool IsEditorValue { get; set; }
            public bool IsDevelopmentBuildValue { get; set; }
            public int LegacyPresentationApplyCount { get; private set; }
            public int QuitCount { get; private set; }
            public List<string> Errors { get; } = new List<string>();

            public bool IsEditor => IsEditorValue;
            public bool IsDevelopmentBuild => IsDevelopmentBuildValue;
            public string BaseDirectory => string.Empty;
            public string StreamingAssetsPath => string.Empty;

            public void ApplyLegacyStartupPresentation(StartupProtectionOptions options)
            {
                LegacyPresentationApplyCount++;
            }

            public void QuitApplication()
            {
                QuitCount++;
            }

            public void Log(string message)
            {
            }

            public void LogWarning(string message)
            {
            }

            public void LogError(string message)
            {
                Errors.Add(message);
            }

            public void LogException(Exception exception)
            {
                Errors.Add(exception.Message);
            }
        }
    }
}
