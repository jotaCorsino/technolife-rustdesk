using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Core.Services;

namespace Technolife.RustDesk.Tests.Core.Services;

public sealed class RustDeskConfigurationWorkflowTests
{
    private const string SensitiveConfiguration = "sensitive-exported-configuration";

    [Fact]
    public async Task StopsWhenDetectorFails()
    {
        var detector = new FakeDetector
        {
            Result = OperationResult<RustDeskInstallation>.Failed(
                ErrorCode.DetectionFailed,
                "Detection failed.")
        };
        var configurator = new FakeConfigurator();
        var validator = new FakeValidator();
        var workflow = CreateWorkflow(detector, configurator, validator);

        var result = await workflow.ExecuteAsync(CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.DetectionFailed, result.ErrorCode);
        Assert.Equal(1, detector.CallCount);
        Assert.Equal(0, configurator.CallCount);
        Assert.Equal(0, validator.CallCount);
    }

    [Fact]
    public async Task StopsWhenRustDeskIsNotFound()
    {
        var detector = new FakeDetector
        {
            Result = OperationResult<RustDeskInstallation>.Succeeded(
                RustDeskInstallation.CreateNotFound(CreatePlatform()),
                "Not found.")
        };
        var configurator = new FakeConfigurator();
        var validator = new FakeValidator();
        var workflow = CreateWorkflow(detector, configurator, validator);

        var result = await workflow.ExecuteAsync(CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.RustDeskNotFound, result.ErrorCode);
        Assert.Equal(0, configurator.CallCount);
        Assert.Equal(0, validator.CallCount);
    }

    [Fact]
    public async Task RunsDetectionConfigurationAndValidationInOrder()
    {
        var logger = new InMemoryLogger();
        var detector = new FakeDetector();
        var configurator = new FakeConfigurator();
        var validator = new FakeValidator();
        var workflow = CreateWorkflow(detector, configurator, validator, logger);

        var result = await workflow.ExecuteAsync(CreateConfiguration());

        var workflowResult = Assert.IsType<RustDeskConfigurationWorkflowResult>(result.Value);
        Assert.True(result.Success);
        Assert.Equal(RustDeskValidationStatus.Verified, workflowResult.Validation.Status);
        Assert.Equal(1, detector.CallCount);
        Assert.Equal(1, configurator.CallCount);
        Assert.Equal(1, validator.CallCount);
        Assert.Contains(logger.Entries, entry => entry.Contains("Detection started."));
        Assert.Contains(logger.Entries, entry => entry.Contains("exit code 0"));
        Assert.Contains(logger.Entries, entry => entry.Contains("Validation result: Verified"));
    }

    [Fact]
    public async Task StopsBeforeConfigurationWhenServiceCannotStart()
    {
        var serviceManager = new FakeServiceManager
        {
            EnsureRunningResult = OperationResult.Failed(
                ErrorCode.InstallationFailed,
                "Service did not start.")
        };
        var configurator = new FakeConfigurator();
        var validator = new FakeValidator();
        var workflow = CreateWorkflow(
            new FakeDetector(),
            configurator,
            validator,
            serviceManager: serviceManager);

        var result = await workflow.ExecuteAsync(CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InstallationFailed, result.ErrorCode);
        Assert.Equal(1, serviceManager.EnsureInstalledCallCount);
        Assert.Equal(1, serviceManager.EnsureRunningCallCount);
        Assert.Equal(0, configurator.CallCount);
        Assert.Equal(0, validator.CallCount);
    }

    [Fact]
    public async Task RejectsAppliedStatusWithoutIndependentVerification()
    {
        var validator = new FakeValidator
        {
            Result = OperationResult<RustDeskValidation>.Succeeded(
                new RustDeskValidation(
                    RustDeskValidationStatus.Applied,
                    "Only the command exit code was checked."),
                "Applied only.")
        };
        var workflow = CreateWorkflow(
            new FakeDetector(),
            new FakeConfigurator(),
            validator);

        var result = await workflow.ExecuteAsync(CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task StopsWhenConfiguratorFails()
    {
        var configurator = new FakeConfigurator
        {
            Result = OperationResult.Failed(
                ErrorCode.ProcessFailed,
                "Configuration failed.")
        };
        var validator = new FakeValidator();
        var workflow = CreateWorkflow(new FakeDetector(), configurator, validator);

        var result = await workflow.ExecuteAsync(CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ProcessFailed, result.ErrorCode);
        Assert.Equal(1, configurator.CallCount);
        Assert.Equal(0, validator.CallCount);
    }

    [Fact]
    public async Task ReturnsFailureWhenValidatorFails()
    {
        var validator = new FakeValidator
        {
            Result = OperationResult<RustDeskValidation>.Failed(
                ErrorCode.ValidationFailed,
                "Validation failed.")
        };
        var workflow = CreateWorkflow(
            new FakeDetector(),
            new FakeConfigurator(),
            validator);

        var result = await workflow.ExecuteAsync(CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Equal(1, validator.CallCount);
    }

    [Fact]
    public async Task RedactsConfigurationFromLogsAndFailureResult()
    {
        var logger = new InMemoryLogger();
        var configurator = new FakeConfigurator
        {
            Result = OperationResult.Failed(
                ErrorCode.ProcessFailed,
                $"Failed for {SensitiveConfiguration}.",
                $"Technical failure for {SensitiveConfiguration}.")
        };
        var workflow = CreateWorkflow(
            new FakeDetector(),
            configurator,
            new FakeValidator(),
            logger);

        var result = await workflow.ExecuteAsync(CreateConfiguration());
        var log = string.Join(Environment.NewLine, logger.Entries);

        Assert.DoesNotContain(SensitiveConfiguration, result.Message);
        Assert.DoesNotContain(
            SensitiveConfiguration,
            result.TechnicalDetails ?? string.Empty);
        Assert.DoesNotContain(SensitiveConfiguration, log);
        Assert.Contains("[REDACTED]", log);
    }

    [Fact]
    public async Task ConvertsUnexpectedExceptionToRedactedFailure()
    {
        var logger = new InMemoryLogger();
        var detector = new FakeDetector
        {
            Exception = new InvalidOperationException(SensitiveConfiguration)
        };
        var workflow = CreateWorkflow(
            detector,
            new FakeConfigurator(),
            new FakeValidator(),
            logger);

        var result = await workflow.ExecuteAsync(CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.UnexpectedFailure, result.ErrorCode);
        Assert.DoesNotContain(
            SensitiveConfiguration,
            result.TechnicalDetails ?? string.Empty);
        Assert.DoesNotContain(
            SensitiveConfiguration,
            string.Join(Environment.NewLine, logger.Entries));
    }

    private static RustDeskConfigurationWorkflow CreateWorkflow(
        IRustDeskDetector detector,
        IRustDeskConfigurator configurator,
        IRustDeskValidator validator,
        IAppLogger? logger = null,
        IRustDeskServiceManager? serviceManager = null) =>
        new(
            detector,
            serviceManager ?? new FakeServiceManager(),
            configurator,
            validator,
            new StubPlatformEnvironment(),
            logger ?? new InMemoryLogger());

    private static RustDeskConfiguration CreateConfiguration() =>
        new(
            "id.example.test",
            "relay.example.test",
            "public-key",
            SensitiveConfiguration);

    private static RustDeskInstallation CreateInstallation() =>
        RustDeskInstallation.CreateFound(
            @"C:\Program Files\RustDesk\RustDesk.exe",
            new Version(1, 4, 9),
            CreatePlatform());

    private static PlatformInfo CreatePlatform() =>
        new(PlatformKind.Windows, CpuArchitecture.X64);

    private sealed class FakeDetector : IRustDeskDetector
    {
        public OperationResult<RustDeskInstallation> Result { get; init; } =
            OperationResult<RustDeskInstallation>.Succeeded(
                CreateInstallation(),
                "RustDesk found.");

        public Exception? Exception { get; init; }

        public int CallCount { get; private set; }

        public Task<OperationResult<RustDeskInstallation>> DetectAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class FakeConfigurator : IRustDeskConfigurator
    {
        public OperationResult Result { get; init; } =
            OperationResult.Succeeded("Configured.");

        public int CallCount { get; private set; }

        public Task<OperationResult> ConfigureAsync(
            RustDeskInstallation installation,
            RustDeskConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeValidator : IRustDeskValidator
    {
        public OperationResult<RustDeskValidation> Result { get; init; } =
            OperationResult<RustDeskValidation>.Succeeded(
                new RustDeskValidation(
                    RustDeskValidationStatus.Verified,
                    "Service and fields were independently verified."),
                "Verified.");

        public int CallCount { get; private set; }

        public Task<OperationResult<RustDeskValidation>> ValidateAsync(
            RustDeskInstallation installation,
            RustDeskConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeServiceManager : IRustDeskServiceManager
    {
        public OperationResult EnsureInstalledResult { get; init; } =
            OperationResult.Succeeded("Installed.");

        public OperationResult EnsureRunningResult { get; init; } =
            OperationResult.Succeeded("Running.");

        public int EnsureInstalledCallCount { get; private set; }

        public int EnsureRunningCallCount { get; private set; }

        public Task<OperationResult<RustDeskServiceStatus>> GetStatusAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                OperationResult<RustDeskServiceStatus>.Succeeded(
                    RustDeskServiceStatus.Running,
                    "Running."));

        public Task<OperationResult> EnsureInstalledAsync(
            RustDeskInstallation installation,
            CancellationToken cancellationToken = default)
        {
            EnsureInstalledCallCount++;
            return Task.FromResult(EnsureInstalledResult);
        }

        public Task<OperationResult> EnsureRunningAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureRunningCallCount++;
            return Task.FromResult(EnsureRunningResult);
        }
    }

    private sealed class StubPlatformEnvironment : IPlatformEnvironment
    {
        public PlatformInfo Current { get; } = CreatePlatform();
    }

    private sealed class InMemoryLogger : IAppLogger
    {
        public string Destination => "memory://workflow.log";

        public List<string> Entries { get; } = [];

        public void Info(string message) => Entries.Add($"INFO {message}");

        public void Warning(string message) => Entries.Add($"WARN {message}");

        public void Error(string message, string? technicalDetails = null) =>
            Entries.Add($"ERROR {message} {technicalDetails}");
    }
}
