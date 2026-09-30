using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Abstractions;

public interface IRustDeskServiceManager
{
    Task<OperationResult<RustDeskServiceStatus>> GetStatusAsync(
        CancellationToken cancellationToken = default);

    Task<OperationResult> EnsureInstalledAsync(
        RustDeskInstallation installation,
        CancellationToken cancellationToken = default);

    Task<OperationResult> EnsureRunningAsync(
        CancellationToken cancellationToken = default);
}
