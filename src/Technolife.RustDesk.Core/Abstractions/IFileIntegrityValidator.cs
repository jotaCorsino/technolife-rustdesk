using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Abstractions;

public interface IFileIntegrityValidator
{
    Task<OperationResult> ValidateSha256Async(
        string filePath,
        string expectedSha256,
        CancellationToken cancellationToken = default);
}
