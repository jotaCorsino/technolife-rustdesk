using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Tests.Core;

public sealed class RustDeskConfigurationTests
{
    [Fact]
    public void RepresentsOnlyDistributableConfigurationValues()
    {
        var configuration = new RustDeskConfiguration(
            "id.example.test",
            "relay.example.test",
            "public-key",
            "exported-config");

        Assert.Equal("id.example.test", configuration.IdServer);
        Assert.Equal("relay.example.test", configuration.RelayServer);
        Assert.Equal("public-key", configuration.PublicKey);
        Assert.Equal("exported-config", configuration.ExportedConfiguration);

        var propertyNames = typeof(RustDeskConfiguration)
            .GetProperties()
            .Select(property => property.Name);

        Assert.All(propertyNames, propertyName =>
        {
            Assert.False(propertyName.Contains("password", StringComparison.OrdinalIgnoreCase));
            Assert.False(propertyName.Contains("credential", StringComparison.OrdinalIgnoreCase));
            Assert.False(propertyName.Contains("token", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void RedactsConfigurationFromTextRepresentation()
    {
        var configuration = new RustDeskConfiguration(
            "id.example.test",
            "relay.example.test",
            "public-key",
            "exported-config");

        var text = configuration.ToString();

        Assert.DoesNotContain("public-key", text);
        Assert.DoesNotContain("exported-config", text);
    }
}
