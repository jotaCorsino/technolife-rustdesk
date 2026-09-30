namespace Technolife.RustDesk.Cli;

public enum CliExitCode
{
    Success = 0,
    GeneralError = 1,
    RustDeskNotFound = 2,
    InvalidConfiguration = 3,
    ProcessFailed = 4,
    ValidationFailed = 5,
    UnsupportedPlatform = 6,
    DownloadFailed = 7,
    ChecksumMismatch = 8,
    InstallationFailed = 9,
    ElevationFailed = 10
}
