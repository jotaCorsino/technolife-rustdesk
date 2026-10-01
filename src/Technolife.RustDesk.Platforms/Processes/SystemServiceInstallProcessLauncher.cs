using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Abstractions;

namespace Technolife.RustDesk.Platforms.Processes;

public sealed class SystemServiceInstallProcessLauncher : IServiceInstallProcessLauncher
{
    private static readonly TimeSpan DisposalTimeout = TimeSpan.FromSeconds(5);

    private readonly IProcessElevationContext _elevationContext;

    public SystemServiceInstallProcessLauncher()
        : this(new WindowsProcessElevationContext())
    {
    }

    public SystemServiceInstallProcessLauncher(IProcessElevationContext elevationContext)
    {
        ArgumentNullException.ThrowIfNull(elevationContext);
        _elevationContext = elevationContext;
    }

    public OperationResult<IServiceInstallProcess> Start(ProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var useShellElevation =
            request.RequiresElevation && !_elevationContext.IsCurrentProcessElevated;
        var process = new Process
        {
            StartInfo = CreateStartInfo(request, useShellElevation)
        };

        try
        {
            if (!process.Start())
            {
                process.Dispose();
                return OperationResult<IServiceInstallProcess>.Failed(
                    useShellElevation
                        ? ErrorCode.ElevationFailed
                        : ErrorCode.ProcessFailed,
                    "The RustDesk service installation process could not be started.");
            }

            return OperationResult<IServiceInstallProcess>.Succeeded(
                new SystemServiceInstallProcess(process),
                "The RustDesk service installation process was started.");
        }
        catch (Exception exception) when (IsStartException(exception))
        {
            process.Dispose();
            var errorCode = useShellElevation
                ? ErrorCode.ElevationFailed
                : exception is UnauthorizedAccessException or SecurityException
                    ? ErrorCode.PermissionDenied
                    : ErrorCode.ProcessFailed;

            return OperationResult<IServiceInstallProcess>.Failed(
                errorCode,
                "The RustDesk service installation process could not be started.",
                $"Service installation process start failed with " +
                $"{exception.GetType().Name}.");
        }
    }

    private static ProcessStartInfo CreateStartInfo(
        ProcessRequest request,
        bool useShellElevation)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.Executable,
            UseShellExecute = useShellElevation,
            CreateNoWindow = !useShellElevation
        };

        if (useShellElevation)
        {
            startInfo.Verb = "runas";
        }

        if (request.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = request.WorkingDirectory;
        }

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static bool IsStartException(Exception exception) =>
        exception is Win32Exception
            or InvalidOperationException
            or NotSupportedException
            or UnauthorizedAccessException
            or SecurityException;

    private sealed class SystemServiceInstallProcess(Process process)
        : IServiceInstallProcess
    {
        private bool _disposed;

        public bool HasExited => !_disposed && process.HasExited;

        public int? ExitCode => HasExited ? process.ExitCode : null;

        public async Task<OperationResult> TerminateAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            if (timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout));
            }

            if (_disposed || process.HasExited)
            {
                return OperationResult.Succeeded(
                    "The RustDesk service installation process has already exited.");
            }

            try
            {
                // Kill only the exact helper PID started by this launcher. The RustDesk
                // Windows service is owned by SCM and is never targeted here.
                process.Kill(entireProcessTree: false);

                using var timeoutCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCancellation.CancelAfter(timeout);
                await process
                    .WaitForExitAsync(timeoutCancellation.Token)
                    .ConfigureAwait(false);

                return OperationResult.Succeeded(
                    "The RustDesk service installation helper was terminated.");
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return OperationResult.Failed(
                    ErrorCode.ProcessFailed,
                    "The RustDesk service installation helper did not terminate in time.",
                    $"Helper termination timed out after {timeout:c}.");
            }
            catch (Exception exception) when (IsTerminationException(exception))
            {
                return OperationResult.Failed(
                    ErrorCode.ProcessFailed,
                    "The RustDesk service installation helper could not be terminated.",
                    $"Helper termination failed with {exception.GetType().Name}.");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            if (!process.HasExited)
            {
                await TerminateAsync(DisposalTimeout).ConfigureAwait(false);
            }

            _disposed = true;
            process.Dispose();
        }

        private static bool IsTerminationException(Exception exception) =>
            exception is Win32Exception
                or InvalidOperationException
                or NotSupportedException;
    }
}
