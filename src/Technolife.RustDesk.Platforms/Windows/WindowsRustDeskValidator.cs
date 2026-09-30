using System.ComponentModel;
using System.Security;
using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Abstractions;

namespace Technolife.RustDesk.Platforms.Windows;

public sealed class WindowsRustDeskValidator : IRustDeskValidator
{
    private readonly IFileProbe _fileProbe;

    public WindowsRustDeskValidator()
        : this(new SystemFileProbe())
    {
    }

    public WindowsRustDeskValidator(IFileProbe fileProbe)
    {
        ArgumentNullException.ThrowIfNull(fileProbe);
        _fileProbe = fileProbe;
    }

    public Task<OperationResult<RustDeskValidation>> ValidateAsync(
        RustDeskInstallation installation,
        RustDeskConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();

        if (!installation.Found || string.IsNullOrWhiteSpace(installation.ExecutablePath))
        {
            return Task.FromResult(
                OperationResult<RustDeskValidation>.Failed(
                    ErrorCode.RustDeskNotFound,
                    "RustDesk is not available for post-configuration validation."));
        }

        if (installation.Platform.Kind is not PlatformKind.Windows)
        {
            return Task.FromResult(
                OperationResult<RustDeskValidation>.Failed(
                    ErrorCode.UnsupportedPlatform,
                    "The Windows validator requires a Windows RustDesk installation."));
        }

        if (string.IsNullOrWhiteSpace(configuration.ExportedConfiguration))
        {
            return Task.FromResult(
                OperationResult<RustDeskValidation>.Failed(
                    ErrorCode.ConfigurationFailed,
                    "The exported RustDesk configuration is required for validation."));
        }

        try
        {
            if (!_fileProbe.FileExists(installation.ExecutablePath))
            {
                return Task.FromResult(
                    OperationResult<RustDeskValidation>.Failed(
                        ErrorCode.ValidationFailed,
                        "RustDesk was no longer accessible after configuration."));
            }
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            var errorCode = exception is UnauthorizedAccessException or SecurityException
                ? ErrorCode.PermissionDenied
                : ErrorCode.ValidationFailed;

            return Task.FromResult(
                OperationResult<RustDeskValidation>.Failed(
                    errorCode,
                    "RustDesk accessibility could not be validated.",
                    $"Validation probe failed with {exception.GetType().Name}."));
        }

        var validation = new RustDeskValidation(
            RustDeskValidationStatus.Applied,
            "The configuration process completed successfully and the RustDesk executable " +
            "remains accessible. The configured server fields were not independently read back.");

        return Task.FromResult(
            OperationResult<RustDeskValidation>.Succeeded(
                validation,
                "RustDesk configuration was applied; complete field verification is unavailable."));
    }

    private static bool IsFileSystemException(Exception exception) =>
        exception is UnauthorizedAccessException
            or SecurityException
            or IOException
            or Win32Exception
            or NotSupportedException;
}
