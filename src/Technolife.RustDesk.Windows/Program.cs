namespace Technolife.RustDesk.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var controller = new SetupFlowController(new WindowsSetupWorkflowRunner());
        Application.Run(new SetupMainForm(controller));
    }
}
