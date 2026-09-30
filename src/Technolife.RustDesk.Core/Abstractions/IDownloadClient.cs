using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Abstractions;

public interface IDownloadClient
{
    Task<OperationResult> DownloadAsync(
        DownloadRequest request,
        CancellationToken cancellationToken = default);
}
