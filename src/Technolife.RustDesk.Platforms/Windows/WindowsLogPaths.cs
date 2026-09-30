namespace Technolife.RustDesk.Platforms.Windows;

public static class WindowsLogPaths
{
    public static string GetDefaultDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Technolife",
            "RustDeskConfigurator",
            "logs");
}
