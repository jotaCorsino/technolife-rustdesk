namespace Technolife.RustDesk.Core.Models;

public sealed record RustDeskSetupWorkflowResult
{
    public RustDeskSetupWorkflowResult(
        RustDeskConfigurationWorkflowResult configurationResult,
        bool installationPerformed)
    {
        ArgumentNullException.ThrowIfNull(configurationResult);

        ConfigurationResult = configurationResult;
        InstallationPerformed = installationPerformed;
    }

    public RustDeskConfigurationWorkflowResult ConfigurationResult { get; }

    public bool InstallationPerformed { get; }
}
