// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace PVZ.Core;

public static class PascalAbcRunner
{
    private static readonly string[] CompilerExeNames = ["pabcnetc.exe", "PascalABCNETCompiler.exe"];
    private const int CompileTimeoutMs = 12_000;
    private const int RunTimeoutMs = 15_000;

    private static string? _cachedCompilerPath;

    public static async Task<PythonRunResult> RunAsync(string code, string stdin, int timeoutMs = 20000)
    {
        var runTimeout = timeoutMs > 0 ? timeoutMs : RunTimeoutMs;
        var compilerPath = ResolveCompilerPath();
        if (compilerPath == null)
        {
            return new PythonRunResult
            {
                Success = false,
                ErrorMessage =
                    "PascalABC.NET не установлен.\n" +
                    "Установите PascalABC.NET с https://pascalabc.net/ssyilki-dlya-skachivaniya " +
                    "(StandardPack или MiniPack) или добавьте pabcnetc.exe в PATH.",
            };
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "pvz-pas", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var sourcePath = Path.Combine(tempDir, "solution.pas");
        var exePath = Path.Combine(tempDir, "solution.exe");

        try
        {
            var source = PreparePascalSource(code);
            await File.WriteAllTextAsync(sourcePath, source, Encoding.UTF8);

            var compile = await TryRunProcessAsync(
                compilerPath,
                $"\"{sourcePath}\"",
                tempDir,
                CompileTimeoutMs,
                stdin: null,
                redirectOutput: false);
            if (!compile.Success)
            {
                return new PythonRunResult
                {
                    Success = false,
                    ErrorMessage = compile.ErrorMessage ?? BuildCompileHelpMessage(source),
                    StandardOutput = compile.StandardOutput,
                    StandardError = compile.StandardError,
                };
            }

            if (!File.Exists(exePath))
            {
                return new PythonRunResult
                {
                    Success = false,
                    ErrorMessage = BuildCompileHelpMessage(source),
                };
            }

            return await TryRunProcessAsync(exePath, "", tempDir, runTimeout, stdin, redirectOutput: true);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // ignore temp cleanup errors
            }
        }
    }

    internal static string? ResolveCompilerPath()
    {
        if (_cachedCompilerPath != null && File.Exists(_cachedCompilerPath))
            return _cachedCompilerPath;

        foreach (var dir in GetSearchDirectories())
        {
            foreach (var name in CompilerExeNames)
            {
                var path = Path.Combine(dir, name);
                if (File.Exists(path))
                {
                    _cachedCompilerPath = path;
                    return path;
                }
            }
        }

        return null;
    }

    internal static string PreparePascalSource(string code)
    {
        var text = code.Trim();
        if (text.Length == 0)
            return text;

        if (!Regex.IsMatch(text, @"^\s*program\b", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            text = "program solution;" + Environment.NewLine + text;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var result = new List<string>(lines.Length);
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                result.Add(line);
                continue;
            }

            if (trimmed.Equals("begin", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("end", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(line);
                continue;
            }

            if (!trimmed.EndsWith(';') && !trimmed.EndsWith('.'))
                line = line.TrimEnd() + ";";

            line = Regex.Replace(line, @"\bread\s*\(", "readln(", RegexOptions.IgnoreCase);
            result.Add(line);
        }

        text = string.Join(Environment.NewLine, result);
        if (!text.TrimEnd().EndsWith('.'))
            text = text.TrimEnd() + ".";

        return text;
    }

    private static string BuildCompileHelpMessage(string source) =>
        "Ошибка компиляции PascalABC.NET.\n" +
        "Проверьте синтаксис. Пример решения:\n" +
        "program solution;\n" +
        "var a: real;\n" +
        "begin\n" +
        "  readln(a);\n" +
        "  writeln(a + 1);\n" +
        "end.\n" +
        "---\n" +
        "Отправлено компилятору:\n" +
        source;

    private static IEnumerable<string> GetSearchDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in EnumerateSearchDirectories())
        {
            if (string.IsNullOrWhiteSpace(dir))
                continue;

            var full = Path.GetFullPath(dir.Trim());
            if (seen.Add(full))
                yield return full;
        }
    }

    private static IEnumerable<string> EnumerateSearchDirectories()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var segment in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            yield return segment.Trim();

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        yield return Path.Combine(programFiles, "PascalABC.NET");
        yield return Path.Combine(programFilesX86, "PascalABC.NET");

        var repoTools = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tools", "PascalABC"));
        if (Directory.Exists(repoTools))
            yield return repoTools;

        var localTools = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PVZ",
            "PascalABC");
        if (Directory.Exists(localTools))
            yield return localTools;
    }

    private static async Task<PythonRunResult> TryRunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        int timeoutMs,
        string? stdin,
        bool redirectOutput)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = redirectOutput && stdin != null,
            RedirectStandardOutput = redirectOutput,
            RedirectStandardError = redirectOutput,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        if (redirectOutput)
        {
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
        }

        using var process = new Process { StartInfo = psi };
        try
        {
            if (!process.Start())
            {
                return new PythonRunResult
                {
                    Success = false,
                    ErrorMessage = "Не удалось запустить PascalABC.NET.",
                };
            }
        }
        catch (Exception ex)
        {
            return new PythonRunResult
            {
                Success = false,
                ErrorMessage = $"PascalABC.NET не найден: {ex.Message}",
            };
        }

        if (redirectOutput && stdin != null)
        {
            if (!string.IsNullOrEmpty(stdin))
            {
                await process.StandardInput.WriteAsync(stdin);
                if (!stdin.EndsWith('\n'))
                    await process.StandardInput.WriteAsync("\n");
            }

            process.StandardInput.Close();
        }

        using var cts = new CancellationTokenSource(timeoutMs);

        Task<string> stdoutTask = Task.FromResult("");
        Task<string> stderrTask = Task.FromResult("");
        if (redirectOutput)
        {
            stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
        }

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { /* ignore */ }
            return new PythonRunResult
            {
                Success = false,
                ErrorMessage = "Превышено время выполнения (компиляция или запуск).",
            };
        }

        var stdout = redirectOutput ? await stdoutTask : "";
        var stderr = redirectOutput ? await stderrTask : "";

        return new PythonRunResult
        {
            Success = process.ExitCode == 0,
            StandardOutput = stdout,
            StandardError = stderr,
            ErrorMessage = process.ExitCode == 0 ? null : $"Код выхода: {process.ExitCode}",
        };
    }
}
