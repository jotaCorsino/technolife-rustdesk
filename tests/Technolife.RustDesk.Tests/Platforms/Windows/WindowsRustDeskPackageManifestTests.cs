using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Platforms.Packages;

namespace Technolife.RustDesk.Tests.Platforms.Windows;

public sealed class WindowsRustDeskPackageManifestTests
{
    [Fact]
    public void UsesPinnedOfficialWindowsX64Package()
    {
        var manifest = WindowsRustDeskPackageManifest.Create();

        Assert.Equal(new Version(1, 4, 9), manifest.Version);
        Assert.Equal(PlatformKind.Windows, manifest.Platform);
        Assert.Equal(CpuArchitecture.X64, manifest.Architecture);
        Assert.Equal(
            "https://github.com/rustdesk/rustdesk/releases/download/1.4.9/" +
            "rustdesk-1.4.9-x86_64.exe",
            manifest.DownloadUri.AbsoluteUri);
        Assert.Equal(
            "EAEDEB0088E687BF46F7C46A9C6EA5493CE51F3134DFD6ACBEDB47B5B9136274",
            manifest.Sha256);
        Assert.Equal("rustdesk-1.4.9-x86_64.exe", manifest.FileName);
    }
}
