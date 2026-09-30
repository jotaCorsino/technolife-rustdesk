using System.ComponentModel;
using System.Security;
using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Abstractions;

namespace Technolife.RustDesk.Platforms.Windows;

public sealed class WindowsRustDeskDetector : IRustDeskDetector
{
    private static readonly PlatformInfo WindowsPlatform =
        new(PlatformKind.Windows, CpuArchitecture.Unknown);

    private readonly IFileProbe _fileProbe;
    private readonly IPlatformEnvironment _platformEnvironment;
    private readonly WindowsRustDeskPaths _paths;

    public WindowsRustDeskDetector()
        : this(
            new SystemFileProbe(),
            new PlatformInformationProvider(),
            WindowsRustDeskPaths.FromCurrentEnvironment())
    {
    }

    public WindowsRustDeskDetector(
        IFileProbe fileProbe,
        IPlatformEnvironment platformEnvironment,
        WindowsRustDeskPaths paths)
    {
        ArgumentNullException.ThrowIfNull(fileProbe);
        ArgumentNullException.ThrowIfNull(platformEnvironment);
        ArgumentNullException.ThrowIfNull(paths);

        _fileProbe = fileProbe;
        _platformEnvironment = platformEnvironment;
        _paths = paths;
    }

    public Task<OperationResult<RustDeskInstallation>> DetectAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_platformEnvironment.Current.Kind is not PlatformKind.Windows)
        {
            return Task.FromResult(
                OperationResult<RustDeskInstallation>.Failed(
                    ErrorCode.UnsupportedPlatform,
                    "The Windows RustDesk detector requires a Windows environment."));
        }

        foreach (var candidatePath in _paths.CandidatePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!_fileProbe.FileExists(candidatePath))
                {
                    continue;
                }
            }
            catch (Exception exception) when (IsFileSystemAccessException(exception))
            {
                return Task.FromResult(CreateAccessFailure(exception));
            }

            var installation = RustDeskInstallation.CreateFound(
                candidatePath,
                TryReadVersion(candidatePath),
                WindowsPlatform);

            return Task.FromResult(
                OperationResult<RustDeskInstallation>.Succeeded(
                    installation,
                    "RustDesk installation found."));
        }

        return Task.FromResult(
            OperationResult<RustDeskInstallation>.Succeeded(
                RustDeskInstallation.CreateNotFound(WindowsPlatform),
                "RustDesk was not found in known Windows installation locations."));
    }

    private Version? TryReadVersion(string path)
    {
        try
        {
            return _fileProbe.ReadVersion(path);
        }
        catch (Exception exception) when (IsFileMetadataException(exception))
        {
            return null;
        }
    }

    private static OperationResult<RustDeskInstallation> CreateAccessFailure(Exception exception)
    {
        var errorCode = exception is UnauthorizedAccessException or SecurityException
            ? ErrorCode.PermissionDenied
            : ErrorCode.DetectionFailed;

        return OperationResult<RustDeskInstallation>.Failed(
            errorCode,
            "RustDesk detection could not access a Windows installation location.",
            exception.Message);
    }

    private static bool IsFileSystemAccessException(Exception exception) =>
        exception is UnauthorizedAccessException
            or SecurityException
            or IOException
            or Win32Exception;

    private static bool IsFileMetadataException(Exception exception) =>
        IsFileSystemAccessException(exception)
        || exception is ArgumentException
            or NotSupportedException;
}
