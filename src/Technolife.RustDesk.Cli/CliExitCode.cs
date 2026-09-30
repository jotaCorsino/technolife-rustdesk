namespace Technolife.RustDesk.Cli;

public enum CliExitCode
{
    Success = 0,
    GeneralError = 1,
    RustDeskNotFound = 2,
    InvalidConfiguration = 3,
    ProcessFailed = 4,
    ValidationFailed = 5,
    UnsupportedPlatform = 6
}
