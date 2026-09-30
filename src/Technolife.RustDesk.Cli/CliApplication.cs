using System.Runtime.Versioning;
using Technolife.RustDesk.Core;
using Technolife.RustDesk.Core.Enums;
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

namespace Technolife.RustDesk.Cli;

public static class CliApplication
{
    public static async Task<int> RunAsync(
        string[] arguments,
        TextWriter output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);

        WriteHeader(output);

        if (arguments.Length is 0 || IsHelpCommand(arguments[0]))
        {
            WriteUsage(output);
            return (int)CliExitCode.Success;
        }

        if (arguments.Length is 1 && IsVersionCommand(arguments[0]))
        {
            return (int)CliExitCode.Success;
        }

        if (!TryParse(arguments, out var options, out var parseError))
        {
            output.WriteLine($"[ERRO] {parseError}");
            output.WriteLine();
            WriteUsage(output);
            return (int)CliExitCode.GeneralError;
        }

        var platformEnvironment = new PlatformInformationProvider();

        if (!OperatingSystem.IsWindows() ||
            platformEnvironment.Current.Kind is not PlatformKind.Windows)
        {
            output.WriteLine("[ERRO] Esta operação é suportada somente no Windows nesta versão.");
            return (int)CliExitCode.UnsupportedPlatform;
        }

        try
        {
            var detector = CreateDetector(options, platformEnvironment);

            return options.Command switch
            {
                "status" => await RunStatusAsync(
                    detector,
                    output,
                    cancellationToken).ConfigureAwait(false),
                "configure" => await RunConfigureAsync(
                    detector,
                    platformEnvironment,
                    options,
                    output,
                    cancellationToken).ConfigureAwait(false),
                "setup" => await RunSetupAsync(
                    platformEnvironment,
                    options,
                    output,
                    cancellationToken).ConfigureAwait(false),
                _ => (int)CliExitCode.GeneralError
            };
        }
        catch (OperationCanceledException)
        {
            output.WriteLine("[ERRO] A operação foi cancelada.");
            return (int)CliExitCode.GeneralError;
        }
        catch (Exception exception)
        {
            output.WriteLine("[ERRO] Não foi possível concluir a operação.");
            output.WriteLine($"Detalhe técnico: {exception.GetType().Name}.");
            return (int)CliExitCode.GeneralError;
        }
    }

    private static WindowsRustDeskDetector CreateDetector(
        CliOptions options,
        PlatformInformationProvider platformEnvironment)
    {
        var paths = options.RustDeskPath is null
            ? WindowsRustDeskPaths.FromCurrentEnvironment()
            : WindowsRustDeskPaths.FromExecutablePath(options.RustDeskPath);

        return new WindowsRustDeskDetector(
            new SystemFileProbe(),
            platformEnvironment,
            paths);
    }

    private static async Task<int> RunStatusAsync(
        WindowsRustDeskDetector detector,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var result = await detector
            .DetectAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success || result.Value is null)
        {
            output.WriteLine("[ERRO] Não foi possível verificar a instalação do RustDesk.");
            return (int)CliExitCodeMapper.FromErrorCode(result.ErrorCode);
        }

        if (!result.Value.Found)
        {
            output.WriteLine("[ERRO] RustDesk não foi encontrado.");
            output.WriteLine();
            output.WriteLine("Nenhuma alteração foi realizada.");
            return (int)CliExitCode.RustDeskNotFound;
        }

        output.WriteLine(
            $"[OK] RustDesk encontrado: {FormatVersion(result.Value.Version)}");
        output.WriteLine($"Caminho: {result.Value.ExecutablePath}");
        return (int)CliExitCode.Success;
    }

    [SupportedOSPlatform("windows")]
    private static async Task<int> RunConfigureAsync(
        WindowsRustDeskDetector detector,
        PlatformInformationProvider platformEnvironment,
        CliOptions options,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var configuration = TechnolifeRustDeskConfiguration.Create();
        var logDirectory = options.LogDirectory ?? WindowsLogPaths.GetDefaultDirectory();
        var logger = new FileAppLogger(
            logDirectory,
            [configuration.ExportedConfiguration]);
        var processRunner = new SystemProcessRunner();
        var serviceManager = new WindowsRustDeskServiceManager(
            processRunner,
            new SystemWindowsServiceController(),
            logger);
        var workflow = new RustDeskConfigurationWorkflow(
            detector,
            serviceManager,
            new WindowsRustDeskConfigurator(processRunner),
            new WindowsRustDeskValidator(
                new SystemFileProbe(),
                serviceManager,
                new WindowsRustDeskOptionReader(processRunner)),
            platformEnvironment,
            logger);

        var result = await workflow
            .ExecuteAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        WriteConfigurationResult(output, result, logger.Destination);
        return (int)CliExitCodeMapper.FromErrorCode(result.ErrorCode);
    }

    [SupportedOSPlatform("windows")]
    private static async Task<int> RunSetupAsync(
        PlatformInformationProvider platformEnvironment,
        CliOptions options,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var configuration = TechnolifeRustDeskConfiguration.Create();
        var logDirectory = options.LogDirectory ?? WindowsLogPaths.GetDefaultDirectory();
        var logger = new FileAppLogger(
            logDirectory,
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
            logger,
            options.InstallerPath);
        var workflow = new RustDeskSetupWorkflow(
            detector,
            installer,
            configurationWorkflow,
            platformEnvironment,
            logger);

        var result = await workflow
            .ExecuteAsync(configuration, cancellationToken)
            .ConfigureAwait(false);

        WriteSetupResult(output, result, logger.Destination);
        return (int)CliExitCodeMapper.FromErrorCode(result.ErrorCode);
    }

    public static void WriteConfigurationResult(
        TextWriter output,
        OperationResult<RustDeskConfigurationWorkflowResult> result,
        string logPath)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);

        if (result.Success && result.Value is not null)
        {
            output.WriteLine(
                $"[OK] RustDesk encontrado: {FormatVersion(result.Value.Installation.Version)}");
            output.WriteLine("[OK] Configuração aplicada.");

            if (result.Value.Validation.Status is RustDeskValidationStatus.Verified)
            {
                output.WriteLine("[OK] Configuração verificada.");
            }
            else
            {
                output.WriteLine(
                    "[AVISO] Configuração aplicada; confirmação completa dos campos indisponível.");
            }

            output.WriteLine();
            output.WriteLine("Resultado: configuração concluída com sucesso.");
            output.WriteLine($"Log: {logPath}");
            return;
        }

        switch (result.ErrorCode)
        {
            case ErrorCode.RustDeskNotFound:
                output.WriteLine("[ERRO] RustDesk não foi encontrado.");
                output.WriteLine();
                output.WriteLine("Nenhuma alteração foi realizada.");
                break;

            case ErrorCode.ValidationFailed:
                output.WriteLine("[OK] RustDesk encontrado.");
                output.WriteLine("[OK] Configuração aplicada.");
                output.WriteLine("[ERRO] Não foi possível validar o resultado.");
                break;

            case ErrorCode.ProcessFailed:
            case ErrorCode.ConfigurationFailed:
            case ErrorCode.PermissionDenied:
                output.WriteLine("[OK] RustDesk encontrado.");
                output.WriteLine("[ERRO] Não foi possível aplicar a configuração.");
                break;

            case ErrorCode.UnsupportedPlatform:
                output.WriteLine("[ERRO] Plataforma não suportada nesta versão.");
                break;

            default:
                output.WriteLine("[ERRO] Não foi possível concluir o fluxo de configuração.");
                break;
        }

        output.WriteLine();
        output.WriteLine("Consulte o log para detalhes.");
        output.WriteLine($"Log: {logPath}");
    }

    public static void WriteSetupResult(
        TextWriter output,
        OperationResult<RustDeskSetupWorkflowResult> result,
        string logPath)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);

        if (result.Success && result.Value is not null)
        {
            var configurationResult = result.Value.ConfigurationResult;

            if (result.Value.InstallationPerformed)
            {
                output.WriteLine("[OK] Pacote homologado obtido e integridade validada.");
                output.WriteLine("[OK] RustDesk instalado com o pacote homologado.");
            }
            else
            {
                output.WriteLine("[OK] RustDesk já estava instalado; reinstalação dispensada.");
            }

            output.WriteLine(
                $"[OK] RustDesk encontrado: {FormatVersion(configurationResult.Installation.Version)}");
            output.WriteLine("[OK] Configuração aplicada.");

            if (configurationResult.Validation.Status is RustDeskValidationStatus.Verified)
            {
                output.WriteLine("[OK] Configuração verificada.");
            }
            else
            {
                output.WriteLine(
                    "[AVISO] Configuração aplicada; confirmação completa dos campos indisponível.");
            }

            output.WriteLine();
            output.WriteLine("Resultado: preparação concluída com sucesso.");
            output.WriteLine($"Log: {logPath}");
            return;
        }

        switch (result.ErrorCode)
        {
            case ErrorCode.DownloadFailed:
                output.WriteLine("[ERRO] Não foi possível baixar o pacote homologado do RustDesk.");
                break;

            case ErrorCode.ChecksumMismatch:
                output.WriteLine("[ERRO] O pacote do RustDesk foi rejeitado por falha de integridade.");
                break;

            case ErrorCode.InstallationFailed:
                output.WriteLine("[ERRO] Não foi possível instalar ou redetectar o RustDesk.");
                break;

            case ErrorCode.ElevationFailed:
                output.WriteLine("[ERRO] A elevação necessária para instalar o RustDesk não foi concluída.");
                break;

            case ErrorCode.ValidationFailed:
                output.WriteLine("[OK] RustDesk instalado ou já presente.");
                output.WriteLine("[OK] Configuração aplicada.");
                output.WriteLine("[ERRO] Não foi possível validar o resultado.");
                break;

            case ErrorCode.ProcessFailed:
            case ErrorCode.ConfigurationFailed:
            case ErrorCode.PermissionDenied:
                output.WriteLine("[OK] RustDesk instalado ou já presente.");
                output.WriteLine("[ERRO] Não foi possível aplicar a configuração.");
                break;

            case ErrorCode.UnsupportedPlatform:
                output.WriteLine("[ERRO] Plataforma não suportada nesta versão.");
                break;

            default:
                output.WriteLine("[ERRO] Não foi possível concluir a preparação do RustDesk.");
                break;
        }

        output.WriteLine();
        output.WriteLine("Consulte o log para detalhes.");
        output.WriteLine($"Log: {logPath}");
    }

    private static bool TryParse(
        string[] arguments,
        out CliOptions options,
        out string? error)
    {
        options = new CliOptions(arguments[0].ToLowerInvariant(), null, null, null);
        error = null;

        if (options.Command is not ("status" or "configure" or "setup"))
        {
            error = $"Comando desconhecido: {arguments[0]}.";
            return false;
        }

        for (var index = 1; index < arguments.Length; index++)
        {
            var option = arguments[index];

            if (index + 1 >= arguments.Length)
            {
                error = $"A opção {option} exige um valor.";
                return false;
            }

            var value = arguments[++index];

            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"A opção {option} exige um valor válido.";
                return false;
            }

            options = option switch
            {
                "--rustdesk-path" => options with { RustDeskPath = value },
                "--log-directory" => options with { LogDirectory = value },
                "--installer-path" => options with { InstallerPath = value },
                _ => options
            };

            if (option is not ("--rustdesk-path" or "--log-directory" or "--installer-path"))
            {
                error = $"Opção desconhecida: {option}.";
                return false;
            }
        }

        if (options.Command == "setup" && options.RustDeskPath is not null)
        {
            error = "A opção --rustdesk-path não é válida para o comando setup.";
            return false;
        }

        if (options.Command != "setup" && options.InstallerPath is not null)
        {
            error = "A opção --installer-path é válida somente para o comando setup.";
            return false;
        }

        if (options.Command == "status" && options.LogDirectory is not null)
        {
            error = "A opção --log-directory não é válida para o comando status.";
            return false;
        }

        return true;
    }

    private static bool IsHelpCommand(string value) =>
        value is "help" or "--help" or "-h";

    private static bool IsVersionCommand(string value) =>
        value is "--version" or "-v";

    private static string FormatVersion(Version? version) =>
        version?.ToString() ?? "versão desconhecida";

    private static void WriteHeader(TextWriter output)
    {
        output.WriteLine(ApplicationInfo.Name);
        output.WriteLine($"Versão: {ApplicationInfo.Version}");
        output.WriteLine();
    }

    private static void WriteUsage(TextWriter output)
    {
        output.WriteLine("Uso:");
        output.WriteLine("  technolife-rustdesk --version");
        output.WriteLine("  technolife-rustdesk status [--rustdesk-path <caminho>]");
        output.WriteLine(
            "  technolife-rustdesk configure [--rustdesk-path <caminho>] " +
            "[--log-directory <diretório>]");
        output.WriteLine(
            "  technolife-rustdesk setup [--installer-path <caminho>] " +
            "[--log-directory <diretório>]");
        output.WriteLine();
        output.WriteLine(
            "Somente os comandos explícitos 'configure' e 'setup' alteram o sistema.");
    }

    private sealed record CliOptions(
        string Command,
        string? RustDeskPath,
        string? LogDirectory,
        string? InstallerPath);
}
