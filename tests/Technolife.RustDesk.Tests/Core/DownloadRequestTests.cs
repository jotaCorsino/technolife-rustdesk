using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Tests.Core;

public sealed class DownloadRequestTests
{
    private const string Sha256 =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void CreatesRequestWithHttpsAndNormalizedChecksum()
    {
        var request = new DownloadRequest(
            new Uri("https://example.test/rustdesk.exe"),
            "C:\\downloads\\rustdesk.exe",
            Sha256);

        Assert.Equal(Uri.UriSchemeHttps, request.Source.Scheme);
        Assert.Equal("C:\\downloads\\rustdesk.exe", request.DestinationPath);
        Assert.Equal(Sha256.ToUpperInvariant(), request.ExpectedSha256);
    }

    [Fact]
    public void RejectsNonHttpsSource()
    {
        Assert.Throws<ArgumentException>(() =>
            new DownloadRequest(
                new Uri("http://example.test/rustdesk.exe"),
                "C:\\downloads\\rustdesk.exe",
                Sha256));
    }

    [Fact]
    public void RejectsInvalidChecksum()
    {
        Assert.Throws<ArgumentException>(() =>
            new DownloadRequest(
                new Uri("https://example.test/rustdesk.exe"),
                "C:\\downloads\\rustdesk.exe",
                "invalid"));
    }

    [Fact]
    public void StoresPositiveTimeout()
    {
        var timeout = TimeSpan.FromMinutes(5);

        var request = new DownloadRequest(
            new Uri("https://example.test/rustdesk.exe"),
            "C:\\downloads\\rustdesk.exe",
            Sha256,
            timeout);

        Assert.Equal(timeout, request.Timeout);
    }

    [Fact]
    public void RejectsNonPositiveTimeout()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DownloadRequest(
                new Uri("https://example.test/rustdesk.exe"),
                "C:\\downloads\\rustdesk.exe",
                Sha256,
                TimeSpan.Zero));
    }
}
