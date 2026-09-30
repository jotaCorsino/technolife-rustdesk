using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Abstractions;

public interface IRustDeskDetector
{
    Task<OperationResult<RustDeskInstallation>> DetectAsync(
        CancellationToken cancellationToken = default);
}
