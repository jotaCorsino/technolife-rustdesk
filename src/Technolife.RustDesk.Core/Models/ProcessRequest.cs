namespace Technolife.RustDesk.Core.Models;

public sealed class ProcessRequest
{
    public ProcessRequest(
        string executable,
        IEnumerable<string>? arguments = null,
        string? workingDirectory = null,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);

        if (workingDirectory is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        }

        if (timeout.HasValue && timeout.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "Timeout must be greater than zero.");
        }

        Executable = executable;
        Arguments = Array.AsReadOnly(arguments?.ToArray() ?? Array.Empty<string>());
        WorkingDirectory = workingDirectory;
        Timeout = timeout;
    }

    public string Executable { get; }

    public IReadOnlyList<string> Arguments { get; }

    public string? WorkingDirectory { get; }

    public TimeSpan? Timeout { get; }

    public override string ToString() =>
        $"Process request for '{Path.GetFileName(Executable)}' " +
        $"with {Arguments.Count} argument(s); argument values are redacted.";
}
