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
    private readonly IRustDeskServiceManager _serviceManager;
    private readonly IRustDeskOptionReader _optionReader;
    private readonly int _verificationAttempts;
    private readonly TimeSpan _verificationDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public WindowsRustDeskValidator(
        IFileProbe fileProbe,
        IRustDeskServiceManager serviceManager,
        IRustDeskOptionReader optionReader,
        int verificationAttempts = 5,
        TimeSpan? verificationDelay = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(fileProbe);
        ArgumentNullException.ThrowIfNull(serviceManager);
        ArgumentNullException.ThrowIfNull(optionReader);

        if (verificationAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(verificationAttempts));
        }

        if (verificationDelay.HasValue && verificationDelay.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(verificationDelay));
        }

        _fileProbe = fileProbe;
        _serviceManager = serviceManager;
        _optionReader = optionReader;
        _verificationAttempts = verificationAttempts;
        _verificationDelay = verificationDelay ?? TimeSpan.FromSeconds(1);
        _delay = delay ?? Task.Delay;
    }

    public async Task<OperationResult<RustDeskValidation>> ValidateAsync(
        RustDeskInstallation installation,
        RustDeskConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();

        var preconditionFailure = ValidatePreconditions(installation, configuration);

        if (preconditionFailure is not null)
        {
            return preconditionFailure;
        }

        var accessibilityFailure = ValidateExecutableAccessibility(installation);

        if (accessibilityFailure is not null)
        {
            return accessibilityFailure;
        }

        string[] mismatchedOptions = [];

        for (var attempt = 1; attempt <= _verificationAttempts; attempt++)
        {
            var serviceStatus = await _serviceManager
                .GetStatusAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!serviceStatus.Success)
            {
                return OperationResult<RustDeskValidation>.Failed(
                    serviceStatus.ErrorCode is ErrorCode.None
                        ? ErrorCode.ValidationFailed
                        : serviceStatus.ErrorCode,
                    "The RustDesk service status could not be verified.",
                    serviceStatus.TechnicalDetails ?? serviceStatus.Message);
            }

            if (serviceStatus.Value is not RustDeskServiceStatus.Running)
            {
                return OperationResult<RustDeskValidation>.Failed(
                    ErrorCode.ValidationFailed,
                    "The RustDesk service is not running after configuration.",
                    $"Service status was {serviceStatus.Value}.");
            }

            var optionResults = await ReadExpectedOptionsAsync(
                    installation,
                    configuration,
                    cancellationToken)
                .ConfigureAwait(false);
            var readFailure = optionResults.FirstOrDefault(result => !result.Result.Success);

            if (readFailure is not null)
            {
                if (attempt == _verificationAttempts ||
                    readFailure.Result.ErrorCode is ErrorCode.ElevationFailed or
                        ErrorCode.PermissionDenied)
                {
                    return OperationResult<RustDeskValidation>.Failed(
                        readFailure.Result.ErrorCode is ErrorCode.None
                            ? ErrorCode.ValidationFailed
                            : readFailure.Result.ErrorCode,
                        "The applied RustDesk configuration could not be read back.",
                        readFailure.Result.TechnicalDetails ?? readFailure.Result.Message);
                }
            }
            else
            {
                mismatchedOptions = optionResults
                    .Where(result => !string.Equals(
                        result.Result.Value,
                        result.ExpectedValue,
                        StringComparison.Ordinal))
                    .Select(result => result.OptionName)
                    .ToArray();

                if (mismatchedOptions.Length is 0)
                {
                    var validation = new RustDeskValidation(
                        RustDeskValidationStatus.Verified,
                        "The RustDesk service is running and the ID server, relay server, " +
                        "and public key were independently read back and matched.");

                    return OperationResult<RustDeskValidation>.Succeeded(
                        validation,
                        "The RustDesk configuration was verified successfully.");
                }
            }

            if (attempt < _verificationAttempts)
            {
                await _delay(_verificationDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        var optionNames = mismatchedOptions.Length is 0
            ? "one or more required options"
            : string.Join(", ", mismatchedOptions);

        return OperationResult<RustDeskValidation>.Failed(
            ErrorCode.ValidationFailed,
            "The applied RustDesk configuration does not match the expected values.",
            $"Verification failed for {optionNames}; values were omitted from diagnostics.");
    }

    private async Task<OptionExpectation[]> ReadExpectedOptionsAsync(
        RustDeskInstallation installation,
        RustDeskConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var expectations = new[]
        {
            new OptionExpectation(WindowsRustDeskOptions.IdServer, configuration.IdServer),
            new OptionExpectation(WindowsRustDeskOptions.RelayServer, configuration.RelayServer),
            new OptionExpectation(WindowsRustDeskOptions.PublicKey, configuration.PublicKey)
        };

        foreach (var expectation in expectations)
        {
            expectation.Result = await _optionReader
                .ReadAsync(installation, expectation.OptionName, cancellationToken)
                .ConfigureAwait(false);
        }

        return expectations;
    }

    private static OperationResult<RustDeskValidation>? ValidatePreconditions(
        RustDeskInstallation installation,
        RustDeskConfiguration configuration)
    {
        if (!installation.Found || string.IsNullOrWhiteSpace(installation.ExecutablePath))
        {
            return OperationResult<RustDeskValidation>.Failed(
                ErrorCode.RustDeskNotFound,
                "RustDesk is not available for post-configuration validation.");
        }

        if (installation.Platform.Kind is not PlatformKind.Windows)
        {
            return OperationResult<RustDeskValidation>.Failed(
                ErrorCode.UnsupportedPlatform,
                "The Windows validator requires a Windows RustDesk installation.");
        }

        if (string.IsNullOrWhiteSpace(configuration.ExportedConfiguration))
        {
            return OperationResult<RustDeskValidation>.Failed(
                ErrorCode.ConfigurationFailed,
                "The exported RustDesk configuration is required for validation.");
        }

        return null;
    }

    private OperationResult<RustDeskValidation>? ValidateExecutableAccessibility(
        RustDeskInstallation installation)
    {
        try
        {
            if (!_fileProbe.FileExists(installation.ExecutablePath!))
            {
                return OperationResult<RustDeskValidation>.Failed(
                    ErrorCode.ValidationFailed,
                    "RustDesk was no longer accessible after configuration.");
            }
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            var errorCode = exception is UnauthorizedAccessException or SecurityException
                ? ErrorCode.PermissionDenied
                : ErrorCode.ValidationFailed;

            return OperationResult<RustDeskValidation>.Failed(
                errorCode,
                "RustDesk accessibility could not be validated.",
                $"Validation probe failed with {exception.GetType().Name}.");
        }

        return null;
    }

    private static bool IsFileSystemException(Exception exception) =>
        exception is UnauthorizedAccessException
            or SecurityException
            or IOException
            or Win32Exception
            or NotSupportedException;

    private sealed class OptionExpectation(string optionName, string expectedValue)
    {
        public string OptionName { get; } = optionName;

        public string ExpectedValue { get; } = expectedValue;

        public OperationResult<string> Result { get; set; } =
            OperationResult<string>.Failed(
                ErrorCode.ValidationFailed,
                "The option has not been read.");
    }
}
