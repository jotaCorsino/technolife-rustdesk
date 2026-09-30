using Technolife.RustDesk.Core;

namespace Technolife.RustDesk.Tests;

public sealed class ApplicationInfoTests
{
    [Fact]
    public void ExposesExpectedBetaVersion()
    {
        Assert.Equal("0.1.0-beta.2", ApplicationInfo.Version);
    }
}
