namespace Technolife.RustDesk.Platforms.Windows;

public sealed class WindowsRustDeskPaths
{
    private const string InstallationDirectoryName = "RustDesk";
    private const string ExecutableFileName = "RustDesk.exe";

    public WindowsRustDeskPaths(string programFiles, string? programFilesX86)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programFiles);

        var candidates = new List<string>
        {
            CreateExecutablePath(programFiles)
        };

        if (!string.IsNullOrWhiteSpace(programFilesX86))
        {
            var x86Candidate = CreateExecutablePath(programFilesX86);

            if (!candidates.Contains(x86Candidate, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(x86Candidate);
            }
        }

        CandidatePaths = candidates.AsReadOnly();
    }

    public IReadOnlyList<string> CandidatePaths { get; }

    public static WindowsRustDeskPaths FromCurrentEnvironment() =>
        new(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

    private static string CreateExecutablePath(string baseDirectory) =>
        $"{baseDirectory.TrimEnd('\\', '/')}\\{InstallationDirectoryName}\\{ExecutableFileName}";
}
