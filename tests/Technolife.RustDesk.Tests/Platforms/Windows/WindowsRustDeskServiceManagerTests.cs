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
        var processRunner = new FakeProcessRunner();
        var serviceController = new FakeWindowsServiceController(
            RustDeskServiceStatus.NotInstalled,
            RustDeskServiceStatus.NotInstalled,
            RustDeskServiceStatus.Stopped);
        var manager = CreateManager(processRunner, serviceController);

        var result = await manager.EnsureInstalledAsync(CreateInstallation());

        Assert.True(result.Success);
        Assert.NotNull(processRunner.Request);
        Assert.Equal(["--install-service"], processRunner.Request.Arguments);
        Assert.True(processRunner.Request.RequiresElevation);
        Assert.Equal(WindowsRustDeskServiceManager.ServiceName, "RustDesk");
    }

    [Fact]
    public async Task DoesNotReinstallExistingService()
    {
        var processRunner = new FakeProcessRunner();
        var manager = CreateManager(
            processRunner,
            new FakeWindowsServiceController(RustDeskServiceStatus.Running));

        var result = await manager.EnsureInstalledAsync(CreateInstallation());

        Assert.True(result.Success);
        Assert.Equal(0, processRunner.CallCount);
    }

    [Fact]
    public async Task StartsStoppedServiceAndWaitsUntilRunning()
    {
        var serviceController = new FakeWindowsServiceController(
            RustDeskServiceStatus.Stopped,
            RustDeskServiceStatus.StartPending,
            RustDeskServiceStatus.Running);
        var manager = CreateManager(new FakeProcessRunner(), serviceController);

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
        var manager = CreateManager(new FakeProcessRunner(), serviceController);

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
            new FakeProcessRunner(),
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
        var processRunner = new FakeProcessRunner
        {
            Result = OperationResult<ProcessResult>.Failed(
                ErrorCode.ElevationFailed,
                "Elevation declined.")
        };
        var manager = CreateManager(
            processRunner,
            new FakeWindowsServiceController(RustDeskServiceStatus.NotInstalled));

        var result = await manager.EnsureInstalledAsync(CreateInstallation());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ElevationFailed, result.ErrorCode);
        Assert.Contains("elevation", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepeatedReadyChecksAreIdempotent()
    {
        var processRunner = new FakeProcessRunner();
        var serviceController = new FakeWindowsServiceController(
            RustDeskServiceStatus.Running,
            RustDeskServiceStatus.Running,
            RustDeskServiceStatus.Running,
            RustDeskServiceStatus.Running);
        var manager = CreateManager(processRunner, serviceController);

        var firstInstall = await manager.EnsureInstalledAsync(CreateInstallation());
        var firstRun = await manager.EnsureRunningAsync();
        var secondInstall = await manager.EnsureInstalledAsync(CreateInstallation());
        var secondRun = await manager.EnsureRunningAsync();

        Assert.True(firstInstall.Success);
        Assert.True(firstRun.Success);
        Assert.True(secondInstall.Success);
        Assert.True(secondRun.Success);
        Assert.Equal(0, processRunner.CallCount);
        Assert.Equal(0, serviceController.StartCallCount);
        Assert.Equal(0, serviceController.ContinueCallCount);
    }

    private static WindowsRustDeskServiceManager CreateManager(
        IProcessRunner processRunner,
        IWindowsServiceController serviceController,
        int pollingAttempts = 4) =>
        new(
            processRunner,
            serviceController,
            new InMemoryLogger(),
            pollingAttempts,
            TimeSpan.Zero,
            (_, _) => Task.CompletedTask);

    private static RustDeskInstallation CreateInstallation() =>
        RustDeskInstallation.CreateFound(
            @"C:\Program Files\RustDesk\RustDesk.exe",
            new Version(1, 4, 9),
            new PlatformInfo(PlatformKind.Windows, CpuArchitecture.X64));

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public OperationResult<ProcessResult> Result { get; init; } =
            OperationResult<ProcessResult>.Succeeded(
                new ProcessResult(0, string.Empty, string.Empty),
                "Completed.");

        public int CallCount { get; private set; }

        public ProcessRequest? Request { get; private set; }

        public Task<OperationResult<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Request = request;
            return Task.FromResult(Result);
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
