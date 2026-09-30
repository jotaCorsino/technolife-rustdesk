using System.Runtime.InteropServices;
using Technolife.RustDesk.Platforms;

namespace Technolife.RustDesk.Tests;

public sealed class PlatformInformationProviderTests
{
    [Fact]
    public void ReturnsInformationForCurrentRuntime()
    {
        var platform = PlatformInformationProvider.GetCurrent();

        Assert.Contains(
            platform.OperatingSystem,
            new[] { "Windows", "Linux", "macOS", "Unknown" });
        Assert.Equal(RuntimeInformation.OSArchitecture, platform.Architecture);
    }
}
