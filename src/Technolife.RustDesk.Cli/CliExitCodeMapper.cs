using Technolife.RustDesk.Core.Enums;

namespace Technolife.RustDesk.Cli;

public static class CliExitCodeMapper
{
    public static CliExitCode FromErrorCode(ErrorCode errorCode) =>
        errorCode switch
        {
            ErrorCode.None => CliExitCode.Success,
            ErrorCode.RustDeskNotFound => CliExitCode.RustDeskNotFound,
            ErrorCode.ConfigurationFailed => CliExitCode.InvalidConfiguration,
            ErrorCode.ProcessFailed => CliExitCode.ProcessFailed,
            ErrorCode.ValidationFailed => CliExitCode.ValidationFailed,
            ErrorCode.UnsupportedPlatform => CliExitCode.UnsupportedPlatform,
            ErrorCode.DownloadFailed => CliExitCode.DownloadFailed,
            ErrorCode.ChecksumMismatch => CliExitCode.ChecksumMismatch,
            ErrorCode.InstallationFailed => CliExitCode.InstallationFailed,
            ErrorCode.ElevationFailed => CliExitCode.ElevationFailed,
            _ => CliExitCode.GeneralError
        };
}
