using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Abstractions;
using Technolife.RustDesk.Platforms.Windows;

namespace Technolife.RustDesk.Tests.Platforms.Windows;

public sealed class WindowsRustDeskValidatorTests
{
    private const string ExecutablePath = @"C:\Program Files\RustDesk\RustDesk.exe";

    [Fact]
    public async Task ReturnsAppliedWithoutClaimingFullVerification()
    {
        var validator = new WindowsRustDeskValidator(new FakeFileProbe(fileExists: true));

        var result = await validator.ValidateAsync(
            CreateInstallation(),
            CreateConfiguration());

        var validation = Assert.IsType<RustDeskValidation>(result.Value);
        Assert.True(result.Success);
        Assert.Equal(RustDeskValidationStatus.Applied, validation.Status);
        Assert.NotEqual(RustDeskValidationStatus.Verified, validation.Status);
        Assert.Contains("not independently", validation.Evidence);
    }

    [Fact]
    public async Task FailsWhenExecutableIsNoLongerAccessible()
    {
        var validator = new WindowsRustDeskValidator(new FakeFileProbe(fileExists: false));

        var result = await validator.ValidateAsync(
            CreateInstallation(),
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task RejectsMissingInstallation()
    {
        var validator = new WindowsRustDeskValidator(new FakeFileProbe(fileExists: true));

        var result = await validator.ValidateAsync(
            RustDeskInstallation.CreateNotFound(CreatePlatform()),
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.RustDeskNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task RejectsNonWindowsInstallation()
    {
        var validator = new WindowsRustDeskValidator(new FakeFileProbe(fileExists: true));
        var installation = RustDeskInstallation.CreateFound(
            "/usr/bin/rustdesk",
            null,
            new PlatformInfo(PlatformKind.Linux, CpuArchitecture.X64));

        var result = await validator.ValidateAsync(
            installation,
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.UnsupportedPlatform, result.ErrorCode);
    }

    [Fact]
    public async Task ConvertsProbeFailureToStructuredValidationFailure()
    {
        var validator = new WindowsRustDeskValidator(
            new FakeFileProbe(new IOException("Unavailable.")));

        var result = await validator.ValidateAsync(
            CreateInstallation(),
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains(nameof(IOException), result.TechnicalDetails);
    }

    private static RustDeskInstallation CreateInstallation() =>
        RustDeskInstallation.CreateFound(
            ExecutablePath,
            new Version(1, 4, 9),
            CreatePlatform());

    private static PlatformInfo CreatePlatform() =>
        new(PlatformKind.Windows, CpuArchitecture.X64);

    private static RustDeskConfiguration CreateConfiguration() =>
        new(
            "id.example.test",
            "relay.example.test",
            "public-key",
            "exported-configuration");

    private sealed class FakeFileProbe : IFileProbe
    {
        private readonly bool _fileExists;
        private readonly Exception? _exception;

        public FakeFileProbe(bool fileExists)
        {
            _fileExists = fileExists;
        }

        public FakeFileProbe(Exception exception)
        {
            _exception = exception;
        }

        public bool FileExists(string path)
        {
            if (_exception is not null)
            {
                throw _exception;
            }

            return _fileExists;
        }

        public Version? ReadVersion(string path) => null;
    }
}
