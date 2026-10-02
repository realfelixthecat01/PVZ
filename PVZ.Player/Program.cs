namespace PVZ.Player;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "PVZ.Player", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
