using System.Security.Cryptography;
using Technolife.RustDesk.Core.Abstractions;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Platforms.Integrity;

public sealed class Sha256FileIntegrityValidator : IFileIntegrityValidator
{
    public async Task<OperationResult> ValidateSha256Async(
        string filePath,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);

        if (expectedSha256.Length is not 64 || !expectedSha256.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "Expected checksum must be a 64-character SHA-256 value.",
                nameof(expectedSha256));
        }

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await SHA256
                .HashDataAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            var actualSha256 = Convert.ToHexString(hash);

            if (!actualSha256.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                return OperationResult.Failed(
                    ErrorCode.ChecksumMismatch,
                    "The RustDesk package failed SHA-256 validation.",
                    $"Expected {expectedSha256.ToUpperInvariant()}, actual {actualSha256}.");
            }

            return OperationResult.Succeeded(
                "The RustDesk package SHA-256 checksum is valid.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or NotSupportedException)
        {
            return OperationResult.Failed(
                ErrorCode.ChecksumMismatch,
                "The RustDesk package could not be validated.",
                $"Checksum calculation failed with {exception.GetType().Name}.");
        }
    }
}
