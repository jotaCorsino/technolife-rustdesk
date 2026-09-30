using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Abstractions;

public interface IRustDeskInstaller
{
    Task<OperationResult<RustDeskInstallation>> InstallAsync(
        CancellationToken cancellationToken = default);
}
