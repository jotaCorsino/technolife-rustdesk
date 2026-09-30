using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Tests.Core;

public sealed class PlatformInfoTests
{
    [Theory]
    [InlineData(PlatformKind.Windows, CpuArchitecture.X64)]
    [InlineData(PlatformKind.Linux, CpuArchitecture.X64)]
    [InlineData(PlatformKind.MacOS, CpuArchitecture.Arm64)]
    [InlineData(PlatformKind.Unknown, CpuArchitecture.Unknown)]
    public void RepresentsPlatformWithoutOperatingSystemDependencies(
        PlatformKind kind,
        CpuArchitecture architecture)
    {
        var platform = new PlatformInfo(kind, architecture);

        Assert.Equal(kind, platform.Kind);
        Assert.Equal(architecture, platform.Architecture);
    }
}
