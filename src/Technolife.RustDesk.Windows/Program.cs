using Technolife.RustDesk.Core.Models;
using Technolife.RustDesk.Platforms.Processes;

namespace Technolife.RustDesk.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var elevationContext = new WindowsProcessElevationContext();

        if (!elevationContext.IsCurrentProcessElevated)
        {
            if (!TryRelaunchElevated(elevationContext))
            {
                MessageBox.Show(
                    "A autorização do Windows é necessária para ativar e configurar " +
                    "o acesso remoto. Abra o configurador novamente e clique em Sim.",
                    "Technolife - Autorização necessária",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return;
        }

        var controller = new SetupFlowController(new WindowsSetupWorkflowRunner());
        Application.Run(new SetupMainForm(controller));
    }

    private static bool TryRelaunchElevated(
        WindowsProcessElevationContext elevationContext)
    {
        var executablePath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var result = new SystemProcessRunner(elevationContext)
            .RunAsync(
                new ProcessRequest(
                    executablePath,
                    workingDirectory: AppContext.BaseDirectory,
                    requiresElevation: true))
            .GetAwaiter()
            .GetResult();

        return result.Success;
    }
}
