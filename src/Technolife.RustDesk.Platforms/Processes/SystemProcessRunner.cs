using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Platforms.Processes;

public sealed class SystemProcessRunner : IProcessRunner
{
    private static readonly TimeSpan TerminationWait = TimeSpan.FromSeconds(5);

    public async Task<OperationResult<ProcessResult>> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var process = new Process
        {
            StartInfo = CreateStartInfo(request)
        };

        var processStarted = false;

        try
        {
            processStarted = process.Start();

            if (!processStarted)
            {
                return OperationResult<ProcessResult>.Failed(
                    ErrorCode.ProcessFailed,
                    "The requested executable could not be started.");
            }

            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();

            using var executionCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (request.Timeout is { } timeout)
            {
                executionCancellation.CancelAfter(timeout);
            }

            try
            {
                await process.WaitForExitAsync(executionCancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested && request.Timeout.HasValue)
            {
                await TerminateAsync(process).ConfigureAwait(false);

                return OperationResult<ProcessResult>.Failed(
                    ErrorCode.ProcessFailed,
                    "The process exceeded the allowed execution time.",
                    $"Process timed out after {request.Timeout.Value:c}.");
            }
            catch (OperationCanceledException)
            {
                await TerminateAsync(process).ConfigureAwait(false);
                throw;
            }

            var standardOutput = await standardOutputTask.ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);

            return OperationResult<ProcessResult>.Succeeded(
                new ProcessResult(process.ExitCode, standardOutput, standardError),
                "The process completed.");
        }
        catch (UnauthorizedAccessException exception)
        {
            if (processStarted)
            {
                await TerminateAsync(process).ConfigureAwait(false);
            }

            return CreateStartFailure(ErrorCode.PermissionDenied, exception);
        }
        catch (SecurityException exception)
        {
            if (processStarted)
            {
                await TerminateAsync(process).ConfigureAwait(false);
            }

            return CreateStartFailure(ErrorCode.PermissionDenied, exception);
        }
        catch (Exception exception) when (IsProcessException(exception))
        {
            if (processStarted)
            {
                await TerminateAsync(process).ConfigureAwait(false);
            }

            return CreateStartFailure(ErrorCode.ProcessFailed, exception);
        }
    }

    private static ProcessStartInfo CreateStartInfo(ProcessRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.Executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

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

    private static OperationResult<ProcessResult> CreateStartFailure(
        ErrorCode errorCode,
        Exception exception) =>
        OperationResult<ProcessResult>.Failed(
            errorCode,
            "The requested executable could not be started or completed.",
            $"Process execution failed with {exception.GetType().Name}.");

    private static bool IsProcessException(Exception exception) =>
        exception is Win32Exception
            or InvalidOperationException
            or IOException
            or NotSupportedException;

    private static async Task TerminateAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (IsTerminationException(exception))
        {
            return;
        }

        try
        {
            using var terminationCancellation =
                new CancellationTokenSource(TerminationWait);
            await process.WaitForExitAsync(terminationCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is OperationCanceledException || IsTerminationException(exception))
        {
            // Best effort: disposal below still releases the process resources.
        }
    }

    private static bool IsTerminationException(Exception exception) =>
        exception is Win32Exception
            or InvalidOperationException
            or NotSupportedException;
}
