using PVZ.Core;

namespace PVZ.Player;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (EmbeddedTaskbook.TryLoadAppendedForRunningApp(out var document) && document != null)
                Application.Run(new MainForm(document));
            else
                Application.Run(new StudentStartForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "PVZ.Player", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
