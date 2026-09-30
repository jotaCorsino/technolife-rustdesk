namespace Technolife.RustDesk.Core.Models;

public enum RustDeskServiceStatus
{
    NotInstalled = 0,
    Stopped,
    StartPending,
    StopPending,
    Running,
    ContinuePending,
    PausePending,
    Paused,
    Unknown
}
