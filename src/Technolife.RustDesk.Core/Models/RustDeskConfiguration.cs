namespace Technolife.RustDesk.Core.Models;

public sealed class RustDeskConfiguration
{
    public RustDeskConfiguration(
        string idServer,
        string relayServer,
        string publicKey,
        string exportedConfiguration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idServer);
        ArgumentException.ThrowIfNullOrWhiteSpace(relayServer);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportedConfiguration);

        IdServer = idServer;
        RelayServer = relayServer;
        PublicKey = publicKey;
        ExportedConfiguration = exportedConfiguration;
    }

    public string IdServer { get; }

    public string RelayServer { get; }

    public string PublicKey { get; }

    public string ExportedConfiguration { get; }

    public override string ToString() => "RustDesk configuration values are redacted.";
}
