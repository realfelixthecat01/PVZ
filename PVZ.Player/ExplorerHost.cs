using System.Diagnostics;

namespace PVZ.Player;

internal static class ExplorerHost
{
    public static bool TryStop()
    {
        if (Process.GetProcessesByName("explorer").Length == 0)
            return false;

        try
        {
            // Do not use /T — it kills child processes (e.g. this app if started from Explorer).
            using var taskKill = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/F /IM explorer.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            taskKill?.WaitForExit(8000);
        }
        catch
        {
            // fall through to direct kill
        }

        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            try
            {
                process.Kill(entireProcessTree: false);
                process.WaitForExit(3000);
            }
            catch
            {
                // ignore per-process failures
            }
        }

        return Process.GetProcessesByName("explorer").Length == 0;
    }

    public static void Restart()
    {
        try
        {
            if (Process.GetProcessesByName("explorer").Length > 0)
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                UseShellExecute = true,
            });
        }
        catch
        {
            try
            {
                Process.Start("explorer.exe");
            }
            catch
            {
                // ignore
            }
        }
    }
}
