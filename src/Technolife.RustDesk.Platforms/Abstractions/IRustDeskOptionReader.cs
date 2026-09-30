using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Platforms.Abstractions;

public interface IRustDeskOptionReader
{
    Task<OperationResult<string>> ReadAsync(
        RustDeskInstallation installation,
        string optionName,
        CancellationToken cancellationToken = default);
}
