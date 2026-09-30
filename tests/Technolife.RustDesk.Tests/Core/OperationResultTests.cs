using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Tests.Core;

public sealed class OperationResultTests
{
    [Fact]
    public void CreatesSuccessfulResultWithoutError()
    {
        var result = OperationResult.Succeeded("Completed.");

        Assert.True(result.Success);
        Assert.Equal(ErrorCode.None, result.ErrorCode);
        Assert.Equal("Completed.", result.Message);
        Assert.Null(result.TechnicalDetails);
    }

    [Fact]
    public void CreatesFailedResultWithDiagnosticInformation()
    {
        var result = OperationResult.Failed(
            ErrorCode.ProcessFailed,
            "The process failed.",
            "Exit code 1.");

        Assert.False(result.Success);
        Assert.Equal(ErrorCode.ProcessFailed, result.ErrorCode);
        Assert.Equal("The process failed.", result.Message);
        Assert.Equal("Exit code 1.", result.TechnicalDetails);
    }

    [Fact]
    public void CreatesSuccessfulResultWithValue()
    {
        var value = new object();

        var result = OperationResult<object>.Succeeded(value, "Found.");

        Assert.True(result.Success);
        Assert.Same(value, result.Value);
        Assert.Equal(ErrorCode.None, result.ErrorCode);
    }

    [Fact]
    public void FailureRequiresNonSuccessErrorCode()
    {
        Assert.Throws<ArgumentException>(() =>
            OperationResult.Failed(ErrorCode.None, "Invalid failure."));
    }
}
