using System.Net;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Downloads;

namespace Technolife.RustDesk.Tests.Platforms.Downloads;

public sealed class HttpDownloadClientTests
{
    private const string Sha256 =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task StreamsHttpsResponseToDestination()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var destination = Path.Combine(directory, "rustdesk.exe");
            using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([1, 2, 3, 4])
            });
            var downloader = new HttpDownloadClient(client);

            var result = await downloader.DownloadAsync(CreateRequest(destination));

            Assert.True(result.Success);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(destination));
            Assert.Empty(Directory.GetFiles(directory, "*.partial-*"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ReturnsStructuredFailureForHttpErrorWithoutFinalFile()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var destination = Path.Combine(directory, "rustdesk.exe");
            using var client = CreateClient(_ =>
                new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            var downloader = new HttpDownloadClient(client);

            var result = await downloader.DownloadAsync(CreateRequest(destination));

            Assert.False(result.Success);
            Assert.Equal(ErrorCode.DownloadFailed, result.ErrorCode);
            Assert.False(File.Exists(destination));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DeletesPartialFileWhenStreamFails()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var destination = Path.Combine(directory, "rustdesk.exe");
            using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new FailingReadStream())
            });
            var downloader = new HttpDownloadClient(client);

            var result = await downloader.DownloadAsync(CreateRequest(destination));

            Assert.False(result.Success);
            Assert.Equal(ErrorCode.DownloadFailed, result.ErrorCode);
            Assert.False(File.Exists(destination));
            Assert.Empty(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new StubHandler(responseFactory));

    private static DownloadRequest CreateRequest(string destination) =>
        new(
            new Uri("https://example.test/rustdesk.exe"),
            destination,
            Sha256,
            TimeSpan.FromSeconds(5));

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"technolife-download-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = responseFactory(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }

    private sealed class FailingReadStream : Stream
    {
        private bool _firstRead = true;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new IOException("Simulated interrupted download.");

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_firstRead)
            {
                _firstRead = false;
                buffer.Span[0] = 42;
                return ValueTask.FromResult(1);
            }

            return ValueTask.FromException<int>(
                new IOException("Simulated interrupted download."));
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
