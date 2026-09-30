using Technolife.RustDesk.Core;
using Technolife.RustDesk.Platforms;

var platform = new PlatformInformationProvider().Current;

Console.WriteLine(ApplicationInfo.Name);
Console.WriteLine($"Version: {ApplicationInfo.Version}");
Console.WriteLine();
Console.WriteLine($"Platform: {platform.Kind}");
Console.WriteLine($"Architecture: {platform.Architecture}");
Console.WriteLine();
Console.WriteLine("Status: Project foundation initialized.");
