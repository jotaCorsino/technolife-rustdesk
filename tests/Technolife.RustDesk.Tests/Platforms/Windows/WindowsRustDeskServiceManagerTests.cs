using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Abstractions;
using Technolife.RustDesk.Platforms.Windows;

namespace Technolife.RustDesk.Tests.Platforms.Windows;

public sealed class WindowsRustDeskServiceManagerTests
{
    [Fact]
    public async Task InstallsMissingServiceWithElevatedOfficialCommand()
    {
        var processLauncher = new FakeServiceInstallProcessLauncher();
        var serviceController = new FakeWindowsServiceController(
            RustDeskServiceStatus.NotInstalled,
            RustDeskServiceStatus.NotInstalled,
            RustDeskServiceStatus.Stopped);
        var manager = CreateManager(processLauncher, serviceController);

        var result = await manager.EnsureInstalledAsync(CreateInstallation());

        Assert.True(result.Success);
        Assert.NotNull(processLauncher.Request);
        Assert.Equal(["--install-service"], processLauncher.Request.Arguments);
        Assert.True(processLauncher.Request.RequiresElevation);
        Assert.Equal(1, processLauncher.Process.TerminateCallCount);
        Assert.Equal(WindowsRustDeskServiceManager.ServiceName, "RustDesk");
    }

    [Fact]
    public async Task DoesNotReinstallExistingService()
    {
        var processLauncher = new FakeServiceInstallProcessLauncher();
        var manager = CreateManager(
            processLauncher,
            new FakeWindowsServiceController(RustDeskServiceStatus.Running));

        var result = await manager.EnsureInstalledAsync(CreateInstallation());

        Assert.True(result.Success);
        Assert.Equal(0, processLauncher.CallCount);
    }

    [Fact]
    public async Task StartsStoppedServiceAndWaitsUntilRunning()
    {
        var serviceController = new FakeWindowsServiceController(
            RustDeskServiceStatus.Stopped,
            RustDeskServiceStatus.StartPending,
            RustDeskServiceStatus.Running);
        var manager = CreateManager(
            new FakeServiceInstallProcessLauncher(),
            serviceController);

        var result = await manager.EnsureRunningAsync();

        Assert.True(result.Success);
        Assert.Equal(1, serviceController.StartCallCount);
        Assert.Equal(0, serviceController.ContinueCallCount);
    }

    [Fact]
    public async Task ContinuesPausedServiceAndWaitsUntilRunning()
    {
        var serviceController = new FakeWindowsServiceController(
            RustDeskServiceStatus.Paused,
            RustDeskServiceStatus.ContinuePending,
            RustDeskServiceStatus.Running);
        var manager = CreateManager(
            new FakeServiceInstallProcessLauncher(),
            serviceController);

        var result = await manager.EnsureRunningAsync();

        Assert.True(result.Success);
        Assert.Equal(0, serviceController.StartCallCount);
        Assert.Equal(1, serviceController.ContinueCallCount);
    }

    [Fact]
    public async Task FailsWhenServiceDoesNotReachRunningState()
    {
        var serviceController = new FakeWindowsServiceController(
            RustDeskServiceStatus.Stopped,
            RustDeskServiceStatus.Stopped,
            RustDeskServiceStatus.Stopped);
        var manager = CreateManager(
            new FakeServiceInstallProcessLauncher(),
            serviceController,
            pollingAttempts: 3);

        var result = await manager.EnsureRunningAsync();

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InstallationFailed, result.ErrorCode);
        Assert.Equal(1, serviceController.StartCallCount);
    }

    [Fact]
    public async Task ReturnsFriendlyElevationFailureWhenServiceInstallIsDeclined()
    {
        var processLauncher = new FakeServiceInstallProcessLauncher
        {
            StartResult = OperationResult<IServiceInstallProcess>.Failed(
                ErrorCode.ElevationFailed,
                "Elevation declined.")
        };
        var manager = CreateManager(
            processLauncher,
            new FakeWindowsServiceController(RustDeskServiceStatus.NotInstalled));

        var result = await manager.EnsureInstalledAsync(CreateInstallation());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ElevationFailed, result.ErrorCode);
        Assert.Contains("elevation", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepeatedReadyChecksAreIdempotent()
    {
        var processLauncher = new FakeServiceInstallProcessLauncher();
        var serviceController = new FakeWindowsServiceController(
            RustDeskServiceStatus.Running,
            RustDeskServiceStatus.Running,
            RustDeskServiceStatus.Running,
            RustDeskServiceStatus.Running);
        var manager = CreateManager(processLauncher, serviceController);

        var firstInstall = await manager.EnsureInstalledAsync(CreateInstallation());
        var firstRun = await manager.EnsureRunningAsync();
        var secondInstall = await manager.EnsureInstalledAsync(CreateInstallation());
        var secondRun = await manager.EnsureRunningAsync();

        Assert.True(firstInstall.Success);
        Assert.True(firstRun.Success);
        Assert.True(secondInstall.Success);
        Assert.True(secondRun.Success);
        Assert.Equal(0, processLauncher.CallCount);
        Assert.Equal(0, serviceController.StartCallCount);
        Assert.Equal(0, serviceController.ContinueCallCount);
    }

    [Fact]
    public async Task ContinuesToRunningWhenInstallHelperRemainsActiveButScmReportsService()
    {
        var processLauncher = new FakeServiceInstallProcessLauncher();
        var serviceController = new FakeWindowsServiceController(
            RustDeskServiceStatus.NotInstalled,
            RustDeskServiceStatus.NotInstalled,
            RustDeskServiceStatus.Stopped,
            RustDeskServiceStatus.Stopped,
            RustDeskServiceStatus.Running);
        var manager = CreateManager(processLauncher, serviceController);

        var installationResult = await manager.EnsureInstalledAsync(CreateInstallation());
        var runningResult = await manager.EnsureRunningAsync();

        Assert.True(installationResult.Success);
        Assert.True(runningResult.Success);
        Assert.Equal(1, processLauncher.CallCount);
        Assert.Equal(1, processLauncher.Process.TerminateCallCount);
        Assert.True(processLauncher.Process.HasExited);
        Assert.Equal(1, serviceController.StartCallCount);
    }

    [Fact]
    public async Task FailsAndTerminatesHelperWhenServiceNeverAppears()
    {
        var processLauncher = new FakeServiceInstallProcessLauncher();
        var serviceController = new FakeWindowsServiceController(
            RustDeskServiceStatus.NotInstalled);
        var manager = CreateManager(
            processLauncher,
            serviceController,
            pollingAttempts: 3);

        var result = await manager.EnsureInstalledAsync(CreateInstallation());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InstallationFailed, result.ErrorCode);
        Assert.True(processLauncher.Process.HasExited);
        Assert.Equal(1, processLauncher.Process.TerminateCallCount);
    }

    [Fact]
    public async Task InstallationTimeoutFailsWhenScmNeverReportsService()
    {
        var processLauncher = new FakeServiceInstallProcessLauncher();
        var manager = new WindowsRustDeskServiceManager(
            processLauncher,
            new FakeWindowsServiceController(RustDeskServiceStatus.NotInstalled),
            new InMemoryLogger(),
            pollingAttempts: 30,
            pollingDelay: TimeSpan.FromMinutes(1),
            delay: (delay, token) => Task.Delay(delay, token),
            serviceInstallationTimeout: TimeSpan.FromMilliseconds(100),
            serviceStartTimeout: TimeSpan.FromSeconds(5));

        var result = await manager.EnsureInstalledAsync(CreateInstallation());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InstallationFailed, result.ErrorCode);
        Assert.Contains("timed out", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(processLauncher.Process.HasExited);
    }

    [Fact]
    public async Task StartTimeoutFailsWhenServiceNeverReachesRunning()
    {
        var manager = new WindowsRustDeskServiceManager(
            new FakeServiceInstallProcessLauncher(),
            new FakeWindowsServiceController(RustDeskServiceStatus.StartPending),
            new InMemoryLogger(),
            pollingAttempts: 30,
            pollingDelay: TimeSpan.FromMinutes(1),
            delay: (delay, token) => Task.Delay(delay, token),
            serviceInstallationTimeout: TimeSpan.FromSeconds(5),
            serviceStartTimeout: TimeSpan.FromMilliseconds(100));

        var result = await manager.EnsureRunningAsync();

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InstallationFailed, result.ErrorCode);
        Assert.Contains("in time", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TrustsScmWhenHelperCannotBeTerminatedCleanly()
    {
        var processLauncher = new FakeServiceInstallProcessLauncher();
        processLauncher.Process.TerminationResult = OperationResult.Failed(
            ErrorCode.ProcessFailed,
            "Helper remained active.");
        var manager = CreateManager(
            processLauncher,
            new FakeWindowsServiceController(
                RustDeskServiceStatus.NotInstalled,
                RustDeskServiceStatus.Running));

        var result = await manager.EnsureInstalledAsync(CreateInstallation());

        Assert.True(result.Success);
        Assert.True(processLauncher.Process.TerminateCallCount >= 1);
    }

    private static WindowsRustDeskServiceManager CreateManager(
        IServiceInstallProcessLauncher processLauncher,
        IWindowsServiceController serviceController,
        int pollingAttempts = 4) =>
        new(
            processLauncher,
            serviceController,
            new InMemoryLogger(),
            pollingAttempts,
            TimeSpan.Zero,
            (_, _) => Task.CompletedTask,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5));

    private static RustDeskInstallation CreateInstallation() =>
        RustDeskInstallation.CreateFound(
            @"C:\Program Files\RustDesk\RustDesk.exe",
            new Version(1, 4, 9),
            new PlatformInfo(PlatformKind.Windows, CpuArchitecture.X64));

    private sealed class FakeServiceInstallProcessLauncher
        : IServiceInstallProcessLauncher
    {
        public FakeServiceInstallProcess Process { get; } = new();

        public OperationResult<IServiceInstallProcess>? StartResult { get; init; }

        public int CallCount { get; private set; }

        public ProcessRequest? Request { get; private set; }

        public OperationResult<IServiceInstallProcess> Start(ProcessRequest request)
        {
            CallCount++;
            Request = request;
            return StartResult ?? OperationResult<IServiceInstallProcess>.Succeeded(
                Process,
                "Started.");
        }
    }

    private sealed class FakeServiceInstallProcess : IServiceInstallProcess
    {
        public bool HasExited { get; private set; }

        public int? ExitCode => HasExited ? 0 : null;

        public int TerminateCallCount { get; private set; }

        public OperationResult TerminationResult { get; set; } =
            OperationResult.Succeeded("Terminated.");

        public Task<OperationResult> TerminateAsync(
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            TerminateCallCount++;

            if (TerminationResult.Success)
            {
                HasExited = true;
            }

            return Task.FromResult(TerminationResult);
        }

        public ValueTask DisposeAsync()
        {
            if (!HasExited)
            {
                TerminateCallCount++;
                HasExited = true;
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeWindowsServiceController(
        params RustDeskServiceStatus[] statuses) : IWindowsServiceController
    {
        private readonly Queue<RustDeskServiceStatus> _statuses = new(statuses);
        private RustDeskServiceStatus _lastStatus = statuses.LastOrDefault();

        public int StartCallCount { get; private set; }

        public int ContinueCallCount { get; private set; }

        public RustDeskServiceStatus GetStatus(string serviceName)
        {
            Assert.Equal(WindowsRustDeskServiceManager.ServiceName, serviceName);

            if (_statuses.Count > 0)
            {
                _lastStatus = _statuses.Dequeue();
            }

            return _lastStatus;
        }

        public void Start(string serviceName)
        {
            Assert.Equal(WindowsRustDeskServiceManager.ServiceName, serviceName);
            StartCallCount++;
        }

        public void Continue(string serviceName)
        {
            Assert.Equal(WindowsRustDeskServiceManager.ServiceName, serviceName);
            ContinueCallCount++;
        }
    }

    private sealed class InMemoryLogger : IAppLogger
    {
        public string Destination => "memory://service.log";

        public void Info(string message) { }

        public void Warning(string message) { }

        public void Error(string message, string? technicalDetails = null) { }
    }
}
