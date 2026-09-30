using Technolife.RustDesk.Platforms.Abstractions;

namespace Technolife.RustDesk.Platforms.Windows;

public sealed class SystemInstallerFileSystem : IInstallerFileSystem
{
    public string CreateTemporaryDirectory()
    {
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "Technolife",
            "RustDeskConfigurator");
        var executionDirectory = Path.Combine(rootDirectory, Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(executionDirectory);
        return executionDirectory;
    }

    public void CopyFile(string sourcePath, string destinationPath, bool overwrite) =>
        File.Copy(sourcePath, destinationPath, overwrite);

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive);
        }
    }
}
