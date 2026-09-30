using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Tests.Core;

public sealed class RustDeskInstallationTests
{
    private static readonly PlatformInfo WindowsX64 =
        new(PlatformKind.Windows, CpuArchitecture.X64);

    [Fact]
    public void CreatesFoundInstallationWithTypedMetadata()
    {
        var version = new Version(1, 4, 9);

        var installation = RustDeskInstallation.CreateFound(
            "C:\\test\\rustdesk.exe",
            version,
            WindowsX64);

        Assert.True(installation.Found);
        Assert.Equal("C:\\test\\rustdesk.exe", installation.ExecutablePath);
        Assert.Equal(version, installation.Version);
        Assert.Equal(WindowsX64, installation.Platform);
    }

    [Fact]
    public void CreatesNotFoundInstallationWithoutExecutableMetadata()
    {
        var installation = RustDeskInstallation.CreateNotFound(WindowsX64);

        Assert.False(installation.Found);
        Assert.Null(installation.ExecutablePath);
        Assert.Null(installation.Version);
        Assert.Equal(WindowsX64, installation.Platform);
    }

    [Fact]
    public void FoundInstallationRequiresExecutablePath()
    {
        Assert.Throws<ArgumentException>(() =>
            RustDeskInstallation.CreateFound(" ", null, WindowsX64));
    }
}
