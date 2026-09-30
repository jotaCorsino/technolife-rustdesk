using System.Security;
using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Abstractions;

namespace Technolife.RustDesk.Platforms.Windows;

public sealed class WindowsRustDeskInstaller : IRustDeskInstaller
{
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InstallationTimeout = TimeSpan.FromMinutes(5);

    private readonly RustDeskPackageManifest _manifest;
    private readonly IDownloadClient _downloadClient;
    private readonly IFileIntegrityValidator _integrityValidator;
    private readonly IProcessRunner _processRunner;
    private readonly IPlatformEnvironment _platformEnvironment;
    private readonly IInstallerFileSystem _fileSystem;
    private readonly IAppLogger _logger;
    private readonly string? _localInstallerPath;

    public WindowsRustDeskInstaller(
        RustDeskPackageManifest manifest,
        IDownloadClient downloadClient,
        IFileIntegrityValidator integrityValidator,
        IProcessRunner processRunner,
        IPlatformEnvironment platformEnvironment,
        IInstallerFileSystem fileSystem,
        IAppLogger logger,
        string? localInstallerPath = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(downloadClient);
        ArgumentNullException.ThrowIfNull(integrityValidator);
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(platformEnvironment);
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(logger);

        if (localInstallerPath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(localInstallerPath);
        }

        _manifest = manifest;
        _downloadClient = downloadClient;
        _integrityValidator = integrityValidator;
        _processRunner = processRunner;
        _platformEnvironment = platformEnvironment;
        _fileSystem = fileSystem;
        _logger = logger;
        _localInstallerPath = localInstallerPath;
    }

    public async Task<OperationResult> InstallAsync(
        CancellationToken cancellationToken = default,
        IProgress<SetupProgress>? progress = null)
    {
        var platform = _platformEnvironment.Current;

        if (platform.Kind is not PlatformKind.Windows ||
            _manifest.Platform is not PlatformKind.Windows)
        {
            return OperationResult.Failed(
                ErrorCode.UnsupportedPlatform,
                "The Windows RustDesk installer requires a Windows package and environment.");
        }

        if (platform.Architecture is not CpuArchitecture.X64 ||
            _manifest.Architecture is not CpuArchitecture.X64)
        {
            return OperationResult.Failed(
                ErrorCode.UnsupportedPlatform,
                "The homologated RustDesk package supports Windows x64 only.");
        }

        string? temporaryDirectory = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            temporaryDirectory = _fileSystem.CreateTemporaryDirectory();
            var packagePath = Path.Combine(temporaryDirectory, _manifest.FileName);

            _logger.Info(
                $"Homologated RustDesk package selected: {_manifest.Version} " +
                $"{_manifest.Platform} {_manifest.Architecture}.");
            progress?.Report(new SetupProgress(SetupProgressStage.Downloading));

            var acquisitionResult = await AcquirePackageAsync(
                    packagePath,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!acquisitionResult.Success)
            {
                return acquisitionResult;
            }

            var integrityResult = await _integrityValidator
                .ValidateSha256Async(
                    packagePath,
                    _manifest.Sha256,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!integrityResult.Success)
            {
                TryDeleteFile(packagePath);

                return OperationResult.Failed(
                    ErrorCode.ChecksumMismatch,
                    "The RustDesk package was rejected because its checksum is invalid.",
                    integrityResult.TechnicalDetails ?? integrityResult.Message);
            }

            _logger.Info("RustDesk package SHA-256 checksum validated.");
            _logger.Info("Elevated silent installation started.");
            progress?.Report(new SetupProgress(SetupProgressStage.Installing));

            var processRequest = new ProcessRequest(
                packagePath,
                ["--silent-install"],
                temporaryDirectory,
                InstallationTimeout,
                requiresElevation: true);
            var processResult = await _processRunner
                .RunAsync(processRequest, cancellationToken)
                .ConfigureAwait(false);

            if (!processResult.Success)
            {
                var errorCode = processResult.ErrorCode is ErrorCode.ElevationFailed
                    ? ErrorCode.ElevationFailed
                    : ErrorCode.InstallationFailed;

                return OperationResult.Failed(
                    errorCode,
                    errorCode is ErrorCode.ElevationFailed
                        ? "RustDesk installation elevation was declined or failed."
                        : "The RustDesk installer process failed.",
                    $"Installer process failed with error code {processResult.ErrorCode}.");
            }

            if (processResult.Value is null)
            {
                return OperationResult.Failed(
                    ErrorCode.InstallationFailed,
                    "The RustDesk installer returned no process result.");
            }

            _logger.Info(
                $"RustDesk installer completed with exit code {processResult.Value.ExitCode}.");

            if (processResult.Value.ExitCode is not 0)
            {
                return OperationResult.Failed(
                    ErrorCode.InstallationFailed,
                    "The RustDesk installer returned an error.",
                    $"Installer process exited with code {processResult.Value.ExitCode}.");
            }

            return OperationResult.Succeeded(
                "RustDesk silent installation completed successfully.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsInstallerFileSystemException(exception))
        {
            var errorCode = exception is UnauthorizedAccessException or SecurityException
                ? ErrorCode.PermissionDenied
                : ErrorCode.InstallationFailed;

            return OperationResult.Failed(
                errorCode,
                "The RustDesk installation files could not be prepared.",
                $"Installer file operation failed with {exception.GetType().Name}.");
        }
        finally
        {
            if (temporaryDirectory is not null)
            {
                TryDeleteDirectory(temporaryDirectory);
            }
        }
    }

    private async Task<OperationResult> AcquirePackageAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        if (_localInstallerPath is not null)
        {
            _logger.Info("Preparing the explicitly supplied local RustDesk package.");
            _fileSystem.CopyFile(_localInstallerPath, packagePath, overwrite: true);
            _logger.Info("Local RustDesk package prepared.");
            return OperationResult.Succeeded("The local RustDesk package was prepared.");
        }

        _logger.Info("Official RustDesk package download started.");

        var downloadRequest = new DownloadRequest(
            _manifest.DownloadUri,
            packagePath,
            _manifest.Sha256,
            DownloadTimeout);
        var downloadResult = await _downloadClient
            .DownloadAsync(downloadRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!downloadResult.Success)
        {
            return OperationResult.Failed(
                ErrorCode.DownloadFailed,
                "The homologated RustDesk package could not be downloaded.",
                downloadResult.TechnicalDetails ?? downloadResult.Message);
        }

        _logger.Info("Official RustDesk package download completed.");
        return OperationResult.Succeeded("The RustDesk package was downloaded.");
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            _fileSystem.DeleteFile(path);
        }
        catch (Exception exception) when (IsInstallerFileSystemException(exception))
        {
            _logger.Warning(
                $"The rejected package could not be removed ({exception.GetType().Name}).");
        }
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            _fileSystem.DeleteDirectory(path, recursive: true);
        }
        catch (Exception exception) when (IsInstallerFileSystemException(exception))
        {
            _logger.Warning(
                $"The temporary installer directory could not be removed " +
                $"({exception.GetType().Name}).");
        }
    }

    private static bool IsInstallerFileSystemException(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or SecurityException
            or NotSupportedException;
}
