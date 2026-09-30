using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Services;

public sealed class RustDeskConfigurationWorkflow
{
    private const string RedactedValue = "[REDACTED]";

    private readonly IRustDeskDetector _detector;
    private readonly IRustDeskConfigurator _configurator;
    private readonly IRustDeskValidator _validator;
    private readonly IPlatformEnvironment _platformEnvironment;
    private readonly IAppLogger _logger;

    public RustDeskConfigurationWorkflow(
        IRustDeskDetector detector,
        IRustDeskConfigurator configurator,
        IRustDeskValidator validator,
        IPlatformEnvironment platformEnvironment,
        IAppLogger logger)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(platformEnvironment);
        ArgumentNullException.ThrowIfNull(logger);

        _detector = detector;
        _configurator = configurator;
        _validator = validator;
        _platformEnvironment = platformEnvironment;
        _logger = logger;
    }

    public async Task<OperationResult<RustDeskConfigurationWorkflowResult>> ExecuteAsync(
        RustDeskConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            return await ExecuteCoreAsync(configuration, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return CreateUnexpectedFailure(exception, configuration);
        }
    }

    public async Task<OperationResult<RustDeskConfigurationWorkflowResult>>
        ExecuteDetectedAsync(
            RustDeskInstallation installation,
            RustDeskConfiguration configuration,
            CancellationToken cancellationToken = default,
            IProgress<SetupProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(installation);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            return await ExecuteDetectedCoreAsync(
                    installation,
                    configuration,
                    cancellationToken,
                    progress)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return CreateUnexpectedFailure(exception, configuration);
        }
    }

    private async Task<OperationResult<RustDeskConfigurationWorkflowResult>> ExecuteCoreAsync(
        RustDeskConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var platform = _platformEnvironment.Current;

        _logger.Info($"{ApplicationInfo.Name} {ApplicationInfo.Version}");
        _logger.Info($"Platform: {platform.Kind} {platform.Architecture}");
        _logger.Info("Detection started.");

        var detection = await _detector
            .DetectAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!detection.Success || detection.Value is null)
        {
            return CreateFailure(
                detection.ErrorCode is ErrorCode.None
                    ? ErrorCode.DetectionFailed
                    : detection.ErrorCode,
                "RustDesk detection failed.",
                detection.TechnicalDetails ?? detection.Message,
                configuration);
        }

        var installation = detection.Value;

        if (!installation.Found)
        {
            _logger.Warning("RustDesk was not found. No changes were made.");

            return OperationResult<RustDeskConfigurationWorkflowResult>.Failed(
                ErrorCode.RustDeskNotFound,
                "RustDesk was not found. No changes were made.");
        }

        return await ExecuteDetectedCoreAsync(
                installation,
                configuration,
                cancellationToken,
                progress: null)
            .ConfigureAwait(false);
    }

    private async Task<OperationResult<RustDeskConfigurationWorkflowResult>>
        ExecuteDetectedCoreAsync(
            RustDeskInstallation installation,
            RustDeskConfiguration configuration,
            CancellationToken cancellationToken,
            IProgress<SetupProgress>? progress)
    {
        if (!installation.Found || string.IsNullOrWhiteSpace(installation.ExecutablePath))
        {
            return OperationResult<RustDeskConfigurationWorkflowResult>.Failed(
                ErrorCode.RustDeskNotFound,
                "RustDesk was not found. No changes were made.");
        }

        _logger.Info(
            $"RustDesk {installation.Version?.ToString() ?? "unknown version"} found at " +
            $"{installation.ExecutablePath}.");
        _logger.Info("Configuration started.");
        progress?.Report(new SetupProgress(SetupProgressStage.Configuring));

        var configurationResult = await _configurator
            .ConfigureAsync(installation, configuration, cancellationToken)
            .ConfigureAwait(false);

        if (!configurationResult.Success)
        {
            return CreateFailure(
                configurationResult.ErrorCode,
                "RustDesk configuration failed.",
                configurationResult.TechnicalDetails ?? configurationResult.Message,
                configuration);
        }

        _logger.Info("Configuration process completed with exit code 0.");
        _logger.Info("Validation started.");
        progress?.Report(new SetupProgress(SetupProgressStage.Validating));

        var validationResult = await _validator
            .ValidateAsync(installation, configuration, cancellationToken)
            .ConfigureAwait(false);

        if (!validationResult.Success || validationResult.Value is null)
        {
            return CreateFailure(
                validationResult.ErrorCode is ErrorCode.None
                    ? ErrorCode.ValidationFailed
                    : validationResult.ErrorCode,
                "RustDesk validation failed.",
                validationResult.TechnicalDetails ?? validationResult.Message,
                configuration);
        }

        _logger.Info($"Validation result: {validationResult.Value.Status}.");
        _logger.Info("Configuration workflow completed successfully.");

        var workflowResult = new RustDeskConfigurationWorkflowResult(
            installation,
            validationResult.Value,
            _logger.Destination);

        return OperationResult<RustDeskConfigurationWorkflowResult>.Succeeded(
            workflowResult,
            "RustDesk configuration workflow completed successfully.");
    }

    private OperationResult<RustDeskConfigurationWorkflowResult> CreateUnexpectedFailure(
        Exception exception,
        RustDeskConfiguration configuration)
    {
        var technicalDetails = Redact(
            $"{exception.GetType().Name}: {exception.Message}",
            configuration.ExportedConfiguration);

        _logger.Error("The configuration workflow failed unexpectedly.", technicalDetails);

        return OperationResult<RustDeskConfigurationWorkflowResult>.Failed(
            ErrorCode.UnexpectedFailure,
            "The RustDesk configuration workflow failed unexpectedly.",
            technicalDetails);
    }

    private OperationResult<RustDeskConfigurationWorkflowResult> CreateFailure(
        ErrorCode errorCode,
        string message,
        string? technicalDetails,
        RustDeskConfiguration configuration)
    {
        var safeDetails = Redact(
            technicalDetails,
            configuration.ExportedConfiguration);

        _logger.Error(message, safeDetails);

        return OperationResult<RustDeskConfigurationWorkflowResult>.Failed(
            errorCode is ErrorCode.None ? ErrorCode.UnexpectedFailure : errorCode,
            message,
            safeDetails);
    }

    private static string? Redact(string? value, string sensitiveValue)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(sensitiveValue))
        {
            return value;
        }

        return value.Replace(
            sensitiveValue,
            RedactedValue,
            StringComparison.Ordinal);
    }
}
