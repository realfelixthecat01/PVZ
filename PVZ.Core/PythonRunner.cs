using System.Diagnostics;
using System.Text;

namespace PVZ.Core;

public sealed class PythonRunResult
{
    public bool Success { get; init; }
    public string StandardOutput { get; init; } = "";
    public string StandardError { get; init; } = "";
    public string? ErrorMessage { get; init; }
}

public static class PythonRunner
{
    public static async Task<PythonRunResult> RunAsync(string code, string stdin, int timeoutMs = 15000)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pvz-py", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var scriptPath = Path.Combine(tempDir, "solution.py");

        try
        {
            await File.WriteAllTextAsync(scriptPath, code, Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var process = new Process { StartInfo = psi };
            try
            {
                if (!process.Start())
                {
                    return new PythonRunResult
                    {
                        Success = false,
                        ErrorMessage = "Не удалось запустить python. Установите Python и добавьте его в PATH.",
                    };
                }
            }
            catch (Exception ex)
            {
                return new PythonRunResult
                {
                    Success = false,
                    ErrorMessage = $"Python не найден: {ex.Message}",
                };
            }

            if (!string.IsNullOrEmpty(stdin))
            {
                await process.StandardInput.WriteAsync(stdin);
                if (!stdin.EndsWith('\n'))
                    await process.StandardInput.WriteAsync("\n");
            }

            process.StandardInput.Close();

            using var cts = new CancellationTokenSource(timeoutMs);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

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
                    ErrorMessage = "Превышено время выполнения.",
                };
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return new PythonRunResult
            {
                Success = process.ExitCode == 0,
                StandardOutput = stdout,
                StandardError = stderr,
                ErrorMessage = process.ExitCode == 0 ? null : $"Код выхода: {process.ExitCode}",
            };
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

    public static string NormalizeOutput(string text) =>
        text.Replace("\r\n", "\n").TrimEnd('\n').Trim();
}
