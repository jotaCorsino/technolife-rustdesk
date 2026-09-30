using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Abstractions;
using Technolife.RustDesk.Platforms.Packages;
using Technolife.RustDesk.Platforms.Windows;

namespace Technolife.RustDesk.Tests.Platforms.Windows;

public sealed class WindowsRustDeskInstallerTests
{
    [Fact]
    public async Task UsesLocalPackageValidatesAndRunsElevatedSilentInstaller()
    {
        var download = new FakeDownloadClient();
        var integrity = new FakeIntegrityValidator();
        var process = new FakeProcessRunner();
        var fileSystem = new FakeInstallerFileSystem();
        var installer = CreateInstaller(
            download,
            integrity,
            process,
            fileSystem,
            @"C:\packages\rustdesk.exe");

        var result = await installer.InstallAsync();

        Assert.True(result.Success);
        Assert.Equal(0, download.CallCount);
        Assert.Equal(1, fileSystem.CopyCallCount);
        Assert.Equal(@"C:\packages\rustdesk.exe", fileSystem.CopiedSource);
        Assert.Equal(1, integrity.CallCount);
        Assert.Equal(
            WindowsRustDeskPackageManifest.Create().Sha256,
            integrity.ExpectedSha256);
        var request = Assert.IsType<ProcessRequest>(process.Request);
        Assert.True(request.RequiresElevation);
        Assert.Equal(new[] { "--silent-install" }, request.Arguments);
        Assert.Equal(1, fileSystem.DeleteDirectoryCallCount);
    }

    [Fact]
    public async Task DownloadsOfficialPackageWhenNoLocalOverrideWasSupplied()
    {
        var download = new FakeDownloadClient();
        var installer = CreateInstaller(
            download,
            new FakeIntegrityValidator(),
            new FakeProcessRunner(),
            new FakeInstallerFileSystem());

        var result = await installer.InstallAsync();

        Assert.True(result.Success);
        var request = Assert.IsType<DownloadRequest>(download.Request);
        Assert.Equal(WindowsRustDeskPackageManifest.Create().DownloadUri, request.Source);
        Assert.Equal(TimeSpan.FromMinutes(5), request.Timeout);
    }

    [Fact]
    public async Task RejectsChecksumMismatchBeforeStartingInstallerAndCleansUp()
    {
        var process = new FakeProcessRunner();
        var fileSystem = new FakeInstallerFileSystem();
        var integrity = new FakeIntegrityValidator
        {
            Result = OperationResult.Failed(
                ErrorCode.ChecksumMismatch,
                "Mismatch.")
        };
        var installer = CreateInstaller(
            new FakeDownloadClient(),
            integrity,
            process,
            fileSystem,
            @"C:\packages\rustdesk.exe");

        var result = await installer.InstallAsync();

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ChecksumMismatch, result.ErrorCode);
        Assert.Equal(0, process.CallCount);
        Assert.Equal(1, fileSystem.DeleteFileCallCount);
        Assert.Equal(1, fileSystem.DeleteDirectoryCallCount);
    }

    [Theory]
    [InlineData(ErrorCode.ElevationFailed, ErrorCode.ElevationFailed)]
    [InlineData(ErrorCode.ProcessFailed, ErrorCode.InstallationFailed)]
    public async Task MapsProcessFailureToInstallationSpecificCode(
        ErrorCode processError,
        ErrorCode expectedError)
    {
        var process = new FakeProcessRunner
        {
            Result = OperationResult<ProcessResult>.Failed(
                processError,
                "Process failed.")
        };
        var installer = CreateInstaller(
            new FakeDownloadClient(),
            new FakeIntegrityValidator(),
            process,
            new FakeInstallerFileSystem(),
            @"C:\packages\rustdesk.exe");

        var result = await installer.InstallAsync();

        Assert.False(result.Success);
        Assert.Equal(expectedError, result.ErrorCode);
    }

    [Fact]
    public async Task StopsAfterDownloadFailure()
    {
        var integrity = new FakeIntegrityValidator();
        var process = new FakeProcessRunner();
        var download = new FakeDownloadClient
        {
            Result = OperationResult.Failed(
                ErrorCode.DownloadFailed,
                "Network unavailable.")
        };
        var installer = CreateInstaller(
            download,
            integrity,
            process,
            new FakeInstallerFileSystem());

        var result = await installer.InstallAsync();

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.DownloadFailed, result.ErrorCode);
        Assert.Equal(0, integrity.CallCount);
        Assert.Equal(0, process.CallCount);
    }

    [Fact]
    public async Task RejectsNonZeroInstallerExitCode()
    {
        var process = new FakeProcessRunner
        {
            Result = OperationResult<ProcessResult>.Succeeded(
                new ProcessResult(5, string.Empty, string.Empty),
                "Completed.")
        };
        var installer = CreateInstaller(
            new FakeDownloadClient(),
            new FakeIntegrityValidator(),
            process,
            new FakeInstallerFileSystem(),
            @"C:\packages\rustdesk.exe");

        var result = await installer.InstallAsync();

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.InstallationFailed, result.ErrorCode);
        Assert.Contains("code 5", result.TechnicalDetails);
    }

    private static WindowsRustDeskInstaller CreateInstaller(
        IDownloadClient downloadClient,
        IFileIntegrityValidator integrityValidator,
        IProcessRunner processRunner,
        IInstallerFileSystem fileSystem,
        string? localInstallerPath = null) =>
        new(
            WindowsRustDeskPackageManifest.Create(),
            downloadClient,
            integrityValidator,
            processRunner,
            new StubPlatformEnvironment(),
            fileSystem,
            new InMemoryLogger(),
            localInstallerPath);

    private sealed class FakeDownloadClient : IDownloadClient
    {
        public OperationResult Result { get; init; } =
            OperationResult.Succeeded("Downloaded.");
        public int CallCount { get; private set; }
        public DownloadRequest? Request { get; private set; }

        public Task<OperationResult> DownloadAsync(
            DownloadRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Request = request;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeIntegrityValidator : IFileIntegrityValidator
    {
        public OperationResult Result { get; init; } =
            OperationResult.Succeeded("Valid.");
        public int CallCount { get; private set; }
        public string? ExpectedSha256 { get; private set; }

        public Task<OperationResult> ValidateSha256Async(
            string filePath,
            string expectedSha256,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ExpectedSha256 = expectedSha256;
            return Task.FromResult(Result);
        }
    }

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

    private sealed class FakeInstallerFileSystem : IInstallerFileSystem
    {
        public int CopyCallCount { get; private set; }
        public int DeleteFileCallCount { get; private set; }
        public int DeleteDirectoryCallCount { get; private set; }
        public string? CopiedSource { get; private set; }

        public string CreateTemporaryDirectory() => @"C:\Temp\Technolife\test";

        public void CopyFile(string sourcePath, string destinationPath, bool overwrite)
        {
            CopyCallCount++;
            CopiedSource = sourcePath;
        }

        public void DeleteFile(string path) => DeleteFileCallCount++;

        public void DeleteDirectory(string path, bool recursive) =>
            DeleteDirectoryCallCount++;
    }

    private sealed class StubPlatformEnvironment : IPlatformEnvironment
    {
        public PlatformInfo Current { get; } =
            new(PlatformKind.Windows, CpuArchitecture.X64);
    }

    private sealed class InMemoryLogger : IAppLogger
    {
        public string Destination => "memory://installer.log";
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message, string? technicalDetails = null) { }
    }
}
