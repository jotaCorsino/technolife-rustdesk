using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Tests.Core;

public sealed class ProcessModelsTests
{
    [Fact]
    public void KeepsExecutableAndArgumentsSeparated()
    {
        var arguments = new List<string> { "--config", "value with spaces" };

        var request = new ProcessRequest(
            "rustdesk.exe",
            arguments,
            "C:\\test",
            TimeSpan.FromSeconds(30));
        arguments[1] = "changed";

        Assert.Equal("rustdesk.exe", request.Executable);
        Assert.Equal(new[] { "--config", "value with spaces" }, request.Arguments);
        Assert.Equal("C:\\test", request.WorkingDirectory);
        Assert.Equal(TimeSpan.FromSeconds(30), request.Timeout);
    }

    [Fact]
    public void RejectsNonPositiveTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProcessRequest("rustdesk.exe", timeout: TimeSpan.Zero));
    }

    [Fact]
    public void CapturesProcessExitCodeAndStreams()
    {
        var result = new ProcessResult(5, "standard output", "standard error");

        Assert.Equal(5, result.ExitCode);
        Assert.Equal("standard output", result.StandardOutput);
        Assert.Equal("standard error", result.StandardError);
    }
}
