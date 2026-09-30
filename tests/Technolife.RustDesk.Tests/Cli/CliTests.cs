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
    [InlineData(ErrorCode.DownloadFailed, CliExitCode.DownloadFailed)]
    [InlineData(ErrorCode.ChecksumMismatch, CliExitCode.ChecksumMismatch)]
    [InlineData(ErrorCode.InstallationFailed, CliExitCode.InstallationFailed)]
    [InlineData(ErrorCode.ElevationFailed, CliExitCode.ElevationFailed)]
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
        Assert.Contains("Somente os comandos explícitos", output.ToString());
        Assert.Contains("setup", output.ToString());
    }

    [Fact]
    public async Task VersionOptionOnlyShowsApplicationVersion()
    {
        using var output = new StringWriter();

        var exitCode = await CliApplication.RunAsync(["--version"], output);

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Contains("Versão: 0.1.0-dev", output.ToString());
        Assert.DoesNotContain("Uso:", output.ToString());
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

    [Theory]
    [InlineData(ErrorCode.DownloadFailed, "baixar")]
    [InlineData(ErrorCode.ChecksumMismatch, "integridade")]
    [InlineData(ErrorCode.InstallationFailed, "instalar")]
    [InlineData(ErrorCode.ElevationFailed, "elevação")]
    public void SetupFailureOutputIsFriendlyAndDoesNotExposeDetails(
        ErrorCode errorCode,
        string expectedText)
    {
        using var output = new StringWriter();
        var result = OperationResult<RustDeskSetupWorkflowResult>.Failed(
            errorCode,
            $"Failed for {SensitiveConfiguration}.",
            $"Technical details for {SensitiveConfiguration}.");

        CliApplication.WriteSetupResult(output, result, @"C:\logs\technolife.log");

        Assert.DoesNotContain(SensitiveConfiguration, output.ToString());
        Assert.Contains(expectedText, output.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
