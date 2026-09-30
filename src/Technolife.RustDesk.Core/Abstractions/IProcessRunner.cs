using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Abstractions;

public interface IProcessRunner
{
    Task<OperationResult<ProcessResult>> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default);
}
