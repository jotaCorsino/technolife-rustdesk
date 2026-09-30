using System.Drawing;

namespace Technolife.RustDesk.Windows;

public sealed class SetupMainForm : Form
{
    private static readonly Color BrandColor = Color.FromArgb(17, 73, 128);
    private static readonly Color SuccessColor = Color.FromArgb(24, 124, 70);
    private static readonly Color ErrorColor = Color.FromArgb(176, 44, 44);

    private readonly SetupFlowController _controller;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Label _statusLabel;
    private readonly Label _descriptionLabel;
    private readonly ProgressBar _progressBar;
    private readonly Button _finishButton;
    private readonly Button _retryButton;
    private readonly Button _closeButton;
    private bool _started;

    public SetupMainForm(SetupFlowController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        _controller = controller;

        Text = "Technolife - Configuração de Acesso Remoto";
        AccessibleName = Text;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        ClientSize = new Size(520, 340);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;

        var content = new TableLayoutPanel
        {
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Padding = new Padding(36, 28, 36, 28),
            RowCount = 7
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var brandLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = BrandColor,
            Text = "Technolife"
        };
        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 12, FontStyle.Regular),
            ForeColor = Color.FromArgb(55, 65, 81),
            Margin = new Padding(0, 3, 0, 0),
            Text = "Configuração de Acesso Remoto"
        };
        _statusLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            ForeColor = BrandColor,
            MaximumSize = new Size(440, 0),
            Text = "● Preparando a configuração..."
        };
        _descriptionLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            ForeColor = Color.FromArgb(75, 85, 99),
            MaximumSize = new Size(440, 0),
            Margin = new Padding(0, 14, 0, 0),
            Text = "Aguarde enquanto preparamos o acesso remoto deste computador."
        };
        _progressBar = new ProgressBar
        {
            AccessibleName = "Progresso da configuração",
            Dock = DockStyle.Top,
            Height = 10,
            MarqueeAnimationSpeed = 28,
            Margin = new Padding(0, 16, 0, 18),
            Style = ProgressBarStyle.Marquee
        };

        _finishButton = CreateButton("Concluir");
        _retryButton = CreateButton("Tentar novamente");
        _closeButton = CreateButton("Fechar");
        _finishButton.Click += (_, _) => Close();
        _closeButton.Click += (_, _) => Close();
        _retryButton.Click += RetryButtonOnClick;

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };
        actions.Controls.Add(_finishButton);
        actions.Controls.Add(_closeButton);
        actions.Controls.Add(_retryButton);

        content.Controls.Add(brandLabel, 0, 0);
        content.Controls.Add(titleLabel, 0, 1);
        content.Controls.Add(new Panel(), 0, 2);
        content.Controls.Add(_statusLabel, 0, 3);
        content.Controls.Add(_descriptionLabel, 0, 4);
        content.Controls.Add(_progressBar, 0, 5);
        content.Controls.Add(actions, 0, 6);
        Controls.Add(content);

        AcceptButton = _finishButton;
        _controller.StateChanged += ControllerOnStateChanged;
        ApplyState(_controller.State);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_started)
        {
            return;
        }

        _started = true;

        try
        {
            await _controller.StartAutomaticallyAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The window is closing.
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _controller.StateChanged -= ControllerOnStateChanged;
        _lifetime.Cancel();
        _lifetime.Dispose();
        base.OnFormClosed(e);
    }

    private static Button CreateButton(string text) =>
        new()
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            MinimumSize = new Size(112, 36),
            Padding = new Padding(8, 2, 8, 2),
            Text = text,
            UseVisualStyleBackColor = true,
            Visible = false
        };

    private async void RetryButtonOnClick(object? sender, EventArgs e)
    {
        try
        {
            await _controller.RetryAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The window is closing.
        }
    }

    private void ControllerOnStateChanged(SetupUiState state)
    {
        if (IsDisposed || Disposing)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => ApplyState(state)));
            return;
        }

        ApplyState(state);
    }

    private void ApplyState(SetupUiState state)
    {
        _progressBar.Visible = state.Status is SetupUiStatus.Running;
        _finishButton.Visible = state.Status is SetupUiStatus.Completed;
        _retryButton.Visible = state.Status is SetupUiStatus.Failed;
        _closeButton.Visible = state.Status is SetupUiStatus.Failed;

        switch (state.Status)
        {
            case SetupUiStatus.Completed:
                _statusLabel.ForeColor = SuccessColor;
                _statusLabel.Text = $"✓ {state.StatusText}";
                AcceptButton = _finishButton;
                _finishButton.Focus();
                break;

            case SetupUiStatus.Failed:
                _statusLabel.ForeColor = ErrorColor;
                _statusLabel.Text = state.StatusText;
                AcceptButton = _retryButton;
                _retryButton.Focus();
                break;

            default:
                _statusLabel.ForeColor = BrandColor;
                _statusLabel.Text = $"● {state.StatusText}";
                AcceptButton = null;
                break;
        }

        _descriptionLabel.Text = state.ErrorCode is null
            ? state.Description
            : $"{state.Description}{Environment.NewLine}{Environment.NewLine}" +
              $"Código: {state.ErrorCode}";
    }
}
