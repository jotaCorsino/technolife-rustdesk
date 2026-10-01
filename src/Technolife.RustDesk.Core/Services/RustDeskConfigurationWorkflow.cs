using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Services;

public sealed class RustDeskConfigurationWorkflow
{
    private const string RedactedValue = "[REDACTED]";
    private static readonly TimeSpan DefaultServiceActivationTimeout =
        TimeSpan.FromSeconds(75);
    private static readonly TimeSpan DefaultConfigurationTimeout =
        TimeSpan.FromSeconds(45);
    private static readonly TimeSpan DefaultValidationTimeout =
        TimeSpan.FromMinutes(2);

    private readonly IRustDeskDetector _detector;
    private readonly IRustDeskServiceManager _serviceManager;
    private readonly IRustDeskConfigurator _configurator;
    private readonly IRustDeskValidator _validator;
    private readonly IPlatformEnvironment _platformEnvironment;
    private readonly IAppLogger _logger;
    private readonly TimeSpan _serviceActivationTimeout;
    private readonly TimeSpan _configurationTimeout;
    private readonly TimeSpan _validationTimeout;

    public RustDeskConfigurationWorkflow(
        IRustDeskDetector detector,
        IRustDeskServiceManager serviceManager,
        IRustDeskConfigurator configurator,
        IRustDeskValidator validator,
        IPlatformEnvironment platformEnvironment,
        IAppLogger logger,
        TimeSpan? serviceActivationTimeout = null,
        TimeSpan? configurationTimeout = null,
        TimeSpan? validationTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(serviceManager);
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(platformEnvironment);
        ArgumentNullException.ThrowIfNull(logger);

        ValidateTimeout(serviceActivationTimeout, nameof(serviceActivationTimeout));
        ValidateTimeout(configurationTimeout, nameof(configurationTimeout));
        ValidateTimeout(validationTimeout, nameof(validationTimeout));

        _detector = detector;
        _serviceManager = serviceManager;
        _configurator = configurator;
        _validator = validator;
        _platformEnvironment = platformEnvironment;
        _logger = logger;
        _serviceActivationTimeout =
            serviceActivationTimeout ?? DefaultServiceActivationTimeout;
        _configurationTimeout = configurationTimeout ?? DefaultConfigurationTimeout;
        _validationTimeout = validationTimeout ?? DefaultValidationTimeout;
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
        _logger.Info("RustDesk service activation started.");
        progress?.Report(new SetupProgress(SetupProgressStage.StartingService));

        var serviceActivationResult = await ExecuteWithTimeoutAsync(
                token => ActivateServiceAsync(installation, token),
                _serviceActivationTimeout,
                ErrorCode.InstallationFailed,
                "RustDesk service activation",
                cancellationToken)
            .ConfigureAwait(false);

        if (!serviceActivationResult.Success)
        {
            return CreateFailure(
                serviceActivationResult.ErrorCode,
                "RustDesk service activation failed.",
                serviceActivationResult.TechnicalDetails ?? serviceActivationResult.Message,
                configuration);
        }

        _logger.Info("RustDesk service is running.");
        _logger.Info("Configuration started.");
        progress?.Report(new SetupProgress(SetupProgressStage.Configuring));

        var configurationResult = await ExecuteWithTimeoutAsync(
                token => _configurator.ConfigureAsync(
                    installation,
                    configuration,
                    token),
                _configurationTimeout,
                ErrorCode.ConfigurationFailed,
                "RustDesk configuration",
                cancellationToken)
            .ConfigureAwait(false);

        if (!configurationResult.Success)
        {
            return CreateFailure(
                configurationResult.ErrorCode,
                "RustDesk configuration failed.",
                configurationResult.TechnicalDetails ?? configurationResult.Message,
                configuration);
        }

        _logger.Info("Configuration process completed with exit code 0 (Applied).");
        _logger.Info("Independent configuration verification started.");
        progress?.Report(new SetupProgress(SetupProgressStage.Verifying));

        var validationResult = await ExecuteWithTimeoutAsync(
                token => _validator.ValidateAsync(
                    installation,
                    configuration,
                    token),
                _validationTimeout,
                ErrorCode.ValidationFailed,
                "RustDesk validation",
                cancellationToken)
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

        if (validationResult.Value.Status is not RustDeskValidationStatus.Verified)
        {
            return CreateFailure(
                ErrorCode.ValidationFailed,
                "RustDesk validation did not verify the expected configuration.",
                $"Validator returned status {validationResult.Value.Status}.",
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

    private async Task<OperationResult> ActivateServiceAsync(
        RustDeskInstallation installation,
        CancellationToken cancellationToken)
    {
        var installationResult = await _serviceManager
            .EnsureInstalledAsync(installation, cancellationToken)
            .ConfigureAwait(false);

        if (!installationResult.Success)
        {
            return installationResult;
        }

        return await _serviceManager
            .EnsureRunningAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<OperationResult> ExecuteWithTimeoutAsync(
        Func<CancellationToken, Task<OperationResult>> operation,
        TimeSpan timeout,
        ErrorCode timeoutErrorCode,
        string stageName,
        CancellationToken cancellationToken)
    {
        using var stageCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var operationTask = operation(stageCancellation.Token);

        try
        {
            return await operationTask
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            stageCancellation.Cancel();
            ObserveFault(operationTask);
            return OperationResult.Failed(
                timeoutErrorCode,
                $"{stageName} timed out.",
                $"Stage exceeded its timeout of {timeout:c}.");
        }
    }

    private static async Task<OperationResult<RustDeskValidation>>
        ExecuteWithTimeoutAsync(
            Func<CancellationToken, Task<OperationResult<RustDeskValidation>>> operation,
            TimeSpan timeout,
            ErrorCode timeoutErrorCode,
            string stageName,
            CancellationToken cancellationToken)
    {
        using var stageCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var operationTask = operation(stageCancellation.Token);

        try
        {
            return await operationTask
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            stageCancellation.Cancel();
            ObserveFault(operationTask);
            return OperationResult<RustDeskValidation>.Failed(
                timeoutErrorCode,
                $"{stageName} timed out.",
                $"Stage exceeded its timeout of {timeout:c}.");
        }
    }

    private static void ObserveFault(Task task) =>
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously |
                TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

    private static void ValidateTimeout(TimeSpan? timeout, string parameterName)
    {
        if (timeout.HasValue && timeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
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
