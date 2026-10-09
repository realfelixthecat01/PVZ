using System.Diagnostics;

namespace PVZ.Launcher;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // По умолчанию лаунчер сам устанавливает сервер в Program Files (x86)\PVZ и перезапускается оттуда.
        // --no-install (или PVZ_SKIP_INSTALL=1) — запуститься на месте, без установки (удобно для отладки).
        var skipInstall = args.Any(arg => string.Equals(arg, "--no-install", StringComparison.OrdinalIgnoreCase))
            || string.Equals(Environment.GetEnvironmentVariable("PVZ_SKIP_INSTALL"), "1", StringComparison.Ordinal);

        if (!skipInstall && !Installer.RunningFromInstall)
        {
            var error = Installer.Install(_ => { });
            if (error is null && Installer.IsInstalled)
            {
                Process.Start(new ProcessStartInfo(Installer.InstalledLauncher)
                {
                    WorkingDirectory = Installer.InstallDirectory,
                    UseShellExecute = true,
                    Arguments = "--start",
                });
                return;
            }

            if (error is not null)
                MessageBox.Show(error, "Установка ПВЗ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        var form = new LauncherForm();
        // PVZLauncher.exe --start — сразу поднять сервер и туннель, не нажимая кнопку.
        if (args.Any(arg => string.Equals(arg, "--start", StringComparison.OrdinalIgnoreCase)))
            form.AutoStart();
        Application.Run(form);
    }
}
