using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Abstractions;

public interface IRustDeskValidator
{
    Task<OperationResult> ValidateAsync(
        RustDeskInstallation installation,
        RustDeskConfiguration configuration,
        CancellationToken cancellationToken = default);
}
