using System.ComponentModel;
using System.Security;
using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Abstractions;

namespace Technolife.RustDesk.Platforms.Windows;

public sealed class WindowsRustDeskServiceManager : IRustDeskServiceManager
{
    public const string ServiceName = "RustDesk";

    private static readonly TimeSpan ServiceCommandTimeout = TimeSpan.FromMinutes(1);

    private readonly IProcessRunner _processRunner;
    private readonly IWindowsServiceController _serviceController;
    private readonly IAppLogger _logger;
    private readonly int _pollingAttempts;
    private readonly TimeSpan _pollingDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public WindowsRustDeskServiceManager(
        IProcessRunner processRunner,
        IWindowsServiceController serviceController,
        IAppLogger logger,
        int pollingAttempts = 10,
        TimeSpan? pollingDelay = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(serviceController);
        ArgumentNullException.ThrowIfNull(logger);

        if (pollingAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pollingAttempts));
        }

        if (pollingDelay.HasValue && pollingDelay.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollingDelay));
        }

        _processRunner = processRunner;
        _serviceController = serviceController;
        _logger = logger;
        _pollingAttempts = pollingAttempts;
        _pollingDelay = pollingDelay ?? TimeSpan.FromSeconds(1);
        _delay = delay ?? Task.Delay;
    }

    public Task<OperationResult<RustDeskServiceStatus>> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var status = _serviceController.GetStatus(ServiceName);
            return Task.FromResult(
                OperationResult<RustDeskServiceStatus>.Succeeded(
                    status,
                    "The RustDesk service status was read successfully."));
        }
        catch (Exception exception) when (IsServiceException(exception))
        {
            var errorCode = IsPermissionException(exception)
                ? ErrorCode.PermissionDenied
                : ErrorCode.InstallationFailed;

            return Task.FromResult(
                OperationResult<RustDeskServiceStatus>.Failed(
                    errorCode,
                    "The RustDesk service status could not be read.",
                    $"Service status query failed with {exception.GetType().Name}."));
        }
    }

    public async Task<OperationResult> EnsureInstalledAsync(
        RustDeskInstallation installation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);

        if (!installation.Found || string.IsNullOrWhiteSpace(installation.ExecutablePath))
        {
            return OperationResult.Failed(
                ErrorCode.RustDeskNotFound,
                "RustDesk must be installed before its service can be installed.");
        }

        if (installation.Platform.Kind is not PlatformKind.Windows)
        {
            return OperationResult.Failed(
                ErrorCode.UnsupportedPlatform,
                "The Windows service manager requires a Windows RustDesk installation.");
        }

        var currentStatus = await GetStatusAsync(cancellationToken).ConfigureAwait(false);

        if (!currentStatus.Success)
        {
            return ToFailure(currentStatus);
        }

        if (currentStatus.Value is not RustDeskServiceStatus.NotInstalled)
        {
            _logger.Info($"RustDesk service '{ServiceName}' is already installed.");
            return OperationResult.Succeeded("The RustDesk service is already installed.");
        }

        _logger.Info($"RustDesk service '{ServiceName}' installation started.");
        var request = new ProcessRequest(
            installation.ExecutablePath,
            ["--install-service"],
            Path.GetDirectoryName(installation.ExecutablePath),
            ServiceCommandTimeout,
            requiresElevation: true);
        var processResult = await _processRunner
            .RunAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (!processResult.Success)
        {
            var errorCode = processResult.ErrorCode is ErrorCode.ElevationFailed
                ? ErrorCode.ElevationFailed
                : ErrorCode.InstallationFailed;

            return OperationResult.Failed(
                errorCode,
                errorCode is ErrorCode.ElevationFailed
                    ? "RustDesk service installation elevation was declined or failed."
                    : "The RustDesk service installation process failed.",
                $"Service installation process failed with error code " +
                $"{processResult.ErrorCode}.");
        }

        if (processResult.Value is null)
        {
            return OperationResult.Failed(
                ErrorCode.InstallationFailed,
                "The RustDesk service installer returned no process result.");
        }

        _logger.Info(
            $"RustDesk service installer completed with exit code " +
            $"{processResult.Value.ExitCode}.");

        if (processResult.Value.ExitCode is not 0)
        {
            return OperationResult.Failed(
                ErrorCode.InstallationFailed,
                "RustDesk returned an error while installing its service.",
                $"Service installation process exited with code " +
                $"{processResult.Value.ExitCode}.");
        }

        for (var attempt = 1; attempt <= _pollingAttempts; attempt++)
        {
            var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);

            if (!status.Success)
            {
                return ToFailure(status);
            }

            if (status.Value is not RustDeskServiceStatus.NotInstalled)
            {
                _logger.Info($"RustDesk service '{ServiceName}' was detected after installation.");
                return OperationResult.Succeeded(
                    "The RustDesk service was installed successfully.");
            }

            if (attempt < _pollingAttempts)
            {
                await _delay(_pollingDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return OperationResult.Failed(
            ErrorCode.InstallationFailed,
            "The RustDesk service was not found after installation.",
            $"Service '{ServiceName}' remained absent after {_pollingAttempts} checks.");
    }

    public async Task<OperationResult> EnsureRunningAsync(
        CancellationToken cancellationToken = default)
    {
        var startRequested = false;
        var continueRequested = false;

        for (var attempt = 1; attempt <= _pollingAttempts; attempt++)
        {
            var statusResult = await GetStatusAsync(cancellationToken).ConfigureAwait(false);

            if (!statusResult.Success)
            {
                return ToFailure(statusResult);
            }

            switch (statusResult.Value)
            {
                case RustDeskServiceStatus.Running:
                    return OperationResult.Succeeded("The RustDesk service is running.");

                case RustDeskServiceStatus.NotInstalled:
                    return OperationResult.Failed(
                        ErrorCode.InstallationFailed,
                        "The RustDesk service is not installed.");

                case RustDeskServiceStatus.Stopped when !startRequested:
                    var startResult = InvokeServiceAction(
                        () => _serviceController.Start(ServiceName),
                        "start");

                    if (!startResult.Success)
                    {
                        return startResult;
                    }

                    startRequested = true;
                    _logger.Info($"Start requested for RustDesk service '{ServiceName}'.");
                    break;

                case RustDeskServiceStatus.Paused when !continueRequested:
                    var continueResult = InvokeServiceAction(
                        () => _serviceController.Continue(ServiceName),
                        "continue");

                    if (!continueResult.Success)
                    {
                        return continueResult;
                    }

                    continueRequested = true;
                    _logger.Info($"Continue requested for RustDesk service '{ServiceName}'.");
                    break;

                case RustDeskServiceStatus.Stopped:
                case RustDeskServiceStatus.StartPending:
                case RustDeskServiceStatus.StopPending:
                case RustDeskServiceStatus.ContinuePending:
                case RustDeskServiceStatus.PausePending:
                case RustDeskServiceStatus.Paused:
                    break;

                default:
                    return OperationResult.Failed(
                        ErrorCode.InstallationFailed,
                        "The RustDesk service reported an unsupported state.",
                        $"Service '{ServiceName}' reported status {statusResult.Value}.");
            }

            if (attempt < _pollingAttempts)
            {
                await _delay(_pollingDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return OperationResult.Failed(
            ErrorCode.InstallationFailed,
            "The RustDesk service did not reach the running state.",
            $"Service '{ServiceName}' was not Running after {_pollingAttempts} checks.");
    }

    private OperationResult InvokeServiceAction(Action action, string actionName)
    {
        try
        {
            action();
            return OperationResult.Succeeded(
                $"The RustDesk service {actionName} action was requested.");
        }
        catch (Exception exception) when (IsServiceException(exception))
        {
            var errorCode = IsPermissionException(exception)
                ? ErrorCode.PermissionDenied
                : ErrorCode.InstallationFailed;

            return OperationResult.Failed(
                errorCode,
                "The RustDesk service could not be activated.",
                $"Service {actionName} failed with {exception.GetType().Name}.");
        }
    }

    private static OperationResult ToFailure(
        OperationResult<RustDeskServiceStatus> result) =>
        OperationResult.Failed(
            result.ErrorCode is ErrorCode.None
                ? ErrorCode.InstallationFailed
                : result.ErrorCode,
            result.Message,
            result.TechnicalDetails);

    private static bool IsPermissionException(Exception exception) =>
        exception is UnauthorizedAccessException or SecurityException;

    private static bool IsServiceException(Exception exception) =>
        exception is Win32Exception
            or InvalidOperationException
            or PlatformNotSupportedException
            or NotSupportedException
            or UnauthorizedAccessException
            or SecurityException;
}
