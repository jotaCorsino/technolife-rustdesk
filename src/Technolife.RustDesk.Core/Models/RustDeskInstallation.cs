namespace Technolife.RustDesk.Core.Models;

public sealed record RustDeskInstallation
{
    private RustDeskInstallation(
        bool found,
        string? executablePath,
        Version? version,
        PlatformInfo platform)
    {
        Found = found;
        ExecutablePath = executablePath;
        Version = version;
        Platform = platform;
    }

    public bool Found { get; }

    public string? ExecutablePath { get; }

    public Version? Version { get; }

    public PlatformInfo Platform { get; }

    public static RustDeskInstallation CreateFound(
        string executablePath,
        Version? version,
        PlatformInfo platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(platform);

        return new RustDeskInstallation(true, executablePath, version, platform);
    }

    public static RustDeskInstallation CreateNotFound(PlatformInfo platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        return new RustDeskInstallation(false, null, null, platform);
    }
}
