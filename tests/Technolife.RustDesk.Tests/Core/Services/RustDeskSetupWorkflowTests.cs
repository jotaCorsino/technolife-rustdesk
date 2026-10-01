using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Core.Services;
using Technolife.RustDesk.Platforms.Abstractions;
using Technolife.RustDesk.Platforms.Windows;

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
        var serviceManager = new FakeServiceManager();
        var workflow = CreateWorkflow(
            detector,
            installer,
            configurator,
            validator,
            serviceManager: serviceManager);

        var result = await workflow.ExecuteAsync(Configuration());

        var value = Assert.IsType<RustDeskSetupWorkflowResult>(result.Value);
        Assert.True(result.Success);
        Assert.False(value.InstallationPerformed);
        Assert.Equal(0, installer.CallCount);
        Assert.Equal(1, serviceManager.EnsureInstalledCallCount);
        Assert.Equal(1, serviceManager.EnsureRunningCallCount);
        Assert.Equal(1, configurator.CallCount);
        Assert.Equal(1, validator.CallCount);
    }

    [Fact]
    public async Task ReportsConfigurationProgressWithoutInstallationWhenRustDeskExists()
    {
        var installer = new FakeInstaller();
        var processLauncher = new FakeServiceInstallProcessLauncher();
        var serviceManager = CreateWindowsServiceManager(
            processLauncher,
            new FakeWindowsServiceController(
                RustDeskServiceStatus.Running,
                RustDeskServiceStatus.Running));
        var workflow = CreateWorkflow(
            new SequenceDetector(Found()),
            installer,
            new FakeConfigurator(),
            new FakeValidator(),
            serviceManager: serviceManager);
        var progress = new RecordingProgress();

        var result = await workflow.ExecuteAsync(Configuration(), progress: progress);

        Assert.True(result.Success);
        Assert.Equal(0, installer.CallCount);
        Assert.Equal(0, processLauncher.CallCount);
        Assert.Equal(
            [
                SetupProgressStage.Checking,
                SetupProgressStage.StartingService,
                SetupProgressStage.Configuring,
                SetupProgressStage.Verifying,
                SetupProgressStage.Completed
            ],
            progress.Stages);
    }

    [Fact]
    public async Task ContinuesAllProgressWhenServiceAppearsWhileHelperRemainsActive()
    {
        var processLauncher = new FakeServiceInstallProcessLauncher();
        var serviceManager = CreateWindowsServiceManager(
            processLauncher,
            new FakeWindowsServiceController(
                RustDeskServiceStatus.NotInstalled,
                RustDeskServiceStatus.NotInstalled,
                RustDeskServiceStatus.Running,
                RustDeskServiceStatus.Running));
        var workflow = CreateWorkflow(
            new SequenceDetector(Found()),
            new FakeInstaller(),
            new FakeConfigurator(),
            new FakeValidator(),
            serviceManager: serviceManager);
        var progress = new RecordingProgress();

        var result = await workflow.ExecuteAsync(Configuration(), progress: progress);

        Assert.True(result.Success);
        Assert.Equal(1, processLauncher.CallCount);
        Assert.Equal(1, processLauncher.Process.TerminateCallCount);
        Assert.Equal(
            [
                SetupProgressStage.Checking,
                SetupProgressStage.StartingService,
                SetupProgressStage.Configuring,
                SetupProgressStage.Verifying,
                SetupProgressStage.Completed
            ],
            progress.Stages);
    }

    [Fact]
    public async Task ReportsFailureWhenServiceNeverReachesRunningState()
    {
        var serviceManager = new FakeServiceManager
        {
            EnsureRunningResult = OperationResult.Failed(
                ErrorCode.InstallationFailed,
                "Service did not reach Running.")
        };
        var workflow = CreateWorkflow(
            new SequenceDetector(Found()),
            new FakeInstaller(),
            new FakeConfigurator(),
            new FakeValidator(),
            serviceManager: serviceManager);
        var progress = new RecordingProgress();

        var result = await workflow.ExecuteAsync(Configuration(), progress: progress);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InstallationFailed, result.ErrorCode);
        Assert.Equal(
            [
                SetupProgressStage.Checking,
                SetupProgressStage.StartingService,
                SetupProgressStage.Failed
            ],
            progress.Stages);
    }

    [Fact]
    public async Task InstallsRedetectsConfiguresAndValidatesInOrder()
    {
        var detector = new SequenceDetector(NotFound(), Found());
        var installer = new FakeInstaller();
        var configurator = new FakeConfigurator();
        var validator = new FakeValidator();
        var serviceManager = new FakeServiceManager();
        var workflow = CreateWorkflow(
            detector,
            installer,
            configurator,
            validator,
            serviceManager: serviceManager);

        var result = await workflow.ExecuteAsync(Configuration());

        var value = Assert.IsType<RustDeskSetupWorkflowResult>(result.Value);
        Assert.True(result.Success);
        Assert.True(value.InstallationPerformed);
        Assert.Equal(2, detector.CallCount);
        Assert.Equal(1, installer.CallCount);
        Assert.Equal(1, serviceManager.EnsureInstalledCallCount);
        Assert.Equal(1, serviceManager.EnsureRunningCallCount);
        Assert.Equal(1, configurator.CallCount);
        Assert.Equal(1, validator.CallCount);
    }

    [Fact]
    public async Task ReportsInstallationAndConfigurationProgressInOrder()
    {
        var workflow = CreateWorkflow(
            new SequenceDetector(NotFound(), Found()),
            new FakeInstaller(),
            new FakeConfigurator(),
            new FakeValidator());
        var progress = new RecordingProgress();

        var result = await workflow.ExecuteAsync(Configuration(), progress: progress);

        Assert.True(result.Success);
        Assert.Equal(
            [
                SetupProgressStage.Checking,
                SetupProgressStage.Downloading,
                SetupProgressStage.Installing,
                SetupProgressStage.Checking,
                SetupProgressStage.StartingService,
                SetupProgressStage.Configuring,
                SetupProgressStage.Verifying,
                SetupProgressStage.Completed
            ],
            progress.Stages);
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

    [Fact]
    public async Task FullSetupGuardFailsInsteadOfWaitingIndefinitely()
    {
        var workflow = CreateWorkflow(
            new HangingDetector(),
            new FakeInstaller(),
            new FakeConfigurator(),
            new FakeValidator(),
            setupTimeout: TimeSpan.FromMilliseconds(25));
        var progress = new RecordingProgress();

        var result = await workflow.ExecuteAsync(Configuration(), progress: progress);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.UnexpectedFailure, result.ErrorCode);
        Assert.Equal(
            [SetupProgressStage.Checking, SetupProgressStage.Failed],
            progress.Stages);
    }

    private static RustDeskSetupWorkflow CreateWorkflow(
        IRustDeskDetector detector,
        IRustDeskInstaller installer,
        IRustDeskConfigurator configurator,
        IRustDeskValidator validator,
        int redetectionAttempts = 2,
        IRustDeskServiceManager? serviceManager = null,
        TimeSpan? setupTimeout = null)
    {
        var platform = new StubPlatformEnvironment();
        var logger = new InMemoryLogger();
        var configurationWorkflow = new RustDeskConfigurationWorkflow(
            detector,
            serviceManager ?? new FakeServiceManager(),
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
            (_, _) => Task.CompletedTask,
            setupTimeout);
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

    private static WindowsRustDeskServiceManager CreateWindowsServiceManager(
        IServiceInstallProcessLauncher processLauncher,
        IWindowsServiceController serviceController) =>
        new(
            processLauncher,
            serviceController,
            new InMemoryLogger(),
            pollingAttempts: 5,
            pollingDelay: TimeSpan.Zero,
            delay: (_, _) => Task.CompletedTask,
            serviceInstallationTimeout: TimeSpan.FromSeconds(5),
            serviceStartTimeout: TimeSpan.FromSeconds(5));

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

    private sealed class HangingDetector : IRustDeskDetector
    {
        public async Task<OperationResult<RustDeskInstallation>> DetectAsync(
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return NotFound();
        }
    }

    private sealed class FakeServiceInstallProcessLauncher
        : IServiceInstallProcessLauncher
    {
        public FakeServiceInstallProcess Process { get; } = new();

        public int CallCount { get; private set; }

        public OperationResult<IServiceInstallProcess> Start(ProcessRequest request)
        {
            CallCount++;
            return OperationResult<IServiceInstallProcess>.Succeeded(Process, "Started.");
        }
    }

    private sealed class FakeServiceInstallProcess : IServiceInstallProcess
    {
        public bool HasExited { get; private set; }

        public int? ExitCode => HasExited ? 0 : null;

        public int TerminateCallCount { get; private set; }

        public Task<OperationResult> TerminateAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            TerminateCallCount++;
            HasExited = true;
            return Task.FromResult(OperationResult.Succeeded("Terminated."));
        }

        public ValueTask DisposeAsync()
        {
            HasExited = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeWindowsServiceController(
        params RustDeskServiceStatus[] statuses) : IWindowsServiceController
    {
        private readonly Queue<RustDeskServiceStatus> _statuses = new(statuses);
        private RustDeskServiceStatus _lastStatus = statuses.LastOrDefault();

        public RustDeskServiceStatus GetStatus(string serviceName)
        {
            Assert.Equal(WindowsRustDeskServiceManager.ServiceName, serviceName);

            if (_statuses.Count > 0)
            {
                _lastStatus = _statuses.Dequeue();
            }

            return _lastStatus;
        }

        public void Start(string serviceName) =>
            Assert.Equal(WindowsRustDeskServiceManager.ServiceName, serviceName);

        public void Continue(string serviceName) =>
            Assert.Equal(WindowsRustDeskServiceManager.ServiceName, serviceName);
    }

    private sealed class FakeInstaller : IRustDeskInstaller
    {
        public OperationResult Result { get; init; } =
            OperationResult.Succeeded("Installed.");
        public int CallCount { get; private set; }

        public Task<OperationResult> InstallAsync(
            CancellationToken cancellationToken = default,
            IProgress<SetupProgress>? progress = null)
        {
            CallCount++;
            progress?.Report(new SetupProgress(SetupProgressStage.Downloading));
            progress?.Report(new SetupProgress(SetupProgressStage.Installing));
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
                        RustDeskValidationStatus.Verified,
                        "Service and options matched."),
                    "Validated."));
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
        public PlatformInfo Current { get; } = Platform();
    }

    private sealed class InMemoryLogger : IAppLogger
    {
        public string Destination => "memory://setup.log";
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, string? technicalDetails = null) { }
    }

    private sealed class RecordingProgress : IProgress<SetupProgress>
    {
        public List<SetupProgressStage> Stages { get; } = [];

        public void Report(SetupProgress value) => Stages.Add(value.Stage);
    }
}
