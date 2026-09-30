using System.Runtime.InteropServices;
using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Platforms;

public sealed class PlatformInformationProvider : IPlatformEnvironment
{
    public PlatformInfo Current => new(GetPlatformKind(), GetCpuArchitecture());

    private static PlatformKind GetPlatformKind()
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

    private static CpuArchitecture GetCpuArchitecture() =>
        RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => CpuArchitecture.X64,
            Architecture.Arm64 => CpuArchitecture.Arm64,
            _ => CpuArchitecture.Unknown
        };
}
