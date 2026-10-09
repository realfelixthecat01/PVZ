using System.Diagnostics;

namespace PVZ.Launcher;

/// <summary>
/// Устанавливает сервер ПВЗ в <c>C:\Program Files (x86)\PVZ</c>: сервер, клиенты,
/// папки данных и службу Windows PVZS, чтобы сервер работал всегда.
/// Требует прав администратора (см. app.manifest).
/// </summary>
internal static class Installer
{
    public const string ServiceName = "PVZS";
    public const string ServiceDisplayName = "Пункт Выдачи Задачек — сервер";

    public static string InstallDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PVZ");

    public static string InstalledServer => Path.Combine(InstallDirectory, "PVZS.exe");
    public static string InstalledLauncher => Path.Combine(InstallDirectory, "PVZLauncher.exe");
    public static string DownloadsDirectory => Path.Combine(InstallDirectory, "downloads");

    public static bool IsInstalled => File.Exists(InstalledServer) && File.Exists(InstalledLauncher);

    public static bool RunningFromInstall
    {
        get
        {
            var current = Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty);
            if (string.IsNullOrEmpty(current))
                return false;
            return string.Equals(
                Path.GetFullPath(current).TrimEnd('\\'),
                Path.GetFullPath(InstallDirectory).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Раскладывает файлы и ставит службу. Возвращает null при успехе.</summary>
    public static string? Install(Action<string> log)
    {
        try
        {
            Directory.CreateDirectory(InstallDirectory);
            foreach (var folder in new[] { "sessions", "zadachniki", "downloads" })
                Directory.CreateDirectory(Path.Combine(InstallDirectory, folder));

            if (!InstallServer(log))
                return "Не найден PVZS.exe для установки (нет вшитого сервера и файла рядом с лаунчером).";

            InstallClient("PVZP.exe", log);
            InstallClient("PVZT.exe", log);
            InstallSelf(log);
            InstallService(log);
            return null;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException
                or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return "Не удалось установить сервер: " + exception.Message;
        }
    }

    private static bool InstallServer(Action<string> log)
    {
        var template = Candidates("ServerTemplate", "PVZS.exe")
            .Concat(Candidates(null, "PVZS.exe"))
            .FirstOrDefault(File.Exists);
        if (template is null)
            return false;

        CopyIfNewer(template, InstalledServer);
        log("Сервер установлен: " + InstalledServer);
        return true;
    }

    private static void InstallClient(string fileName, Action<string> log)
    {
        var template = Candidates("ClientTemplate", fileName)
            .Concat(Candidates(null, fileName))
            .FirstOrDefault(File.Exists);
        if (template is null)
        {
            log($"Клиент {fileName} не найден — его можно положить в downloads\\ вручную.");
            return;
        }

        CopyIfNewer(template, Path.Combine(DownloadsDirectory, fileName));
        log($"Клиент {fileName} установлен в downloads\\.");
    }

    private static void InstallSelf(Action<string> log)
    {
        var self = Environment.ProcessPath;
        if (self is null || string.Equals(self, InstalledLauncher, StringComparison.OrdinalIgnoreCase))
            return;
        File.Copy(self, InstalledLauncher, overwrite: true);
        log("Лаунчер установлен: " + InstalledLauncher);
    }

    private static void InstallService(Action<string> log)
    {
        // Останавливаем ранее запущенный сервер, если он держит порт как процесс лаунчера.
        RunSc(["stop", ServiceName], out _);
        if (RunSc(["query", ServiceName], out _) == 0)
        {
            RunSc(["config", ServiceName, "binPath=", InstalledServer, "start=", "auto"], out var configError);
            if (!string.IsNullOrWhiteSpace(configError))
                log("sc config: " + configError.Trim());
        }
        else
        {
            RunSc(
                ["create", ServiceName, "binPath=", InstalledServer, "start=", "auto", "DisplayName=", ServiceDisplayName],
                out var createError);
            if (!string.IsNullOrWhiteSpace(createError))
                log("sc create: " + createError.Trim());
        }

        RunSc(["description", ServiceName, "Сервер ПВЗ: сайт, каталог задачников и обмен файлами."], out _);
        var startResult = RunSc(["start", ServiceName], out var startError);
        if (startResult == 0)
            log("Служба PVZS запущена и будет стартовать автоматически.");
        else if (!string.IsNullOrWhiteSpace(startError) && !startError.Contains("уже запущена", StringComparison.OrdinalIgnoreCase))
            log("Служба установлена, но не стартовала: " + startError.Trim());

        // Ждём, пока служба займёт порт: иначе перезапущенный лаунчер поднял бы второй сервер.
        for (var attempt = 0; attempt < 30 && !ServerRunner.IsListening(); attempt++)
            Thread.Sleep(500);
    }

    private static int RunSc(string[] arguments, out string error)
    {
        error = string.Empty;
        try
        {
            var startInfo = new ProcessStartInfo("sc.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo);
            if (process is null)
                return -1;
            var output = process.StandardOutput.ReadToEnd();
            error = process.StandardError.ReadToEnd();
            process.WaitForExit(15000);
            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(error))
                error = output;
            return process.ExitCode;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            error = exception.Message;
            return -1;
        }
    }

    private static void CopyIfNewer(string source, string destination)
    {
        if (File.Exists(destination) &&
            File.GetLastWriteTimeUtc(destination) >= File.GetLastWriteTimeUtc(source))
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
    }

    private static IEnumerable<string> Candidates(string? subfolder, string fileName)
    {
        var roots = new[]
        {
            AppContext.BaseDirectory,
            Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? string.Empty,
        };
        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root))
                continue;
            yield return subfolder is null
                ? Path.Combine(root, fileName)
                : Path.Combine(root, subfolder, fileName);
        }
    }
}
