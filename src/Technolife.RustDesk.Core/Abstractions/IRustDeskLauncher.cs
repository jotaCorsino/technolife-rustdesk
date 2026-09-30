using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Abstractions;

public interface IRustDeskLauncher
{
    Task<OperationResult> StartAsync(
        RustDeskInstallation installation,
        CancellationToken cancellationToken = default);

    Task<OperationResult> RestartAsync(
        RustDeskInstallation installation,
        CancellationToken cancellationToken = default);
}
