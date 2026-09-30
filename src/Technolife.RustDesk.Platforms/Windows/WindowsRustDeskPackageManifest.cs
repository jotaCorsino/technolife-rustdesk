using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Platforms.Packages;

public static class WindowsRustDeskPackageManifest
{
    public static RustDeskPackageManifest Create() =>
        new(
            new Version(1, 4, 9),
            PlatformKind.Windows,
            CpuArchitecture.X64,
            new Uri(
                "https://github.com/rustdesk/rustdesk/releases/download/1.4.9/" +
                "rustdesk-1.4.9-x86_64.exe"),
            "EAEDEB0088E687BF46F7C46A9C6EA5493CE51F3134DFD6ACBEDB47B5B9136274",
            "rustdesk-1.4.9-x86_64.exe");
}
