using Technolife.RustDesk.Core;

namespace Technolife.RustDesk.Tests;

public sealed class ApplicationInfoTests
{
    [Fact]
    public void ExposesExpectedDevelopmentVersion()
    {
        Assert.Equal("0.1.0-dev", ApplicationInfo.Version);
    }
}
