using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Platforms.Abstractions;

public interface IServiceInstallProcess : IAsyncDisposable
{
    bool HasExited { get; }

    int? ExitCode { get; }

    Task<OperationResult> TerminateAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

public interface IServiceInstallProcessLauncher
{
    OperationResult<IServiceInstallProcess> Start(ProcessRequest request);
}
