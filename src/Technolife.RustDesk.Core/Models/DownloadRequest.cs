namespace Technolife.RustDesk.Core.Models;

public sealed class DownloadRequest
{
    public DownloadRequest(Uri source, string destinationPath, string expectedSha256)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);

        if (!source.IsAbsoluteUri || source.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Download source must be an absolute HTTPS URI.", nameof(source));
        }

        if (!IsSha256(expectedSha256))
        {
            throw new ArgumentException("Expected checksum must be a 64-character SHA-256 value.", nameof(expectedSha256));
        }

        Source = source;
        DestinationPath = destinationPath;
        ExpectedSha256 = expectedSha256.ToUpperInvariant();
    }

    public Uri Source { get; }

    public string DestinationPath { get; }

    public string ExpectedSha256 { get; }

    private static bool IsSha256(string value) =>
        value.Length is 64 && value.All(Uri.IsHexDigit);
}
