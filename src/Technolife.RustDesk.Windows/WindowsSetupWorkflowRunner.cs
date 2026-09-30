using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Core.Services;
using Technolife.RustDesk.Platforms;
using Technolife.RustDesk.Platforms.Configuration;
using Technolife.RustDesk.Platforms.Downloads;
using Technolife.RustDesk.Platforms.Integrity;
using Technolife.RustDesk.Platforms.Logging;
using Technolife.RustDesk.Platforms.Packages;
using Technolife.RustDesk.Platforms.Processes;
using Technolife.RustDesk.Platforms.Windows;

namespace Technolife.RustDesk.Windows;

public sealed class WindowsSetupWorkflowRunner : ISetupWorkflowRunner
{
    public async Task<OperationResult<RustDeskSetupWorkflowResult>> RunAsync(
        IProgress<SetupProgress> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var configuration = TechnolifeRustDeskConfiguration.Create();
        var platformEnvironment = new PlatformInformationProvider();
        var logger = new FileAppLogger(
            WindowsLogPaths.GetDefaultDirectory(),
            [configuration.ExportedConfiguration]);
        var detector = new WindowsRustDeskDetector(
            new SystemFileProbe(),
            platformEnvironment,
            WindowsRustDeskPaths.FromCurrentEnvironment());
        var processRunner = new SystemProcessRunner();
        var serviceManager = new WindowsRustDeskServiceManager(
            processRunner,
            new SystemWindowsServiceController(),
            logger);
        var configurationWorkflow = new RustDeskConfigurationWorkflow(
            detector,
            serviceManager,
            new WindowsRustDeskConfigurator(processRunner),
            new WindowsRustDeskValidator(
                new SystemFileProbe(),
                serviceManager,
                new WindowsRustDeskOptionReader(processRunner)),
            platformEnvironment,
            logger);

        using var downloadClient = new HttpDownloadClient();
        var installer = new WindowsRustDeskInstaller(
            WindowsRustDeskPackageManifest.Create(),
            downloadClient,
            new Sha256FileIntegrityValidator(),
            processRunner,
            platformEnvironment,
            new SystemInstallerFileSystem(),
            logger);
        var workflow = new RustDeskSetupWorkflow(
            detector,
            installer,
            configurationWorkflow,
            platformEnvironment,
            logger);

        return await workflow
            .ExecuteAsync(configuration, cancellationToken, progress)
            .ConfigureAwait(false);
    }
}
