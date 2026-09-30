using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Core.Services;

namespace Technolife.RustDesk.Tests.Core.Services;

public sealed class RustDeskSetupWorkflowTests
{
    [Fact]
    public async Task SkipsInstallationWhenRustDeskAlreadyExists()
    {
        var detector = new SequenceDetector(Found());
        var installer = new FakeInstaller();
        var configurator = new FakeConfigurator();
        var validator = new FakeValidator();
        var workflow = CreateWorkflow(detector, installer, configurator, validator);

        var result = await workflow.ExecuteAsync(Configuration());

        var value = Assert.IsType<RustDeskSetupWorkflowResult>(result.Value);
        Assert.True(result.Success);
        Assert.False(value.InstallationPerformed);
        Assert.Equal(0, installer.CallCount);
        Assert.Equal(1, configurator.CallCount);
        Assert.Equal(1, validator.CallCount);
    }

    [Fact]
    public async Task InstallsRedetectsConfiguresAndValidatesInOrder()
    {
        var detector = new SequenceDetector(NotFound(), Found());
        var installer = new FakeInstaller();
        var configurator = new FakeConfigurator();
        var validator = new FakeValidator();
        var workflow = CreateWorkflow(detector, installer, configurator, validator);

        var result = await workflow.ExecuteAsync(Configuration());

        var value = Assert.IsType<RustDeskSetupWorkflowResult>(result.Value);
        Assert.True(result.Success);
        Assert.True(value.InstallationPerformed);
        Assert.Equal(2, detector.CallCount);
        Assert.Equal(1, installer.CallCount);
        Assert.Equal(1, configurator.CallCount);
        Assert.Equal(1, validator.CallCount);
    }

    [Theory]
    [InlineData(ErrorCode.DownloadFailed)]
    [InlineData(ErrorCode.ChecksumMismatch)]
    [InlineData(ErrorCode.InstallationFailed)]
    [InlineData(ErrorCode.ElevationFailed)]
    public async Task StopsWhenInstallationFails(ErrorCode errorCode)
    {
        var detector = new SequenceDetector(NotFound());
        var installer = new FakeInstaller
        {
            Result = OperationResult.Failed(
                errorCode,
                "Installation stage failed.")
        };
        var configurator = new FakeConfigurator();
        var validator = new FakeValidator();
        var workflow = CreateWorkflow(detector, installer, configurator, validator);

        var result = await workflow.ExecuteAsync(Configuration());

        Assert.False(result.Success);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal(1, detector.CallCount);
        Assert.Equal(0, configurator.CallCount);
        Assert.Equal(0, validator.CallCount);
    }

    [Fact]
    public async Task FailsWhenRustDeskRemainsMissingAfterInstallation()
    {
        var detector = new SequenceDetector(NotFound(), NotFound(), NotFound());
        var configurator = new FakeConfigurator();
        var workflow = CreateWorkflow(
            detector,
            new FakeInstaller(),
            configurator,
            new FakeValidator(),
            redetectionAttempts: 2);

        var result = await workflow.ExecuteAsync(Configuration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InstallationFailed, result.ErrorCode);
        Assert.Equal(3, detector.CallCount);
        Assert.Equal(0, configurator.CallCount);
    }

    [Fact]
    public async Task PropagatesConfigurationFailureAfterSuccessfulInstallation()
    {
        var configurator = new FakeConfigurator
        {
            Result = OperationResult.Failed(
                ErrorCode.ProcessFailed,
                "Configuration process failed.")
        };
        var validator = new FakeValidator();
        var workflow = CreateWorkflow(
            new SequenceDetector(NotFound(), Found()),
            new FakeInstaller(),
            configurator,
            validator);

        var result = await workflow.ExecuteAsync(Configuration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ProcessFailed, result.ErrorCode);
        Assert.Equal(0, validator.CallCount);
    }

    [Fact]
    public async Task RepeatedSetupDoesNotReinstallExistingRustDesk()
    {
        var detector = new SequenceDetector(Found(), Found());
        var installer = new FakeInstaller();
        var workflow = CreateWorkflow(
            detector,
            installer,
            new FakeConfigurator(),
            new FakeValidator());

        var first = await workflow.ExecuteAsync(Configuration());
        var second = await workflow.ExecuteAsync(Configuration());

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(0, installer.CallCount);
    }

    private static RustDeskSetupWorkflow CreateWorkflow(
        IRustDeskDetector detector,
        IRustDeskInstaller installer,
        IRustDeskConfigurator configurator,
        IRustDeskValidator validator,
        int redetectionAttempts = 2)
    {
        var platform = new StubPlatformEnvironment();
        var logger = new InMemoryLogger();
        var configurationWorkflow = new RustDeskConfigurationWorkflow(
            detector,
            configurator,
            validator,
            platform,
            logger);

        return new RustDeskSetupWorkflow(
            detector,
            installer,
            configurationWorkflow,
            platform,
            logger,
            redetectionAttempts,
            TimeSpan.Zero,
            (_, _) => Task.CompletedTask);
    }

    private static RustDeskConfiguration Configuration() =>
        new("id.example.test", "relay.example.test", "public-key", "exported-config");

    private static OperationResult<RustDeskInstallation> Found() =>
        OperationResult<RustDeskInstallation>.Succeeded(
            RustDeskInstallation.CreateFound(
                @"C:\Program Files\RustDesk\RustDesk.exe",
                new Version(1, 4, 9),
                Platform()),
            "Found.");

    private static OperationResult<RustDeskInstallation> NotFound() =>
        OperationResult<RustDeskInstallation>.Succeeded(
            RustDeskInstallation.CreateNotFound(Platform()),
            "Not found.");

    private static PlatformInfo Platform() =>
        new(PlatformKind.Windows, CpuArchitecture.X64);

    private sealed class SequenceDetector(
        params OperationResult<RustDeskInstallation>[] results) : IRustDeskDetector
    {
        private readonly Queue<OperationResult<RustDeskInstallation>> _results = new(results);
        public int CallCount { get; private set; }

        public Task<OperationResult<RustDeskInstallation>> DetectAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class FakeInstaller : IRustDeskInstaller
    {
        public OperationResult Result { get; init; } =
            OperationResult.Succeeded("Installed.");
        public int CallCount { get; private set; }

        public Task<OperationResult> InstallAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
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
        public int CallCount { get; private set; }

        public Task<OperationResult<RustDeskValidation>> ValidateAsync(
            RustDeskInstallation installation,
            RustDeskConfiguration configuration,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(
                OperationResult<RustDeskValidation>.Succeeded(
                    new RustDeskValidation(
                        RustDeskValidationStatus.Applied,
                        "Process completed."),
                    "Validated."));
        }
    }

    private sealed class StubPlatformEnvironment : IPlatformEnvironment
    {
        public PlatformInfo Current { get; } = Platform();
    }

    private sealed class InMemoryLogger : IAppLogger
    {
        public string Destination => "memory://setup.log";
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, string? technicalDetails = null) { }
    }
}
