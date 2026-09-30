using Technolife.RustDesk.Core.Enums;

namespace Technolife.RustDesk.Core.Models;

public sealed record OperationResult
{
    private OperationResult(
        bool success,
        ErrorCode errorCode,
        string message,
        string? technicalDetails)
    {
        Success = success;
        ErrorCode = errorCode;
        Message = message;
        TechnicalDetails = technicalDetails;
    }

    public bool Success { get; }

    public ErrorCode ErrorCode { get; }

    public string Message { get; }

    public string? TechnicalDetails { get; }

    public static OperationResult Succeeded(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new OperationResult(true, ErrorCode.None, message, null);
    }

    public static OperationResult Failed(
        ErrorCode errorCode,
        string message,
        string? technicalDetails = null)
    {
        if (errorCode is ErrorCode.None)
        {
            throw new ArgumentException("A failure must have an error code.", nameof(errorCode));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new OperationResult(false, errorCode, message, technicalDetails);
    }
}

public sealed record OperationResult<T>
{
    private OperationResult(
        bool success,
        T? value,
        ErrorCode errorCode,
        string message,
        string? technicalDetails)
    {
        Success = success;
        Value = value;
        ErrorCode = errorCode;
        Message = message;
        TechnicalDetails = technicalDetails;
    }

    public bool Success { get; }

    public T? Value { get; }

    public ErrorCode ErrorCode { get; }

    public string Message { get; }

    public string? TechnicalDetails { get; }

    public static OperationResult<T> Succeeded(T value, string message)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new OperationResult<T>(true, value, ErrorCode.None, message, null);
    }

    public static OperationResult<T> Failed(
        ErrorCode errorCode,
        string message,
        string? technicalDetails = null)
    {
        if (errorCode is ErrorCode.None)
        {
            throw new ArgumentException("A failure must have an error code.", nameof(errorCode));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new OperationResult<T>(false, default, errorCode, message, technicalDetails);
    }
}
