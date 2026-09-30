using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Processes;

namespace Technolife.RustDesk.Platforms.Windows;

public sealed class WindowsRustDeskConfigurator : IRustDeskConfigurator
{
    private static readonly TimeSpan ConfigurationTimeout = TimeSpan.FromSeconds(30);

    private readonly IProcessRunner _processRunner;

    public WindowsRustDeskConfigurator()
        : this(new SystemProcessRunner())
    {
    }

    public WindowsRustDeskConfigurator(IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        _processRunner = processRunner;
    }

    public async Task<OperationResult> ConfigureAsync(
        RustDeskInstallation installation,
        RustDeskConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!installation.Found || string.IsNullOrWhiteSpace(installation.ExecutablePath))
        {
            return OperationResult.Failed(
                ErrorCode.RustDeskNotFound,
                "RustDesk must be installed before its configuration can be applied.");
        }

        if (installation.Platform.Kind is not PlatformKind.Windows)
        {
            return OperationResult.Failed(
                ErrorCode.UnsupportedPlatform,
                "The Windows configurator requires a Windows RustDesk installation.");
        }

        if (string.IsNullOrWhiteSpace(configuration.ExportedConfiguration))
        {
            return OperationResult.Failed(
                ErrorCode.ConfigurationFailed,
                "The exported RustDesk configuration is required.");
        }

        var request = new ProcessRequest(
            installation.ExecutablePath,
            ["--config", configuration.ExportedConfiguration],
            Path.GetDirectoryName(installation.ExecutablePath),
            ConfigurationTimeout,
            requiresElevation: true);

        var processExecution = await _processRunner
            .RunAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!processExecution.Success)
        {
            var errorCode = processExecution.ErrorCode switch
            {
                ErrorCode.ElevationFailed => ErrorCode.ElevationFailed,
                ErrorCode.PermissionDenied => ErrorCode.PermissionDenied,
                _ => ErrorCode.ProcessFailed
            };

            return OperationResult.Failed(
                errorCode,
                "RustDesk could not apply the configuration.",
                $"Process execution failed with error code {processExecution.ErrorCode}.");
        }

        if (processExecution.Value is null)
        {
            return OperationResult.Failed(
                ErrorCode.ProcessFailed,
                "RustDesk could not apply the configuration.",
                "The process runner returned no result.");
        }

        if (processExecution.Value.ExitCode is not 0)
        {
            return OperationResult.Failed(
                ErrorCode.ProcessFailed,
                "RustDesk returned an error while applying the configuration.",
                $"RustDesk process exited with code {processExecution.Value.ExitCode}.");
        }

        return OperationResult.Succeeded(
            "RustDesk configuration was applied successfully.");
    }
}
