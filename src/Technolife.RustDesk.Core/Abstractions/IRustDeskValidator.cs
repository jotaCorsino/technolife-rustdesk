using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Abstractions;

public interface IRustDeskValidator
{
    Task<OperationResult<RustDeskValidation>> ValidateAsync(
        RustDeskInstallation installation,
        RustDeskConfiguration configuration,
        CancellationToken cancellationToken = default);
}
