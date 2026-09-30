using System.Reflection;

namespace Technolife.RustDesk.Core;

public static class ApplicationInfo
{
    public const string Name = "Technolife RustDesk Configurator";

    public static string Version { get; } =
        typeof(ApplicationInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? "unknown";
}
