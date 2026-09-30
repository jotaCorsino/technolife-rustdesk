namespace Technolife.RustDesk.Platforms.Abstractions;

public interface IFileProbe
{
    bool FileExists(string path);

    Version? ReadVersion(string path);
}
