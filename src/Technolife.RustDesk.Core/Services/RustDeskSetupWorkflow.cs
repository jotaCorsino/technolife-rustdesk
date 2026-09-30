using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Services;

public sealed class RustDeskSetupWorkflow
{
    private const string RedactedValue = "[REDACTED]";

    private readonly IRustDeskDetector _detector;
    private readonly IRustDeskInstaller _installer;
    private readonly RustDeskConfigurationWorkflow _configurationWorkflow;
    private readonly IPlatformEnvironment _platformEnvironment;
    private readonly IAppLogger _logger;
    private readonly int _redetectionAttempts;
    private readonly TimeSpan _redetectionDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public RustDeskSetupWorkflow(
        IRustDeskDetector detector,
        IRustDeskInstaller installer,
        RustDeskConfigurationWorkflow configurationWorkflow,
        IPlatformEnvironment platformEnvironment,
        IAppLogger logger,
        int redetectionAttempts = 10,
        TimeSpan? redetectionDelay = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(installer);
        ArgumentNullException.ThrowIfNull(configurationWorkflow);
        ArgumentNullException.ThrowIfNull(platformEnvironment);
        ArgumentNullException.ThrowIfNull(logger);

        if (redetectionAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(redetectionAttempts));
        }

        if (redetectionDelay.HasValue && redetectionDelay.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(redetectionDelay));
        }

        _detector = detector;
        _installer = installer;
        _configurationWorkflow = configurationWorkflow;
        _platformEnvironment = platformEnvironment;
        _logger = logger;
        _redetectionAttempts = redetectionAttempts;
        _redetectionDelay = redetectionDelay ?? TimeSpan.FromSeconds(2);
        _delay = delay ?? Task.Delay;
    }

    public async Task<OperationResult<RustDeskSetupWorkflowResult>> ExecuteAsync(
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
            var technicalDetails = Redact(
                $"{exception.GetType().Name}: {exception.Message}",
                configuration.ExportedConfiguration);

            _logger.Error("The setup workflow failed unexpectedly.", technicalDetails);

            return OperationResult<RustDeskSetupWorkflowResult>.Failed(
                ErrorCode.UnexpectedFailure,
                "The RustDesk setup workflow failed unexpectedly.",
                technicalDetails);
        }
    }

    private async Task<OperationResult<RustDeskSetupWorkflowResult>> ExecuteCoreAsync(
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
        var installationPerformed = false;

        if (installation.Found)
        {
            _logger.Info("RustDesk is already installed. Installation is not required.");
        }
        else
        {
            _logger.Info("RustDesk was not found.");
            _logger.Info("Installation started.");

            var installationResult = await _installer
                .InstallAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!installationResult.Success)
            {
                return CreateFailure(
                    installationResult.ErrorCode,
                    "RustDesk installation failed.",
                    installationResult.TechnicalDetails ?? installationResult.Message,
                    configuration);
            }

            installationPerformed = true;
            _logger.Info("Installation process completed successfully.");
            _logger.Info("Redetection started.");

            var redetection = await RedetectAsync(cancellationToken).ConfigureAwait(false);

            if (!redetection.Success || redetection.Value is null)
            {
                return CreateFailure(
                    redetection.ErrorCode is ErrorCode.None
                        ? ErrorCode.DetectionFailed
                        : redetection.ErrorCode,
                    "RustDesk redetection failed after installation.",
                    redetection.TechnicalDetails ?? redetection.Message,
                    configuration);
            }

            installation = redetection.Value;

            if (!installation.Found)
            {
                return CreateFailure(
                    ErrorCode.InstallationFailed,
                    "RustDesk was not found after the installation completed.",
                    "All post-installation detection attempts returned not found.",
                    configuration);
            }

            _logger.Info("RustDesk was detected after installation.");
        }

        var configurationResult = await _configurationWorkflow
            .ExecuteDetectedAsync(installation, configuration, cancellationToken)
            .ConfigureAwait(false);

        if (!configurationResult.Success || configurationResult.Value is null)
        {
            return OperationResult<RustDeskSetupWorkflowResult>.Failed(
                configurationResult.ErrorCode is ErrorCode.None
                    ? ErrorCode.UnexpectedFailure
                    : configurationResult.ErrorCode,
                configurationResult.Message,
                Redact(
                    configurationResult.TechnicalDetails,
                    configuration.ExportedConfiguration));
        }

        _logger.Info("Setup workflow completed successfully.");

        return OperationResult<RustDeskSetupWorkflowResult>.Succeeded(
            new RustDeskSetupWorkflowResult(
                configurationResult.Value,
                installationPerformed),
            "RustDesk setup workflow completed successfully.");
    }

    private async Task<OperationResult<RustDeskInstallation>> RedetectAsync(
        CancellationToken cancellationToken)
    {
        OperationResult<RustDeskInstallation>? lastResult = null;

        for (var attempt = 1; attempt <= _redetectionAttempts; attempt++)
        {
            lastResult = await _detector
                .DetectAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!lastResult.Success || lastResult.Value?.Found is true)
            {
                return lastResult;
            }

            if (attempt < _redetectionAttempts)
            {
                _logger.Info(
                    $"RustDesk not found after installation; redetection attempt " +
                    $"{attempt + 1} of {_redetectionAttempts} pending.");
                await _delay(_redetectionDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        return lastResult!;
    }

    private OperationResult<RustDeskSetupWorkflowResult> CreateFailure(
        ErrorCode errorCode,
        string message,
        string? technicalDetails,
        RustDeskConfiguration configuration)
    {
        var safeDetails = Redact(
            technicalDetails,
            configuration.ExportedConfiguration);

        _logger.Error(message, safeDetails);

        return OperationResult<RustDeskSetupWorkflowResult>.Failed(
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
