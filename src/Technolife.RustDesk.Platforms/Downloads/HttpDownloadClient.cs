using System.Net;
using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Platforms.Downloads;

public sealed class HttpDownloadClient : IDownloadClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HttpDownloadClient()
        : this(CreateHttpClient(), ownsHttpClient: true)
    {
    }

    public HttpDownloadClient(HttpClient httpClient)
        : this(httpClient, ownsHttpClient: false)
    {
    }

    private HttpDownloadClient(HttpClient httpClient, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
    }

    public async Task<OperationResult> DownloadAsync(
        DownloadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var destinationDirectory = Path.GetDirectoryName(request.DestinationPath);

        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var partialPath = $"{request.DestinationPath}.partial-{Guid.NewGuid():N}";

        using var executionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (request.Timeout is { } timeout)
        {
            executionCancellation.CancelAfter(timeout);
        }

        try
        {
            using var response = await _httpClient
                .GetAsync(
                    request.Source,
                    HttpCompletionOption.ResponseHeadersRead,
                    executionCancellation.Token)
                .ConfigureAwait(false);

            var finalUri = response.RequestMessage?.RequestUri;

            if (finalUri is null || finalUri.Scheme != Uri.UriSchemeHttps)
            {
                return OperationResult.Failed(
                    ErrorCode.DownloadFailed,
                    "The package download was redirected to a non-HTTPS destination.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return OperationResult.Failed(
                    ErrorCode.DownloadFailed,
                    "The RustDesk package download failed.",
                    $"HTTP status code {(int)response.StatusCode} ({response.StatusCode}).");
            }

            await using (var source = await response.Content
                             .ReadAsStreamAsync(executionCancellation.Token)
                             .ConfigureAwait(false))
            await using (var destination = new FileStream(
                             partialPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 81920,
                             useAsync: true))
            {
                await source
                    .CopyToAsync(destination, executionCancellation.Token)
                    .ConfigureAwait(false);
                await destination
                    .FlushAsync(executionCancellation.Token)
                    .ConfigureAwait(false);
            }

            File.Move(partialPath, request.DestinationPath, overwrite: true);

            return OperationResult.Succeeded(
                "The RustDesk package download completed successfully.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return OperationResult.Failed(
                ErrorCode.DownloadFailed,
                "The RustDesk package download timed out.");
        }
        catch (HttpRequestException exception)
        {
            return OperationResult.Failed(
                ErrorCode.DownloadFailed,
                "The RustDesk package download failed.",
                $"HTTP request failed with {exception.GetType().Name}." +
                FormatStatusCode(exception.StatusCode));
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or NotSupportedException)
        {
            return OperationResult.Failed(
                ErrorCode.DownloadFailed,
                "The RustDesk package could not be written to disk.",
                $"Download storage failed with {exception.GetType().Name}.");
        }
        finally
        {
            TryDeletePartialFile(partialPath);
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static HttpClient CreateHttpClient() =>
        new()
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

    private static string FormatStatusCode(HttpStatusCode? statusCode) =>
        statusCode.HasValue
            ? $" HTTP status code {(int)statusCode.Value} ({statusCode.Value})."
            : string.Empty;

    private static void TryDeletePartialFile(string partialPath)
    {
        try
        {
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or NotSupportedException)
        {
            // A partial file is never promoted to the requested destination.
        }
    }
}
