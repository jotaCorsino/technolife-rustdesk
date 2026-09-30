using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Windows;

namespace Technolife.RustDesk.Tests.Platforms.Windows;

public sealed class WindowsRustDeskOptionReaderTests
{
    [Fact]
    public async Task ReadsOptionWithSeparatedElevatedArguments()
    {
        var processRunner = new FakeProcessRunner();
        var reader = new WindowsRustDeskOptionReader(processRunner);

        var result = await reader.ReadAsync(
            CreateInstallation(),
            WindowsRustDeskOptions.IdServer);

        Assert.True(result.Success);
        Assert.Equal("remoto.technolife.net.br", result.Value);
        Assert.NotNull(processRunner.Request);
        Assert.Equal(
            ["--option", WindowsRustDeskOptions.IdServer],
            processRunner.Request.Arguments);
        Assert.True(processRunner.Request.RequiresElevation);
    }

    private static RustDeskInstallation CreateInstallation() =>
        RustDeskInstallation.CreateFound(
            @"C:\Program Files\RustDesk\RustDesk.exe",
            new Version(1, 4, 9),
            new PlatformInfo(PlatformKind.Windows, CpuArchitecture.X64));

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public ProcessRequest? Request { get; private set; }

        public Task<OperationResult<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(
                OperationResult<ProcessResult>.Succeeded(
                    new ProcessResult(0, "remoto.technolife.net.br\r\n", string.Empty),
                    "Completed."));
        }
    }
}
