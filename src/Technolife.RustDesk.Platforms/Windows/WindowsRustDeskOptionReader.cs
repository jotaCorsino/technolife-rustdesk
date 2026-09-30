using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Abstractions;

namespace Technolife.RustDesk.Platforms.Windows;

public sealed class WindowsRustDeskOptionReader : IRustDeskOptionReader
{
    private static readonly TimeSpan OptionReadTimeout = TimeSpan.FromSeconds(30);

    private readonly IProcessRunner _processRunner;

    public WindowsRustDeskOptionReader(IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        _processRunner = processRunner;
    }

    public async Task<OperationResult<string>> ReadAsync(
        RustDeskInstallation installation,
        string optionName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentException.ThrowIfNullOrWhiteSpace(optionName);

        if (!installation.Found || string.IsNullOrWhiteSpace(installation.ExecutablePath))
        {
            return OperationResult<string>.Failed(
                ErrorCode.RustDeskNotFound,
                "RustDesk must be installed before an option can be read.");
        }

        if (installation.Platform.Kind is not PlatformKind.Windows)
        {
            return OperationResult<string>.Failed(
                ErrorCode.UnsupportedPlatform,
                "The Windows option reader requires a Windows RustDesk installation.");
        }

        var request = new ProcessRequest(
            installation.ExecutablePath,
            ["--option", optionName],
            Path.GetDirectoryName(installation.ExecutablePath),
            OptionReadTimeout,
            requiresElevation: true);
        var processExecution = await _processRunner
            .RunAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!processExecution.Success)
        {
            return OperationResult<string>.Failed(
                processExecution.ErrorCode is ErrorCode.None
                    ? ErrorCode.ProcessFailed
                    : processExecution.ErrorCode,
                "RustDesk could not read the requested option.",
                $"Option process failed for '{optionName}' with error code " +
                $"{processExecution.ErrorCode}.");
        }

        if (processExecution.Value is null)
        {
            return OperationResult<string>.Failed(
                ErrorCode.ProcessFailed,
                "RustDesk returned no result while reading an option.",
                $"No process result was returned for option '{optionName}'.");
        }

        if (processExecution.Value.ExitCode is not 0)
        {
            return OperationResult<string>.Failed(
                ErrorCode.ProcessFailed,
                "RustDesk returned an error while reading an option.",
                $"Option '{optionName}' exited with code " +
                $"{processExecution.Value.ExitCode}.");
        }

        return OperationResult<string>.Succeeded(
            processExecution.Value.StandardOutput.Trim(),
            "The RustDesk option was read successfully.");
    }
}
