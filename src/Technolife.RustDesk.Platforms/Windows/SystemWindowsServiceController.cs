using System.ServiceProcess;
using System.Runtime.Versioning;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Abstractions;

namespace Technolife.RustDesk.Platforms.Windows;

[SupportedOSPlatform("windows")]
public sealed class SystemWindowsServiceController : IWindowsServiceController
{
    public RustDeskServiceStatus GetStatus(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        var services = ServiceController.GetServices();

        try
        {
            var service = services.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.ServiceName,
                    serviceName,
                    StringComparison.OrdinalIgnoreCase));

            if (service is null)
            {
                return RustDeskServiceStatus.NotInstalled;
            }

            service.Refresh();
            return Map(service.Status);
        }
        finally
        {
            foreach (var service in services)
            {
                service.Dispose();
            }
        }
    }

    public void Start(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        using var service = new ServiceController(serviceName);
        service.Start();
    }

    public void Continue(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        using var service = new ServiceController(serviceName);
        service.Continue();
    }

    private static RustDeskServiceStatus Map(ServiceControllerStatus status) =>
        status switch
        {
            ServiceControllerStatus.Stopped => RustDeskServiceStatus.Stopped,
            ServiceControllerStatus.StartPending => RustDeskServiceStatus.StartPending,
            ServiceControllerStatus.StopPending => RustDeskServiceStatus.StopPending,
            ServiceControllerStatus.Running => RustDeskServiceStatus.Running,
            ServiceControllerStatus.ContinuePending => RustDeskServiceStatus.ContinuePending,
            ServiceControllerStatus.PausePending => RustDeskServiceStatus.PausePending,
            ServiceControllerStatus.Paused => RustDeskServiceStatus.Paused,
            _ => RustDeskServiceStatus.Unknown
        };
}
