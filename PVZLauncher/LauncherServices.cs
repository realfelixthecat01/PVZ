using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace PVZ.Launcher;

/// <summary>Запускает локальный сервер PVZS (127.0.0.1:1729), если он ещё не работает.</summary>
internal sealed class ServerRunner
{
    public const int Port = 1729;

    /// <summary>Папка, в которой лежит сам exe: рядом с ней живут данные и распакованный сервер.</summary>
    public static string InstallDirectory =>
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

    /// <summary>Все данные: pvz.db, онлайн-задачники и работы учеников.</summary>
    public static string DataDirectory => Path.Combine(InstallDirectory, "sessions");

    /// <summary>Каталог готовых задачников (&lt;предмет&gt;\&lt;класс&gt;\*.pvz).</summary>
    public static string CatalogDirectory => Path.Combine(InstallDirectory, "zadachniki");

    private Process? _process;

    public bool StartedByUs => _process is { HasExited: false };

    public event Action? Exited;

    public static bool IsListening()
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(IPAddress.Loopback, Port);
            return task.Wait(TimeSpan.FromMilliseconds(400)) && client.Connected;
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException)
        {
            return false;
        }
    }

    public async Task<string?> EnsureAsync(Action<string> log)
    {
        if (IsListening())
        {
            log("Сервер уже работает (порт 1729).");
            return null;
        }

        var executable = InstalledExecutable() ?? ExtractServer(log) ?? FindExecutable();
        if (executable is null)
            return "Не найден PVZS.exe: положите его рядом с этим файлом или в подпапку server\\.";

        try
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(CatalogDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return $"Не удалось создать папку данных рядом с exe: {exception.Message}";
        }

        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{Port}";
        // Данные всегда рядом с этим exe, а не рядом с распакованным сервером.
        startInfo.Environment["PVZ_SERVER_DATA_DIR"] = DataDirectory;
        startInfo.Environment["PVZ_TASKBOOK_CATALOG_DIR"] = CatalogDirectory;

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.Exited += (_, _) => Exited?.Invoke();
        try
        {
            if (!_process.Start())
                return $"Не удалось запустить {Path.GetFileName(executable)}.";
        }
        catch (Exception exception)
        {
            return $"Не удалось запустить {Path.GetFileName(executable)}: {exception.Message}";
        }

        log($"Сервер запущен: {executable}");
        for (var attempt = 0; attempt < 45; attempt++)
        {
            if (IsListening())
            {
                log($"Сервер отвечает на http://127.0.0.1:{Port}.");
                return null;
            }

            if (_process.HasExited)
                return $"{Path.GetFileName(executable)} завершился сразу после запуска (код {_process.ExitCode}).";
            await Task.Delay(1000);
        }

        Stop();
        return $"Сервер не поднялся за 45 секунд — порт {Port} не отвечает.";
    }

    public void Stop()
    {
        var process = _process;
        _process = null;
        if (process is null)
            return;
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }

        process.Dispose();
    }

    /// <summary>Сервер, установленный установщиком в корень папки (Program Files (x86)\PVZ).</summary>
    private static string? InstalledExecutable()
    {
        var candidate = Path.Combine(InstallDirectory, "PVZS.exe");
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? FindExecutable()
    {        var configured = Environment.GetEnvironmentVariable("PVZS_EXE");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;
        return CandidateExecutables().FirstOrDefault(File.Exists);
    }

    private static IEnumerable<string> CandidateExecutables()
    {
        var install = InstallDirectory;
        var baseDirectory = AppContext.BaseDirectory;
        yield return Path.Combine(install, "PVZS.exe");
        yield return Path.Combine(install, "server", "PVZS.exe");
        yield return Path.Combine(baseDirectory, "PVZS.exe");
        yield return Path.Combine(baseDirectory, "server", "PVZS.exe");
        yield return @"C:\PVZS\server\PVZS.exe";
    }

    /// <summary>
    /// Достаёт вшитый в exe сервер в папку server\ рядом с лаунчером (один файл на диске —
    /// данные всё равно лежат в sessions\ и zadachniki\ рядом с лаунчером).
    /// </summary>
    private static string? ExtractServer(Action<string> log)
    {
        var template = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "ServerTemplate", "PVZS.exe"),
            Path.Combine(InstallDirectory, "ServerTemplate", "PVZS.exe"),
        }.FirstOrDefault(File.Exists);
        if (template is null)
            return null;

        var target = Path.Combine(InstallDirectory, "server", "PVZS.exe");
        try
        {
            if (!File.Exists(target) || File.GetLastWriteTimeUtc(template) > File.GetLastWriteTimeUtc(target))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(template, target, overwrite: true);
                log($"Сервер распакован: {target}");
            }

            return target;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            log($"Не удалось положить сервер рядом с exe ({exception.Message}) — запускаю из временной папки.");
            return template;
        }
    }
}

/// <summary>
/// Туннель к локальному серверу: постоянный (cloudflared-token.txt), быстрый Cloudflare
/// (trycloudflare.com) или запасной localhost.run по ssh — всё без проброса портов на роутере.
/// </summary>
internal sealed class TunnelRunner
{
    private static readonly Regex CloudflareUrlPattern = new(
        @"https://(?!(?:api|updates|region)\.)[a-z0-9\-]+\.trycloudflare\.com(?!/)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SshUrlPattern = new(
        @"https://[a-z0-9\-]+(?:\.[a-z0-9\-]+)*\.(?:lhr\.life|localhost\.run)(?![/\w-])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly TimeSpan UrlTimeout = TimeSpan.FromSeconds(60);

    private Process? _process;
    private TaskCompletionSource<string>? _urlSource;
    private string? _cloudflaredPath;

    public event Action? Exited;

    public async Task<string?> EnsureCloudflaredAsync(Action<string> log)
    {
        var bundled = Path.Combine(AppContext.BaseDirectory, "cloudflared.exe");
        var installed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PVZ",
            "cloudflared.exe");
        foreach (var candidate in new[] { bundled, installed })
        {
            if (!File.Exists(candidate))
                continue;
            _cloudflaredPath = candidate;
            log($"cloudflared: {candidate}");
            return null;
        }

        log("Скачиваю cloudflared с github.com/cloudflare/cloudflared…");
        try
        {
            await DownloadCloudflaredAsync(installed, log);
        }
        catch (Exception exception)
        {
            return $"Не удалось скачать cloudflared: {exception.Message}";
        }

        _cloudflaredPath = installed;
        log("cloudflared скачан и готов.");
        return null;
    }

    public async Task<string?> StartAsync(Action<string> log)
    {
        var permanent = ReadPermanentTunnel(log);
        if (permanent is not null)
            return await StartPermanentAsync(permanent.Value, log);

        if (_cloudflaredPath is not null)
        {
            var cloudflare = await StartCloudflareQuickAsync(log);
            if (cloudflare is not null)
                return cloudflare;
            log("Cloudflare не дал ссылку — пробую запасной туннель localhost.run (ssh).");
        }

        return await StartSshFallbackAsync(log);
    }

    private async Task<string?> StartPermanentAsync((string Token, string? Url) tunnel, Action<string> log)
    {
        if (_cloudflaredPath is null)
            return "Постоянный туннель требует cloudflared, но он не найден.";
        log("Постоянный туннель Cloudflare (cloudflared-token.txt).");
        if (!Launch(_cloudflaredPath, $"tunnel --no-autoupdate run --token {tunnel.Token}", log))
            return "Не удалось запустить cloudflared.";

        await Task.Delay(TimeSpan.FromSeconds(4));
        if (_process is { HasExited: true })
            return "cloudflared завершился сразу — проверьте токен в cloudflared-token.txt (подробности в логе).";
        if (string.IsNullOrEmpty(tunnel.Url))
        {
            Stop();
            return "В cloudflared-token.txt нет адреса: допишите во второй строке постоянный https-адрес туннеля.";
        }

        return tunnel.Url;
    }

    private async Task<string?> StartCloudflareQuickAsync(Action<string> log)
    {
        log("Запускаю туннель Cloudflare (trycloudflare.com)…");
        if (!Launch(_cloudflaredPath!, $"tunnel --url http://127.0.0.1:{ServerRunner.Port} --no-autoupdate", log))
            return null;

        var url = await WaitForUrlAsync(log);
        if (url is null)
        {
            log("Подсказка: сеть могла заблокировать api.trycloudflare.com (в логе ошибка запроса туннеля).");
            return null;
        }

        return url;
    }

    private async Task<string?> StartSshFallbackAsync(Action<string> log)
    {
        var ssh = FindSsh();
        if (ssh is null)
            return "Не удалось получить ссылку: Cloudflare недоступен, а ssh-клиент для запасного туннеля не найден.";
        log("Запасной туннель localhost.run (ssh)…");
        var arguments = "-T -o StrictHostKeyChecking=accept-new -o ServerAliveInterval=30 -o ServerAliveCountMax=3 "
            + $"-o ExitOnForwardFailure=yes -R 80:127.0.0.1:{ServerRunner.Port} nokey@localhost.run";
        if (!Launch(ssh, arguments, log))
            return "Не удалось запустить ssh для запасного туннеля.";

        var url = await WaitForUrlAsync(log);
        return url ?? "Ни Cloudflare, ни localhost.run не выдали ссылку за 60 секунд.";
    }

    private async Task<string?> WaitForUrlAsync(Action<string> log)
    {
        var urlSource = _urlSource;
        if (urlSource is null)
            return null;
        var completed = await Task.WhenAny(urlSource.Task, Task.Delay(UrlTimeout));
        if (completed != urlSource.Task)
        {
            Stop();
            return null;
        }

        var url = await urlSource.Task;
        if (string.IsNullOrEmpty(url))
        {
            Stop();
            return null;
        }

        return url;
    }

    private bool Launch(string fileName, string arguments, Action<string> log)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var urlSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, args) => HandleLine(args.Data, log, urlSource);
        process.ErrorDataReceived += (_, args) => HandleLine(args.Data, log, urlSource);
        process.Exited += (_, _) =>
        {
            if (!urlSource.Task.IsCompleted)
                urlSource.TrySetResult(string.Empty);
            Exited?.Invoke();
        };

        try
        {
            if (!process.Start())
            {
                process.Dispose();
                return false;
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            process.Dispose();
            return false;
        }

        _process = process;
        _urlSource = urlSource;
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return true;
    }

    private static string? FindSsh()
    {
        var system = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "OpenSSH",
            "ssh.exe");
        return File.Exists(system) ? system : "ssh";
    }

    public void Stop()
    {
        var process = _process;
        _process = null;
        _urlSource = null;
        if (process is null)
            return;
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }

        process.Dispose();
    }

    /// <summary>
    /// Постоянная ссылка без проброса портов: файл cloudflared-token.txt рядом с exe.
    /// Первая строка — токен туннеля Cloudflare, вторая (необязательно) — его постоянный адрес.
    /// </summary>
    private static (string Token, string? Url)? ReadPermanentTunnel(Action<string> log)
    {
        var path = Path.Combine(ServerRunner.InstallDirectory, "cloudflared-token.txt");
        if (!File.Exists(path))
            return null;

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            log($"Не удалось прочитать cloudflared-token.txt: {exception.Message}");
            return null;
        }

        var useful = lines
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();
        if (useful.Length == 0)
            return null;

        var token = useful[0];
        if (token.Length > 200 || !token.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.'))
        {
            log("cloudflared-token.txt: первая строка должна быть токеном туннеля Cloudflare.");
            return null;
        }

        var url = useful.Skip(1).FirstOrDefault(line =>
            Uri.TryCreate(line, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);
        return (token, url is null ? null : url.TrimEnd('/') + "/");
    }

    private static void HandleLine(string? line, Action<string> log, TaskCompletionSource<string> urlSource)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        if (line.Length > 240)
            line = line[..240] + "…";
        log(line);
        if (urlSource.Task.IsCompleted)
            return;

        var match = CloudflareUrlPattern.Match(line);
        if (!match.Success)
        {
            match = SshUrlPattern.Match(line);
            if (match.Success && line.Contains("admin.localhost.run", StringComparison.OrdinalIgnoreCase))
                return;
        }

        if (match.Success)
            urlSource.TrySetResult(match.Value);
    }

    private static async Task DownloadCloudflaredAsync(string destination, Action<string> log)
    {
        var url = Environment.Is64BitOperatingSystem
            ? "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe"
            : "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-386.exe";

        var directory = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(directory);
        var temporary = destination + ".tmp";

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using (var input = await response.Content.ReadAsStreamAsync())
        await using (var output = File.Create(temporary))
        {
            var buffer = new byte[81920];
            long read = 0;
            var lastPercent = -25;
            int count;
            while ((count = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, count));
                read += count;
                if (totalBytes <= 0)
                    continue;
                var percent = (int)(read * 100 / totalBytes);
                if (percent < lastPercent + 25)
                    continue;
                lastPercent = percent;
                log($"… {percent}%");
            }
        }

        File.Move(temporary, destination, overwrite: true);
        log($"Скачано: {destination}");
    }
}
