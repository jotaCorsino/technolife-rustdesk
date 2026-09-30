using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Abstractions;

public interface IRustDeskInstaller
{
    Task<OperationResult> InstallAsync(
        CancellationToken cancellationToken = default,
        IProgress<SetupProgress>? progress = null);
}
