using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Platforms.Abstractions;

public interface IWindowsServiceController
{
    RustDeskServiceStatus GetStatus(string serviceName);

    void Start(string serviceName);

    void Continue(string serviceName);
}
