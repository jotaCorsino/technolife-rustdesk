namespace Technolife.RustDesk.Core.Enums;

public enum ErrorCode
{
    None = 0,
    RustDeskNotFound,
    PermissionDenied,
    ProcessFailed,
    DownloadFailed,
    ChecksumMismatch,
    ConfigurationFailed,
    ValidationFailed,
    UnsupportedPlatform,
    DetectionFailed
}
