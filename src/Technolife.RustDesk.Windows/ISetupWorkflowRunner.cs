using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Windows;

public interface ISetupWorkflowRunner
{
    Task<OperationResult<RustDeskSetupWorkflowResult>> RunAsync(
        IProgress<SetupProgress> progress,
        CancellationToken cancellationToken = default);
}
