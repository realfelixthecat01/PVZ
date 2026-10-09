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
            TaskbookUserFolders.EnsureCreated();
            if (EmbeddedTaskbook.TryLoadAppendedForRunningApp(out var document) && document != null)
                Application.Run(new MainForm(document));
            else
                Application.Run(new StudentStartForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "PVZP", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
