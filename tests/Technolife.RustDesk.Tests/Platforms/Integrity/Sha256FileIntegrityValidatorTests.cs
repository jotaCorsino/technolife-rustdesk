using System.Security.Cryptography;
using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Platforms.Integrity;

namespace Technolife.RustDesk.Tests.Platforms.Integrity;

public sealed class Sha256FileIntegrityValidatorTests
{
    [Fact]
    public async Task AcceptsMatchingChecksumCaseInsensitively()
    {
        var path = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(path, "homologated package");
            var checksum = Convert.ToHexString(
                SHA256.HashData(await File.ReadAllBytesAsync(path)));
            var validator = new Sha256FileIntegrityValidator();

            var result = await validator.ValidateSha256Async(path, checksum.ToLowerInvariant());

            Assert.True(result.Success);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RejectsMismatchingChecksum()
    {
        var path = Path.GetTempFileName();

        try
        {
            await File.WriteAllTextAsync(path, "unexpected package");
            var validator = new Sha256FileIntegrityValidator();

            var result = await validator.ValidateSha256Async(path, new string('0', 64));

            Assert.False(result.Success);
            Assert.Equal(ErrorCode.ChecksumMismatch, result.ErrorCode);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
