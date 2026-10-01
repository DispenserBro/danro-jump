using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DanroJump.Bootstrap
{
    public interface IStartupLicenseChecker
    {
        UniTask<StartupLicenseResult> CheckAsync(string launcherName, TimeSpan timeout, CancellationToken cancellationToken);
    }
}
