using System.Diagnostics;
using Technolife.RustDesk.Platforms.Abstractions;

namespace Technolife.RustDesk.Platforms.Windows;

public sealed class SystemFileProbe : IFileProbe
{
    public bool FileExists(string path) => File.Exists(path);

    public Version? ReadVersion(string path)
    {
        var versionInfo = FileVersionInfo.GetVersionInfo(path);

        return ParseVersion(versionInfo.FileVersion)
            ?? ParseVersion(versionInfo.ProductVersion);
    }

    private static Version? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmedValue = value.Trim();

        if (Version.TryParse(trimmedValue, out var version))
        {
            return version;
        }

        var numericPrefix = new string(
            trimmedValue
                .TakeWhile(character => char.IsAsciiDigit(character) || character is '.')
                .ToArray())
            .TrimEnd('.');

        return Version.TryParse(numericPrefix, out version) ? version : null;
    }
}
