using Technolife.RustDesk.Core.Enums;

namespace Technolife.RustDesk.Core.Models;

public sealed record RustDeskPackageManifest
{
    public RustDeskPackageManifest(
        Version version,
        PlatformKind platform,
        CpuArchitecture architecture,
        Uri downloadUri,
        string sha256,
        string fileName)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(downloadUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!downloadUri.IsAbsoluteUri || downloadUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException(
                "Package download URI must be an absolute HTTPS URI.",
                nameof(downloadUri));
        }

        if (sha256.Length is not 64 || !sha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "Package checksum must be a 64-character SHA-256 value.",
                nameof(sha256));
        }

        Version = version;
        Platform = platform;
        Architecture = architecture;
        DownloadUri = downloadUri;
        Sha256 = sha256.ToUpperInvariant();
        FileName = fileName;
    }

    public Version Version { get; }

    public PlatformKind Platform { get; }

    public CpuArchitecture Architecture { get; }

    public Uri DownloadUri { get; }

    public string Sha256 { get; }

    public string FileName { get; }
}
