using System.Security;
using System.Security.Principal;
using Technolife.RustDesk.Platforms.Abstractions;

namespace Technolife.RustDesk.Platforms.Processes;

public sealed class WindowsProcessElevationContext : IProcessElevationContext
{
    public bool IsCurrentProcessElevated
    {
        get
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception exception) when (
                exception is SecurityException
                    or UnauthorizedAccessException
                    or InvalidOperationException)
            {
                return false;
            }
        }
    }
}
