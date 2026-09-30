using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Windows;

namespace Technolife.RustDesk.Tests.Windows;

public sealed class SetupFlowControllerTests
{
    private const string SensitiveConfiguration = "sensitive-exported-configuration";

    [Fact]
    public async Task AutomaticStartupRunsSetupAndShowsCompletedState()
    {
        var runner = new FakeSetupWorkflowRunner(Success());
        var controller = new SetupFlowController(runner);

        await controller.StartAutomaticallyAsync();

        Assert.Equal(1, runner.CallCount);
        Assert.Equal(SetupUiStatus.Completed, controller.State.Status);
        Assert.Contains("concluída com sucesso", controller.State.StatusText);
    }

    [Fact]
    public async Task FailedWorkflowShowsFriendlyFailureState()
    {
        var runner = new FakeSetupWorkflowRunner(Failure(ErrorCode.DownloadFailed));
        var controller = new SetupFlowController(runner);

        await controller.StartAutomaticallyAsync();

        Assert.Equal(SetupUiStatus.Failed, controller.State.Status);
        Assert.Contains("Não foi possível", controller.State.StatusText);
        Assert.Equal("TL-WIN-04", controller.State.ErrorCode);
    }

    [Fact]
    public async Task RetryAfterFailureRunsWorkflowAgainAndCanComplete()
    {
        var runner = new FakeSetupWorkflowRunner(
            Failure(ErrorCode.InstallationFailed),
            Success());
        var controller = new SetupFlowController(runner);

        await controller.StartAutomaticallyAsync();
        await controller.RetryAsync();

        Assert.Equal(2, runner.CallCount);
        Assert.Equal(SetupUiStatus.Completed, controller.State.Status);
    }

    [Fact]
    public async Task UiMessagesNeverExposeExportedConfiguration()
    {
        var runner = new FakeSetupWorkflowRunner(
            OperationResult<RustDeskSetupWorkflowResult>.Failed(
                ErrorCode.ConfigurationFailed,
                $"Failure for {SensitiveConfiguration}.",
                $"Technical details for {SensitiveConfiguration}."));
        var controller = new SetupFlowController(runner);
        var states = new List<SetupUiState>();
        controller.StateChanged += states.Add;

        await controller.StartAutomaticallyAsync();

        var visibleText = string.Join(
            Environment.NewLine,
            states.Select(state =>
                $"{state.StatusText} {state.Description} {state.ErrorCode}"));
        Assert.DoesNotContain(SensitiveConfiguration, visibleText);
    }

    [Fact]
    public async Task ShowsFriendlyServiceAndVerificationProgress()
    {
        var controller = new SetupFlowController(new StageReportingRunner());
        var statusTexts = new List<string>();
        controller.StateChanged += state => statusTexts.Add(state.StatusText);

        await controller.StartAutomaticallyAsync();

        Assert.Contains("Ativando acesso remoto...", statusTexts);
        Assert.Contains("Validando configuração...", statusTexts);
        Assert.Equal(SetupUiStatus.Completed, controller.State.Status);
    }

    private static OperationResult<RustDeskSetupWorkflowResult> Success()
    {
        var installation = RustDeskInstallation.CreateFound(
            @"C:\Program Files\RustDesk\RustDesk.exe",
            new Version(1, 4, 9),
            new PlatformInfo(PlatformKind.Windows, CpuArchitecture.X64));
        var validation = new RustDeskValidation(
            RustDeskValidationStatus.Applied,
            "Configuration applied.");
        var configurationResult = new RustDeskConfigurationWorkflowResult(
            installation,
            validation,
            @"C:\ProgramData\Technolife\RustDeskConfigurator\logs\test.log");

        return OperationResult<RustDeskSetupWorkflowResult>.Succeeded(
            new RustDeskSetupWorkflowResult(configurationResult, installationPerformed: false),
            "Setup completed.");
    }

    private static OperationResult<RustDeskSetupWorkflowResult> Failure(
        ErrorCode errorCode) =>
        OperationResult<RustDeskSetupWorkflowResult>.Failed(
            errorCode,
            "Setup failed.",
            "Technical details.");

    private sealed class FakeSetupWorkflowRunner(
        params OperationResult<RustDeskSetupWorkflowResult>[] results)
        : ISetupWorkflowRunner
    {
        private readonly Queue<OperationResult<RustDeskSetupWorkflowResult>> _results =
            new(results);

        public int CallCount { get; private set; }

        public Task<OperationResult<RustDeskSetupWorkflowResult>> RunAsync(
            IProgress<SetupProgress> progress,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            progress.Report(new SetupProgress(SetupProgressStage.Checking));
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class StageReportingRunner : ISetupWorkflowRunner
    {
        public Task<OperationResult<RustDeskSetupWorkflowResult>> RunAsync(
            IProgress<SetupProgress> progress,
            CancellationToken cancellationToken = default)
        {
            progress.Report(new SetupProgress(SetupProgressStage.StartingService));
            progress.Report(new SetupProgress(SetupProgressStage.Verifying));
            return Task.FromResult(Success());
        }
    }
}
