using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Abstractions;
using Technolife.RustDesk.Platforms.Windows;

namespace Technolife.RustDesk.Tests.Platforms.Windows;

public sealed class WindowsRustDeskValidatorTests
{
    private const string ExecutablePath = @"C:\Program Files\RustDesk\RustDesk.exe";

    [Fact]
    public async Task ReturnsVerifiedWhenServiceAndAllOptionsMatch()
    {
        var optionReader = new FakeOptionReader(CreateExpectedOptions());
        var validator = CreateValidator(
            new FakeFileProbe(fileExists: true),
            optionReader: optionReader);

        var result = await validator.ValidateAsync(
            CreateInstallation(),
            CreateConfiguration());

        var validation = Assert.IsType<RustDeskValidation>(result.Value);
        Assert.True(result.Success);
        Assert.Equal(RustDeskValidationStatus.Verified, validation.Status);
        Assert.Contains("independently", validation.Evidence);
        Assert.Equal(
            [
                WindowsRustDeskOptions.IdServer,
                WindowsRustDeskOptions.RelayServer,
                WindowsRustDeskOptions.PublicKey
            ],
            optionReader.RequestedOptions);
    }

    [Fact]
    public async Task RejectsFalsePositiveWhenConfigProcessSucceededButOptionIsWrong()
    {
        var values = CreateExpectedOptions();
        values[WindowsRustDeskOptions.IdServer] = "wrong.example.test";
        var validator = CreateValidator(
            new FakeFileProbe(fileExists: true),
            optionReader: new FakeOptionReader(values));

        var result = await validator.ValidateAsync(
            CreateInstallation(),
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Contains(WindowsRustDeskOptions.IdServer, result.TechnicalDetails);
        Assert.DoesNotContain("wrong.example.test", result.TechnicalDetails);
    }

    [Fact]
    public async Task RejectsValidationWhenServiceIsNotRunning()
    {
        var optionReader = new FakeOptionReader(CreateExpectedOptions());
        var validator = CreateValidator(
            new FakeFileProbe(fileExists: true),
            serviceManager: new FakeServiceManager(RustDeskServiceStatus.Stopped),
            optionReader: optionReader);

        var result = await validator.ValidateAsync(
            CreateInstallation(),
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ValidationFailed, result.ErrorCode);
        Assert.Empty(optionReader.RequestedOptions);
    }

    [Fact]
    public async Task FailsWhenExecutableIsNoLongerAccessible()
    {
        var validator = CreateValidator(new FakeFileProbe(fileExists: false));

        var result = await validator.ValidateAsync(
            CreateInstallation(),
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task RejectsMissingInstallation()
    {
        var validator = CreateValidator(new FakeFileProbe(fileExists: true));

        var result = await validator.ValidateAsync(
            RustDeskInstallation.CreateNotFound(CreatePlatform()),
            CreateConfiguration());

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.RustDeskNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task RejectsNonWindowsInstallation()
    {
        var validator = CreateValidator(new FakeFileProbe(fileExists: true));
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
        var validator = CreateValidator(
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

    private static WindowsRustDeskValidator CreateValidator(
        IFileProbe fileProbe,
        IRustDeskServiceManager? serviceManager = null,
        IRustDeskOptionReader? optionReader = null) =>
        new(
            fileProbe,
            serviceManager ?? new FakeServiceManager(RustDeskServiceStatus.Running),
            optionReader ?? new FakeOptionReader(CreateExpectedOptions()),
            verificationAttempts: 1,
            verificationDelay: TimeSpan.Zero,
            delay: (_, _) => Task.CompletedTask);

    private static Dictionary<string, string> CreateExpectedOptions() =>
        new(StringComparer.Ordinal)
        {
            [WindowsRustDeskOptions.IdServer] = "id.example.test",
            [WindowsRustDeskOptions.RelayServer] = "relay.example.test",
            [WindowsRustDeskOptions.PublicKey] = "public-key"
        };

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

    private sealed class FakeServiceManager(RustDeskServiceStatus status)
        : IRustDeskServiceManager
    {
        public Task<OperationResult<RustDeskServiceStatus>> GetStatusAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                OperationResult<RustDeskServiceStatus>.Succeeded(status, "Read."));

        public Task<OperationResult> EnsureInstalledAsync(
            RustDeskInstallation installation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Succeeded("Installed."));

        public Task<OperationResult> EnsureRunningAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult.Succeeded("Running."));
    }

    private sealed class FakeOptionReader(IReadOnlyDictionary<string, string> values)
        : IRustDeskOptionReader
    {
        public List<string> RequestedOptions { get; } = [];

        public Task<OperationResult<string>> ReadAsync(
            RustDeskInstallation installation,
            string optionName,
            CancellationToken cancellationToken = default)
        {
            RequestedOptions.Add(optionName);
            var value = values.TryGetValue(optionName, out var configuredValue)
                ? configuredValue
                : string.Empty;
            return Task.FromResult(
                OperationResult<string>.Succeeded(value, "Read."));
        }
    }
}
