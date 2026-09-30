using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Windows;

namespace Technolife.RustDesk.Tests.Platforms.Windows;

public sealed class WindowsRustDeskConfiguratorTests
{
    private const string ExecutablePath = @"C:\Program Files\RustDesk\RustDesk.exe";
    private const string SensitiveConfiguration = "sensitive-exported-configuration";

    [Fact]
    public async Task AppliesValidConfigurationWithSeparatedArguments()
    {
        var processRunner = new FakeProcessRunner();
        var configurator = new WindowsRustDeskConfigurator(processRunner);

        var result = await configurator.ConfigureAsync(
            CreateFoundInstallation(),
            CreateConfiguration());

        Assert.True(result.Success);
        Assert.Equal(1, processRunner.CallCount);
        Assert.NotNull(processRunner.Request);
        Assert.Equal(ExecutablePath, processRunner.Request.Executable);
        Assert.Equal(
            new[] { "--config", SensitiveConfiguration },
            processRunner.Request.Arguments);
        Assert.Equal(@"C:\Program Files\RustDesk", processRunner.Request.WorkingDirectory);
        Assert.True(processRunner.Request.Timeout > TimeSpan.Zero);
    }

    [Fact]
    public async Task DoesNotRunWhenRustDeskWasNotFound()
    {
        var processRunner = new FakeProcessRunner();
        var configurator = new WindowsRustDeskConfigurator(processRunner);
        var installation = RustDeskInstallation.CreateNotFound(CreateWindowsPlatform());

        var result = await configurator.ConfigureAsync(
            installation,
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.RustDeskNotFound, result.ErrorCode);
        Assert.Equal(0, processRunner.CallCount);
    }

    [Fact]
    public async Task DoesNotRunWhenExportedConfigurationIsEmpty()
    {
        var processRunner = new FakeProcessRunner();
        var configurator = new WindowsRustDeskConfigurator(processRunner);
        var configuration = new RustDeskConfiguration(
            "id.example.test",
            "relay.example.test",
            "public-key",
            "   ");

        var result = await configurator.ConfigureAsync(
            CreateFoundInstallation(),
            configuration);

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ConfigurationFailed, result.ErrorCode);
        Assert.Equal(0, processRunner.CallCount);
    }

    [Fact]
    public async Task DoesNotRunForNonWindowsInstallation()
    {
        var processRunner = new FakeProcessRunner();
        var configurator = new WindowsRustDeskConfigurator(processRunner);
        var installation = RustDeskInstallation.CreateFound(
            "/usr/bin/rustdesk",
            new Version(1, 4, 9),
            new PlatformInfo(PlatformKind.Linux, CpuArchitecture.X64));

        var result = await configurator.ConfigureAsync(
            installation,
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.UnsupportedPlatform, result.ErrorCode);
        Assert.Equal(0, processRunner.CallCount);
    }

    [Fact]
    public async Task ReturnsStructuredFailureWhenProcessRunnerFails()
    {
        var processRunner = new FakeProcessRunner
        {
            Result = OperationResult<ProcessResult>.Failed(
                ErrorCode.ProcessFailed,
                $"Could not run {SensitiveConfiguration}.",
                $"Failure while processing {SensitiveConfiguration}.")
        };
        var configurator = new WindowsRustDeskConfigurator(processRunner);

        var result = await configurator.ConfigureAsync(
            CreateFoundInstallation(),
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ProcessFailed, result.ErrorCode);
        AssertRedacted(result);
    }

    [Fact]
    public async Task ReturnsExitCodeWithoutProcessOutputOnNonZeroExit()
    {
        var processRunner = new FakeProcessRunner
        {
            Result = OperationResult<ProcessResult>.Succeeded(
                new ProcessResult(7, SensitiveConfiguration, SensitiveConfiguration),
                $"Completed {SensitiveConfiguration}.")
        };
        var configurator = new WindowsRustDeskConfigurator(processRunner);

        var result = await configurator.ConfigureAsync(
            CreateFoundInstallation(),
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ProcessFailed, result.ErrorCode);
        Assert.Contains("7", result.TechnicalDetails);
        AssertRedacted(result);
    }

    [Fact]
    public async Task ReturnsStructuredFailureWhenProcessTimesOut()
    {
        var processRunner = new FakeProcessRunner
        {
            Result = OperationResult<ProcessResult>.Failed(
                ErrorCode.ProcessFailed,
                "The process timed out.",
                $"Timeout while processing {SensitiveConfiguration}.")
        };
        var configurator = new WindowsRustDeskConfigurator(processRunner);

        var result = await configurator.ConfigureAsync(
            CreateFoundInstallation(),
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ProcessFailed, result.ErrorCode);
        AssertRedacted(result);
    }

    [Fact]
    public async Task KeepsSensitiveConfigurationOutOfResultAndTextRepresentations()
    {
        var processRunner = new FakeProcessRunner();
        var configurator = new WindowsRustDeskConfigurator(processRunner);
        var configuration = CreateConfiguration();

        var result = await configurator.ConfigureAsync(
            CreateFoundInstallation(),
            configuration);

        AssertRedacted(result);
        Assert.DoesNotContain(SensitiveConfiguration, configuration.ToString());
        Assert.DoesNotContain(SensitiveConfiguration, processRunner.Request!.ToString());
    }

    private static RustDeskInstallation CreateFoundInstallation() =>
        RustDeskInstallation.CreateFound(
            ExecutablePath,
            new Version(1, 4, 9),
            CreateWindowsPlatform());

    private static PlatformInfo CreateWindowsPlatform() =>
        new(PlatformKind.Windows, CpuArchitecture.X64);

    private static RustDeskConfiguration CreateConfiguration() =>
        new(
            "id.example.test",
            "relay.example.test",
            "public-key",
            SensitiveConfiguration);

    private static void AssertRedacted(OperationResult result)
    {
        Assert.DoesNotContain(SensitiveConfiguration, result.Message);
        Assert.DoesNotContain(
            SensitiveConfiguration,
            result.TechnicalDetails ?? string.Empty);
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public OperationResult<ProcessResult> Result { get; init; } =
            OperationResult<ProcessResult>.Succeeded(
                new ProcessResult(0, string.Empty, string.Empty),
                "Process completed.");

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
}
