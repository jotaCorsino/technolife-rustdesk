using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Core.Abstractions;

public interface IPlatformEnvironment
{
    PlatformInfo Current { get; }
}
