namespace Technolife.RustDesk.Platforms.Abstractions;

public interface IInstallerFileSystem
{
    string CreateTemporaryDirectory();

    void CopyFile(string sourcePath, string destinationPath, bool overwrite);

    void DeleteFile(string path);

    void DeleteDirectory(string path, bool recursive);
}
