using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace PVZS;

/// <summary>
/// Админские операции над самим сервером: запуск произвольного приложения и обновление
/// с сайта. Доступно только администратору через <c>/api/admin/*</c>.
/// ВНИМАНИЕ: запуск произвольного exe — это удалённое выполнение кода; держите админ-токен в секрете.
/// </summary>
public sealed class ServerControl
{
    private readonly DownloadCatalog _downloads;

    public ServerControl(DownloadCatalog downloads) => _downloads = downloads;

    /// <summary>Куда складываются загруженные и скачанные для запуска файлы.</summary>
    public string WorkRoot => Path.Combine(_downloads.DirectoryPath, "_control");

    public string Version =>
        (typeof(ServerControl).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ServerControl).Assembly.GetName().Version?.ToString()
            ?? "0.0.0").Split('+')[0];

    public string ProcessPath => Environment.ProcessPath ?? AppContext.BaseDirectory;

    /// <summary>Сохраняет загруженный файл в папку запуска и возвращает путь.</summary>
    public (string Path, string? Error) SaveUpload(string fileName, byte[] content)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName))
            return ("", "Не задано имя файла.");
        if (content.Length == 0)
            return ("", "Файл пустой.");
        if (content.Length > DownloadCatalog.MaximumControlUploadBytes)
            return ("", $"Файл больше {DownloadCatalog.MaximumControlUploadBytes / (1024 * 1024)} МиБ.");

        try
        {
            Directory.CreateDirectory(WorkRoot);
            var path = Path.Combine(WorkRoot, safeName);
            File.WriteAllBytes(path, content);
            return (path, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ("", $"Не удалось сохранить файл: {exception.Message}");
        }
    }

    /// <summary>Скачивает файл по URL в папку запуска (для обновления).</summary>
    public async Task<(string Path, string? Error)> DownloadAsync(
        string url,
        string? expectedSha256,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return ("", "Нужен http(s)-адрес файла.");

        try
        {
            Directory.CreateDirectory(WorkRoot);
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var fileName = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "download.bin";
            var path = Path.Combine(WorkRoot, fileName);
            var temporary = path + ".tmp";

            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = File.Create(temporary))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > DownloadCatalog.MaximumControlUploadBytes)
                    {
                        await output.DisposeAsync();
                        File.Delete(temporary);
                        return ("", "Файл слишком большой.");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            File.Move(temporary, path, overwrite: true);
            if (!string.IsNullOrWhiteSpace(expectedSha256))
            {
                var actual = await ComputeSha256Async(path, cancellationToken);
                if (!actual.Equals(expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                    return ("", $"SHA-256 не совпал. Ожидался {expectedSha256.Trim()}, получен {actual}.");
            }

            return (path, null);
        }
        catch (HttpRequestException exception)
        {
            return ("", $"Не удалось скачать: {exception.Message}");
        }
        catch (TaskCanceledException)
        {
            return ("", "Скачивание прервано по времени.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ("", $"Не удалось сохранить файл: {exception.Message}");
        }
    }

    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Запускает файл и ждёт завершения, собирая вывод (не более <paramref name="timeout"/>).</summary>
    public async Task<ExecResultDto> RunAsync(
        string executablePath,
        string? arguments,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null)
    {
        if (!File.Exists(executablePath))
            return new ExecResultDto(-1, $"Файл не найден: {executablePath}", false);

        var startInfo = new ProcessStartInfo(executablePath)
        {
            Arguments = arguments ?? string.Empty,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var output = new StringBuilder();
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => Append(output, e.Data);
        process.ErrorDataReceived += (_, e) => Append(output, e.Data);
        try
        {
            if (!process.Start())
                return new ExecResultDto(-1, "Не удалось запустить процесс.", false);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new ExecResultDto(-1, $"Не удалось запустить: {exception.Message}", false);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout ?? TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }

            return new ExecResultDto(-1, Truncate(output.ToString()), true);
        }

        process.WaitForExit();
        return new ExecResultDto(process.ExitCode, Truncate(output.ToString()), false);
    }

    private static void Append(StringBuilder output, string? line)
    {
        if (line is null)
            return;
        lock (output)
        {
            if (output.Length < DownloadCatalog.MaximumControlOutputChars)
                output.AppendLine(line);
        }
    }

    private static string Truncate(string text) =>
        text.Length <= DownloadCatalog.MaximumControlOutputChars
            ? text
            : text[..DownloadCatalog.MaximumControlOutputChars] + "\n…(вывод обрезан)";
}

public sealed record ExecResultDto(int ExitCode, string Output, bool TimedOut);

public sealed record RunPathRequest(string? Path, string? Arguments);

public sealed record UpdateRequest(string? Url, string? Sha256, string? Arguments, bool Run = true);

public sealed record ServerInfoDto(
    string Version,
    string ProcessPath,
    string DataDirectory,
    string CatalogDirectory,
    string DownloadsDirectory,
    int CatalogCollections);
