namespace Technolife.RustDesk.Platforms.Abstractions;

public interface IProcessElevationContext
{
    bool IsCurrentProcessElevated { get; }
}
