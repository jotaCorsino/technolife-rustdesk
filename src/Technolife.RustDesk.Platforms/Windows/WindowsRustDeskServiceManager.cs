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

    private static readonly TimeSpan DefaultServiceInstallationTimeout =
        TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultServiceStartTimeout =
        TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HelperTerminationTimeout =
        TimeSpan.FromSeconds(5);

    private readonly IServiceInstallProcessLauncher _serviceInstallProcessLauncher;
    private readonly IWindowsServiceController _serviceController;
    private readonly IAppLogger _logger;
    private readonly int _pollingAttempts;
    private readonly TimeSpan _pollingDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeSpan _serviceInstallationTimeout;
    private readonly TimeSpan _serviceStartTimeout;

    public WindowsRustDeskServiceManager(
        IServiceInstallProcessLauncher serviceInstallProcessLauncher,
        IWindowsServiceController serviceController,
        IAppLogger logger,
        int pollingAttempts = 30,
        TimeSpan? pollingDelay = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        TimeSpan? serviceInstallationTimeout = null,
        TimeSpan? serviceStartTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(serviceInstallProcessLauncher);
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

        if (serviceInstallationTimeout.HasValue &&
            serviceInstallationTimeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(serviceInstallationTimeout));
        }

        if (serviceStartTimeout.HasValue && serviceStartTimeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(serviceStartTimeout));
        }

        _serviceInstallProcessLauncher = serviceInstallProcessLauncher;
        _serviceController = serviceController;
        _logger = logger;
        _pollingAttempts = pollingAttempts;
        _pollingDelay = pollingDelay ?? TimeSpan.FromSeconds(1);
        _delay = delay ?? Task.Delay;
        _serviceInstallationTimeout =
            serviceInstallationTimeout ?? DefaultServiceInstallationTimeout;
        _serviceStartTimeout = serviceStartTimeout ?? DefaultServiceStartTimeout;
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
            _serviceInstallationTimeout,
            requiresElevation: true);
        var processStart = _serviceInstallProcessLauncher.Start(request);

        if (!processStart.Success || processStart.Value is null)
        {
            var errorCode = processStart.ErrorCode switch
            {
                ErrorCode.ElevationFailed => ErrorCode.ElevationFailed,
                ErrorCode.PermissionDenied => ErrorCode.PermissionDenied,
                _ => ErrorCode.InstallationFailed
            };

            return OperationResult.Failed(
                errorCode,
                errorCode is ErrorCode.ElevationFailed
                    ? "RustDesk service installation elevation was declined or failed."
                    : "The RustDesk service installation process failed.",
                $"Service installation process failed with error code " +
                $"{processStart.ErrorCode}.");
        }

        await using var installProcess = processStart.Value;
        using var operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationCancellation.CancelAfter(_serviceInstallationTimeout);
        var processExitLogged = false;

        try
        {
            for (var attempt = 1; attempt <= _pollingAttempts; attempt++)
            {
                var status = await GetStatusAsync(operationCancellation.Token)
                    .ConfigureAwait(false);

                if (!status.Success)
                {
                    return ToFailure(status);
                }

                if (status.Value is not RustDeskServiceStatus.NotInstalled)
                {
                    _logger.Info(
                        $"RustDesk service '{ServiceName}' was detected by SCM after " +
                        "the installation request.");

                    await TerminateHelperIfRunningAsync(
                            installProcess,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return OperationResult.Succeeded(
                        "The RustDesk service was installed successfully.");
                }

                if (installProcess.HasExited && !processExitLogged)
                {
                    processExitLogged = true;
                    var exitCode = installProcess.ExitCode;
                    _logger.Info(
                        $"RustDesk service installation helper exited with code " +
                        $"{exitCode?.ToString() ?? "unknown"} before SCM detection.");

                    if (exitCode is not 0)
                    {
                        return OperationResult.Failed(
                            ErrorCode.InstallationFailed,
                            "RustDesk returned an error while installing its service.",
                            $"Service installation helper exited with code " +
                            $"{exitCode?.ToString() ?? "unknown"}.");
                    }
                }

                if (attempt < _pollingAttempts)
                {
                    await _delay(_pollingDelay, operationCancellation.Token)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return OperationResult.Failed(
                ErrorCode.InstallationFailed,
                "The RustDesk service installation timed out.",
                $"Service '{ServiceName}' did not appear in SCM within " +
                $"{_serviceInstallationTimeout:c}.");
        }

        return OperationResult.Failed(
            ErrorCode.InstallationFailed,
            "The RustDesk service installation timed out.",
            $"Service '{ServiceName}' remained absent after {_pollingAttempts} checks " +
            $"within {_serviceInstallationTimeout:c}.");
    }

    public async Task<OperationResult> EnsureRunningAsync(
        CancellationToken cancellationToken = default)
    {
        var startRequested = false;
        var continueRequested = false;
        using var operationCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operationCancellation.CancelAfter(_serviceStartTimeout);

        try
        {
            for (var attempt = 1; attempt <= _pollingAttempts; attempt++)
            {
                var statusResult = await GetStatusAsync(operationCancellation.Token)
                    .ConfigureAwait(false);

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
                    await _delay(_pollingDelay, operationCancellation.Token)
                        .ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return OperationResult.Failed(
                ErrorCode.InstallationFailed,
                "The RustDesk service did not reach the running state in time.",
                $"Service '{ServiceName}' was not Running within " +
                $"{_serviceStartTimeout:c}.");
        }

        return OperationResult.Failed(
            ErrorCode.InstallationFailed,
            "The RustDesk service did not reach the running state.",
            $"Service '{ServiceName}' was not Running after {_pollingAttempts} checks " +
            $"within {_serviceStartTimeout:c}.");
    }

    private async Task TerminateHelperIfRunningAsync(
        IServiceInstallProcess installProcess,
        CancellationToken cancellationToken)
    {
        if (installProcess.HasExited)
        {
            return;
        }

        _logger.Info(
            "SCM confirmed the RustDesk service; terminating only the auxiliary " +
            "installation process started by the configurator.");
        var terminationResult = await installProcess
            .TerminateAsync(HelperTerminationTimeout, cancellationToken)
            .ConfigureAwait(false);

        if (terminationResult.Success)
        {
            _logger.Info("RustDesk service installation helper terminated.");
        }
        else
        {
            _logger.Warning(
                "RustDesk service installation helper could not be terminated cleanly; " +
                "SCM already confirmed that the service is installed.");
        }
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
