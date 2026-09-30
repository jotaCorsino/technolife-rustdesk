using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Abstractions;
using Technolife.RustDesk.Platforms.Windows;

namespace Technolife.RustDesk.Tests.Platforms.Windows;

public sealed class WindowsRustDeskDetectorTests
{
    private const string ProgramFilesPath =
        @"C:\Program Files\RustDesk\RustDesk.exe";

    private const string ProgramFilesX86Path =
        @"C:\Program Files (x86)\RustDesk\RustDesk.exe";

    [Fact]
    public async Task ReturnsNotFoundWhenNoCandidateExists()
    {
        var fileProbe = new FakeFileProbe();
        var detector = CreateDetector(fileProbe);

        var result = await detector.DetectAsync();

        var installation = Assert.IsType<RustDeskInstallation>(result.Value);
        Assert.True(result.Success);
        Assert.False(installation.Found);
        Assert.Equal(
            new[] { ProgramFilesPath, ProgramFilesX86Path },
            fileProbe.ProbedPaths);
    }

    [Fact]
    public async Task FindsStandardInstallationWithVersion()
    {
        var expectedVersion = new Version(1, 4, 9);
        var fileProbe = new FakeFileProbe(ProgramFilesPath);
        fileProbe.Versions[ProgramFilesPath] = expectedVersion;
        var detector = CreateDetector(fileProbe);

        var result = await detector.DetectAsync();

        var installation = Assert.IsType<RustDeskInstallation>(result.Value);
        Assert.True(result.Success);
        Assert.True(installation.Found);
        Assert.Equal(ProgramFilesPath, installation.ExecutablePath);
        Assert.Equal(expectedVersion, installation.Version);
        Assert.Equal(PlatformKind.Windows, installation.Platform.Kind);
        Assert.Equal(CpuArchitecture.Unknown, installation.Platform.Architecture);
    }

    [Fact]
    public async Task FindsX86InstallationWhenStandardPathDoesNotExist()
    {
        var fileProbe = new FakeFileProbe(ProgramFilesX86Path);
        var detector = CreateDetector(fileProbe);

        var result = await detector.DetectAsync();

        var installation = Assert.IsType<RustDeskInstallation>(result.Value);
        Assert.True(result.Success);
        Assert.True(installation.Found);
        Assert.Equal(ProgramFilesX86Path, installation.ExecutablePath);
        Assert.Equal(
            new[] { ProgramFilesPath, ProgramFilesX86Path },
            fileProbe.ProbedPaths);
    }

    [Fact]
    public async Task PrioritizesStandardProgramFilesInstallation()
    {
        var fileProbe = new FakeFileProbe(ProgramFilesPath, ProgramFilesX86Path);
        var detector = CreateDetector(fileProbe);

        var result = await detector.DetectAsync();

        var installation = Assert.IsType<RustDeskInstallation>(result.Value);
        Assert.Equal(ProgramFilesPath, installation.ExecutablePath);
        Assert.Equal(new[] { ProgramFilesPath }, fileProbe.ProbedPaths);
    }

    [Fact]
    public async Task KeepsFoundInstallationWhenVersionReadFails()
    {
        var fileProbe = new FakeFileProbe(ProgramFilesPath)
        {
            VersionException = new IOException("Version metadata is unavailable.")
        };
        var detector = CreateDetector(fileProbe);

        var result = await detector.DetectAsync();

        var installation = Assert.IsType<RustDeskInstallation>(result.Value);
        Assert.True(result.Success);
        Assert.True(installation.Found);
        Assert.Equal(ProgramFilesPath, installation.ExecutablePath);
        Assert.Null(installation.Version);
    }

    [Fact]
    public async Task ReturnsFailureWhenFileSystemAccessIsDenied()
    {
        var fileProbe = new FakeFileProbe
        {
            FileExistsException = new UnauthorizedAccessException("Access denied.")
        };
        var detector = CreateDetector(fileProbe);

        var result = await detector.DetectAsync();

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(ErrorCode.PermissionDenied, result.ErrorCode);
        Assert.Equal("Access denied.", result.TechnicalDetails);
    }

    [Fact]
    public async Task ReturnsDetectionFailureWhenFileSystemProbeFails()
    {
        var fileProbe = new FakeFileProbe
        {
            FileExistsException = new IOException("File system unavailable.")
        };
        var detector = CreateDetector(fileProbe);

        var result = await detector.DetectAsync();

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Equal(ErrorCode.DetectionFailed, result.ErrorCode);
        Assert.Equal("File system unavailable.", result.TechnicalDetails);
    }

    [Fact]
    public async Task RejectsNonWindowsEnvironment()
    {
        var detector = new WindowsRustDeskDetector(
            new FakeFileProbe(),
            new StubPlatformEnvironment(PlatformKind.Linux),
            CreatePaths());

        var result = await detector.DetectAsync();

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.UnsupportedPlatform, result.ErrorCode);
    }

    private static WindowsRustDeskDetector CreateDetector(IFileProbe fileProbe) =>
        new(
            fileProbe,
            new StubPlatformEnvironment(PlatformKind.Windows),
            CreatePaths());

    private static WindowsRustDeskPaths CreatePaths() =>
        new(@"C:\Program Files", @"C:\Program Files (x86)");

    private sealed class StubPlatformEnvironment(PlatformKind platformKind)
        : IPlatformEnvironment
    {
        public PlatformInfo Current { get; } =
            new(platformKind, CpuArchitecture.X64);
    }

    private sealed class FakeFileProbe(params string[] existingPaths) : IFileProbe
    {
        private readonly HashSet<string> _existingPaths =
            new(existingPaths, StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Version?> Versions { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public List<string> ProbedPaths { get; } = [];

        public Exception? FileExistsException { get; init; }

        public Exception? VersionException { get; init; }

        public bool FileExists(string path)
        {
            ProbedPaths.Add(path);

            if (FileExistsException is not null)
            {
                throw FileExistsException;
            }

            return _existingPaths.Contains(path);
        }

        public Version? ReadVersion(string path)
        {
            if (VersionException is not null)
            {
                throw VersionException;
            }

            return Versions.GetValueOrDefault(path);
        }
    }
}
