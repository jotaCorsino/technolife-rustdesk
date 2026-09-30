using System.Text;
using Technolife.RustDesk.Core.Abstractions;

namespace Technolife.RustDesk.Platforms.Logging;

public sealed class FileAppLogger : IAppLogger
{
    private const string RedactedValue = "[REDACTED]";

    private readonly object _writeLock = new();
    private readonly string[] _sensitiveValues;
    private readonly TimeProvider _timeProvider;
    private readonly Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public FileAppLogger(
        string directory,
        IEnumerable<string>? sensitiveValues = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _sensitiveValues = sensitiveValues?
            .Where(value => !string.IsNullOrEmpty(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray()
            ?? [];
        _timeProvider = timeProvider ?? TimeProvider.System;

        var fullDirectory = Path.GetFullPath(directory);
        Directory.CreateDirectory(fullDirectory);

        var timestamp = _timeProvider.GetLocalNow();
        Destination = Path.Combine(
            fullDirectory,
            $"technolife-rustdesk-{timestamp:yyyyMMdd-HHmmssfff}.log");

        File.WriteAllText(Destination, string.Empty, _encoding);
    }

    public string Destination { get; }

    public void Info(string message) => Write("INFO", message);

    public void Warning(string message) => Write("WARN", message);

    public void Error(string message, string? technicalDetails = null)
    {
        var entry = string.IsNullOrWhiteSpace(technicalDetails)
            ? message
            : $"{message} Technical details: {technicalDetails}";

        Write("ERROR", entry);
    }

    private void Write(string level, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var safeMessage = Sanitize(message);
        var timestamp = _timeProvider.GetLocalNow();
        var entry = $"{timestamp:yyyy-MM-dd HH:mm:ss} [{level}] {safeMessage}";

        lock (_writeLock)
        {
            File.AppendAllText(Destination, entry + Environment.NewLine, _encoding);
        }
    }

    private string Sanitize(string value)
    {
        var sanitized = value
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        foreach (var sensitiveValue in _sensitiveValues)
        {
            sanitized = sanitized.Replace(
                sensitiveValue,
                RedactedValue,
                StringComparison.Ordinal);
        }

        return sanitized;
    }
}
