namespace Technolife.RustDesk.Core.Abstractions;

public interface IAppLogger
{
    string Destination { get; }

    void Info(string message);

    void Warning(string message);

    void Error(string message, string? technicalDetails = null);
}
