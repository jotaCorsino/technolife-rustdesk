using Technolife.RustDesk.Platforms.Configuration;

namespace Technolife.RustDesk.Tests.Platforms.Configuration;

public sealed class TechnolifeRustDeskConfigurationTests
{
    [Fact]
    public void ProvidesCompleteDistributableRuntimeConfiguration()
    {
        var configuration = TechnolifeRustDeskConfiguration.Create();

        Assert.Equal("remoto.technolife.net.br", configuration.IdServer);
        Assert.Equal("remoto.technolife.net.br", configuration.RelayServer);
        Assert.False(string.IsNullOrWhiteSpace(configuration.PublicKey));
        Assert.False(string.IsNullOrWhiteSpace(configuration.ExportedConfiguration));
        Assert.DoesNotContain(
            configuration.ExportedConfiguration,
            configuration.ToString());
    }
}
