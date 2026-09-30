namespace Technolife.RustDesk.Core.Models;

public sealed record RustDeskConfigurationWorkflowResult
{
    public RustDeskConfigurationWorkflowResult(
        RustDeskInstallation installation,
        RustDeskValidation validation,
        string logPath)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);

        Installation = installation;
        Validation = validation;
        LogPath = logPath;
    }

    public RustDeskInstallation Installation { get; }

    public RustDeskValidation Validation { get; }

    public string LogPath { get; }
}
