using Technolife.RustDesk.Cli;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Tests.Cli;

public sealed class CliTests
{
    private const string SensitiveConfiguration = "sensitive-exported-configuration";

    [Theory]
    [InlineData(ErrorCode.None, CliExitCode.Success)]
    [InlineData(ErrorCode.RustDeskNotFound, CliExitCode.RustDeskNotFound)]
    [InlineData(ErrorCode.ConfigurationFailed, CliExitCode.InvalidConfiguration)]
    [InlineData(ErrorCode.ProcessFailed, CliExitCode.ProcessFailed)]
    [InlineData(ErrorCode.ValidationFailed, CliExitCode.ValidationFailed)]
    [InlineData(ErrorCode.UnsupportedPlatform, CliExitCode.UnsupportedPlatform)]
    [InlineData(ErrorCode.DetectionFailed, CliExitCode.GeneralError)]
    public void MapsStableExitCodes(ErrorCode errorCode, CliExitCode expected)
    {
        Assert.Equal(expected, CliExitCodeMapper.FromErrorCode(errorCode));
    }

    [Fact]
    public async Task NoArgumentsOnlyShowsHelp()
    {
        using var output = new StringWriter();

        var exitCode = await CliApplication.RunAsync([], output);

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Contains("Nenhuma configuração é alterada", output.ToString());
    }

    [Fact]
    public void FailureOutputDoesNotExposeResultDetails()
    {
        using var output = new StringWriter();
        var result = OperationResult<RustDeskConfigurationWorkflowResult>.Failed(
            ErrorCode.ProcessFailed,
            $"Failed for {SensitiveConfiguration}.",
            $"Technical details for {SensitiveConfiguration}.");

        CliApplication.WriteConfigurationResult(
            output,
            result,
            @"C:\logs\technolife.log");

        Assert.DoesNotContain(SensitiveConfiguration, output.ToString());
        Assert.Contains("Não foi possível aplicar", output.ToString());
    }
}
