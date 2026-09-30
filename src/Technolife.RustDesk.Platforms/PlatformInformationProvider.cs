using System.Runtime.InteropServices;

namespace Technolife.RustDesk.Platforms;

public static class PlatformInformationProvider
{
    public static PlatformInformation GetCurrent() =>
        new(GetOperatingSystemName(), RuntimeInformation.OSArchitecture);

    private static string GetOperatingSystemName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "Windows";
        }

        if (OperatingSystem.IsLinux())
        {
            return "Linux";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macOS";
        }

        return "Unknown";
    }
}
