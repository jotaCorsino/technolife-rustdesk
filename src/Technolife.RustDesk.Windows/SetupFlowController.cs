using Technolife.RustDesk.Core.Enums;
using Technolife.RustDesk.Core.Models;

namespace Technolife.RustDesk.Windows;

public sealed class SetupFlowController
{
    private const string WaitingDescription =
        "Aguarde enquanto preparamos o acesso remoto deste computador.";
    private const string FailureDescription =
        "Tente novamente. Se o problema continuar, entre em contato com o suporte da Technolife.";

    private readonly ISetupWorkflowRunner _runner;
    private bool _isRunning;

    public SetupFlowController(ISetupWorkflowRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
    }

    public SetupUiState State { get; private set; } = new(
        SetupUiStatus.Ready,
        "Preparando a configuração...",
        WaitingDescription);

    public event Action<SetupUiState>? StateChanged;

    public Task StartAutomaticallyAsync(CancellationToken cancellationToken = default) =>
        RunAsync(cancellationToken);

    public Task RetryAsync(CancellationToken cancellationToken = default)
    {
        if (State.Status is not SetupUiStatus.Failed)
        {
            throw new InvalidOperationException(
                "A new attempt is only available after a failed setup.");
        }

        return RunAsync(cancellationToken);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("The setup workflow is already running.");
        }

        _isRunning = true;
        SetState(RunningState("Verificando o RustDesk..."));

        try
        {
            var progress = new CallbackProgress<SetupProgress>(OnProgress);
            var result = await _runner
                .RunAsync(progress, cancellationToken)
                .ConfigureAwait(false);

            if (result.Success && result.Value is not null)
            {
                SetState(new SetupUiState(
                    SetupUiStatus.Completed,
                    "Configuração concluída com sucesso",
                    "Este computador já está preparado para o acesso remoto da Technolife."));
                return;
            }

            SetState(CreateFailureState(result.ErrorCode));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            SetState(CreateFailureState(ErrorCode.UnexpectedFailure));
        }
        finally
        {
            _isRunning = false;
        }
    }

    private void OnProgress(SetupProgress progress)
    {
        var state = progress.Stage switch
        {
            SetupProgressStage.Checking => RunningState("Verificando o RustDesk..."),
            SetupProgressStage.Downloading => RunningState("Baixando o RustDesk..."),
            SetupProgressStage.Installing => RunningState("Instalando o RustDesk..."),
            SetupProgressStage.StartingService => RunningState("Ativando acesso remoto..."),
            SetupProgressStage.Configuring => RunningState("Configurando acesso remoto..."),
            SetupProgressStage.Verifying => RunningState("Validando configuração..."),
            _ => null
        };

        if (state is not null)
        {
            SetState(state);
        }
    }

    private static SetupUiState RunningState(string statusText) =>
        new(SetupUiStatus.Running, statusText, WaitingDescription);

    private static SetupUiState CreateFailureState(ErrorCode errorCode)
    {
        var guidance = errorCode switch
        {
            ErrorCode.ElevationFailed =>
                "A instalação não foi autorizada. Tente novamente e confirme a solicitação do Windows.",
            ErrorCode.DownloadFailed =>
                "Não foi possível baixar o RustDesk. Verifique a conexão com a internet e tente novamente.",
            ErrorCode.ChecksumMismatch =>
                "O arquivo de instalação não pôde ser validado. Tente novamente mais tarde.",
            _ => FailureDescription
        };

        return new SetupUiState(
            SetupUiStatus.Failed,
            "Não foi possível concluir a configuração.",
            $"{guidance}{Environment.NewLine}{Environment.NewLine}" +
            "Informações técnicas foram registradas para o suporte.",
            FormatSupportCode(errorCode));
    }

    private static string FormatSupportCode(ErrorCode errorCode) =>
        $"TL-WIN-{(int)errorCode:00}";

    private void SetState(SetupUiState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
