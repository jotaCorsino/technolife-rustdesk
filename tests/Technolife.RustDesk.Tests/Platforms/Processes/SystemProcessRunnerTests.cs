using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Processes;

namespace Technolife.RustDesk.Tests.Platforms.Processes;

public sealed class SystemProcessRunnerTests
{
    [Fact]
    public async Task CapturesExitCodeAndStandardOutput()
    {
        var runner = new SystemProcessRunner();
        var request = new ProcessRequest(
            "dotnet",
            ["--version"],
            timeout: TimeSpan.FromSeconds(30));

        var result = await runner.RunAsync(request);

        var processResult = Assert.IsType<ProcessResult>(result.Value);
        Assert.True(result.Success);
        Assert.Equal(0, processResult.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(processResult.StandardOutput));
        Assert.Equal(string.Empty, processResult.StandardError);
    }

    [Fact]
    public async Task ReturnsStructuredFailureWhenExecutableCannotStart()
    {
        var runner = new SystemProcessRunner();
        var request = new ProcessRequest(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.exe"));

        var result = await runner.RunAsync(request);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(ErrorCode.ProcessFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ReturnsStructuredFailureOnTimeout()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var runner = new SystemProcessRunner();
        var request = new ProcessRequest(
            "ping.exe",
            ["127.0.0.1", "-n", "6"],
            timeout: TimeSpan.FromMilliseconds(50));

        var result = await runner.RunAsync(request);

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(ErrorCode.ProcessFailed, result.ErrorCode);
        Assert.Contains("timed out", result.TechnicalDetails);
    }
}
