using System.Runtime.InteropServices;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Platforms;

namespace Technolife.RustDesk.Tests;

public sealed class PlatformInformationProviderTests
{
    [Fact]
    public void ReturnsInformationForCurrentRuntime()
    {
        var platform = new PlatformInformationProvider().Current;

        Assert.Equal(ExpectedPlatformKind(), platform.Kind);
        Assert.Equal(ExpectedArchitecture(), platform.Architecture);
    }

    private static PlatformKind ExpectedPlatformKind()
    {
        if (OperatingSystem.IsWindows())
        {
            return PlatformKind.Windows;
        }

        if (OperatingSystem.IsLinux())
        {
            return PlatformKind.Linux;
        }

        if (OperatingSystem.IsMacOS())
        {
            return PlatformKind.MacOS;
        }

        return PlatformKind.Unknown;
    }

    private static CpuArchitecture ExpectedArchitecture() =>
        RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => CpuArchitecture.X64,
            Architecture.Arm64 => CpuArchitecture.Arm64,
            _ => CpuArchitecture.Unknown
        };
}
