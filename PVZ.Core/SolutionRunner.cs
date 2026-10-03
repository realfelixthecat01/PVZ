using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PVZ.Core;

public sealed class LanguageRuntimeInfo
{
    internal LanguageRuntimeInfo(
        SolutionLanguage language,
        string displayName,
        bool isAvailable,
        string status,
        string? executablePath = null,
        string? targetFramework = null)
    {
        Language = language;
        DisplayName = displayName;
        IsAvailable = isAvailable;
        Status = status;
        ExecutablePath = executablePath;
        TargetFramework = targetFramework;
    }

    public SolutionLanguage Language { get; }
    public string DisplayName { get; }
    public bool IsAvailable { get; }
    public string Status { get; }
    internal string? ExecutablePath { get; }
    internal string? TargetFramework { get; }

    public override string ToString() => $"{DisplayName} — {Status}";
}

public sealed class SolutionTestRun
{
    public bool Success { get; init; }
    public string StandardOutput { get; init; } = "";
    public string StandardError { get; init; } = "";
    public string? ErrorMessage { get; init; }
}

public sealed class SolutionBatchRun
{
    public bool RuntimeAvailable { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<SolutionTestRun> Tests { get; init; } = Array.Empty<SolutionTestRun>();
}

public static class SolutionRunner
{
    private static readonly TimeSpan RuntimeProbeTimeout = TimeSpan.FromSeconds(3);
    private const int TestTimeoutMilliseconds = 15000;
    private const int CompileTimeoutMilliseconds = 60000;

    public static IReadOnlyList<LanguageRuntimeInfo> DetectRuntimes()
    {
        var runtimes = new List<LanguageRuntimeInfo>
        {
            DetectPython(),
            DetectCpp(),
            DetectCSharp(),
            DetectPascal(),
            new(
                SolutionLanguage.OneC,
                "1C",
                false,
                "выполнение 1C:Предприятия не поддерживается"),
        };
        return runtimes;
    }

    public static async Task<SolutionBatchRun> RunAllAsync(
        LanguageRuntimeInfo runtime,
        string code,
        IReadOnlyList<string> inputs)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(inputs);
        if (!runtime.IsAvailable || string.IsNullOrWhiteSpace(runtime.ExecutablePath))
        {
            return new SolutionBatchRun
            {
                RuntimeAvailable = false,
                ErrorMessage = runtime.Status,
            };
        }

        if (inputs.Count == 0)
        {
            return new SolutionBatchRun
            {
                RuntimeAvailable = true,
                Tests = Array.Empty<SolutionTestRun>(),
            };
        }

        var tempDirectory = Path.Combine(Path.GetTempPath(), "pvz-solutions", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var prepared = await PrepareAsync(runtime, code, tempDirectory);
            if (prepared.ErrorMessage != null)
            {
                return new SolutionBatchRun
                {
                    RuntimeAvailable = true,
                    ErrorMessage = prepared.ErrorMessage,
                    Tests = inputs.Select(_ => new SolutionTestRun
                    {
                        Success = false,
                        ErrorMessage = prepared.ErrorMessage,
                        StandardOutput = prepared.StandardOutput,
                        StandardError = prepared.StandardError,
                    }).ToArray(),
                };
            }

            var results = new List<SolutionTestRun>(inputs.Count);
            foreach (var input in inputs)
            {
                var result = await RunProcessAsync(
                    prepared.ExecutablePath!,
                    prepared.Arguments,
                    tempDirectory,
                    input,
                    TestTimeoutMilliseconds);
                results.Add(new SolutionTestRun
                {
                    Success = result.ExitCode == 0 && result.ErrorMessage == null,
                    StandardOutput = result.StandardOutput,
                    StandardError = result.StandardError,
                    ErrorMessage = result.ErrorMessage ??
                                   (result.ExitCode == 0 ? null : $"Код завершения: {result.ExitCode}"),
                });
            }

            return new SolutionBatchRun
            {
                RuntimeAvailable = true,
                Tests = results,
            };
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Temporary files are isolated and can be removed by the operating system later.
            }
        }
    }

    private static async Task<PreparedCommand> PrepareAsync(
        LanguageRuntimeInfo runtime,
        string code,
        string tempDirectory)
    {
        var sourceName = runtime.Language switch
        {
            SolutionLanguage.Cpp => "solution.cpp",
            SolutionLanguage.CSharp => "Solution.cs",
            SolutionLanguage.Python => "solution.py",
            SolutionLanguage.Pascal => "solution.pas",
            _ => throw new InvalidOperationException("Для выбранного языка нет адаптера выполнения."),
        };
        var sourcePath = Path.Combine(tempDirectory, sourceName);
        await File.WriteAllTextAsync(sourcePath, code, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        switch (runtime.Language)
        {
            case SolutionLanguage.Python:
                return new PreparedCommand(runtime.ExecutablePath!, [sourcePath], null, "", "");

            case SolutionLanguage.Cpp:
            {
                var executablePath = Path.Combine(tempDirectory, "solution.exe");
                var compile = await RunProcessAsync(
                    runtime.ExecutablePath!,
                    ["-std=c++17", "-O2", sourcePath, "-o", executablePath],
                    tempDirectory,
                    "",
                    CompileTimeoutMilliseconds);
                return compile.ExitCode == 0 && compile.ErrorMessage == null
                    ? new PreparedCommand(executablePath, Array.Empty<string>(), null, "", "")
                    : new PreparedCommand("", Array.Empty<string>(), BuildError("C++", compile), compile.StandardOutput, compile.StandardError);
            }

            case SolutionLanguage.Pascal:
            {
                var executablePath = Path.Combine(tempDirectory, "solution.exe");
                var compile = await RunProcessAsync(
                    runtime.ExecutablePath!,
                    [sourcePath, $"-o{executablePath}"],
                    tempDirectory,
                    "",
                    CompileTimeoutMilliseconds);
                return compile.ExitCode == 0 && compile.ErrorMessage == null
                    ? new PreparedCommand(executablePath, Array.Empty<string>(), null, "", "")
                    : new PreparedCommand("", Array.Empty<string>(), BuildError("Pascal", compile), compile.StandardOutput, compile.StandardError);
            }

            case SolutionLanguage.CSharp:
            {
                var framework = runtime.TargetFramework
                    ?? throw new InvalidOperationException("Не определена целевая версия .NET для C#.");
                var projectPath = Path.Combine(tempDirectory, "Solution.csproj");
                var outputDirectory = Path.Combine(tempDirectory, "build");
                var project = $"""
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <OutputType>Exe</OutputType>
                        <TargetFramework>{framework}</TargetFramework>
                        <ImplicitUsings>enable</ImplicitUsings>
                        <Nullable>enable</Nullable>
                        <NuGetAudit>false</NuGetAudit>
                        <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
                      </PropertyGroup>
                    </Project>
                    """;
                await File.WriteAllTextAsync(projectPath, project, Encoding.UTF8);
                var compile = await RunProcessAsync(
                    runtime.ExecutablePath!,
                    ["build", projectPath, "--configuration", "Release", "--output", outputDirectory, "--nologo", "--verbosity", "quiet", "-p:NuGetAudit=false"],
                    tempDirectory,
                    "",
                    CompileTimeoutMilliseconds);
                if (compile.ExitCode != 0 || compile.ErrorMessage != null)
                    return new PreparedCommand("", Array.Empty<string>(), BuildError("C#", compile), compile.StandardOutput, compile.StandardError);

                return new PreparedCommand(
                    runtime.ExecutablePath!,
                    [Path.Combine(outputDirectory, "Solution.dll")],
                    null,
                    "",
                    "");
            }

            default:
                throw new InvalidOperationException("Для выбранного языка нет адаптера выполнения.");
        }
    }

    private static string BuildError(string language, ProcessResult result)
    {
        var details = string.Join(
            Environment.NewLine,
            new[] { result.ErrorMessage, result.StandardError, result.StandardOutput }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
        return $"Не удалось скомпилировать решение на языке {language}." +
               (details.Length == 0 ? "" : Environment.NewLine + details);
    }

    private static LanguageRuntimeInfo DetectPython()
    {
        foreach (var name in ExecutableNames("python", "python3"))
        {
            var path = FindExecutable(name);
            if (path == null)
                continue;
            if (path.Contains(
                    Path.Combine("Microsoft", "WindowsApps", Path.GetFileName(path)),
                    StringComparison.OrdinalIgnoreCase))
                continue;
            if (TryProbe(path, ["--version"], out var version))
                return new LanguageRuntimeInfo(SolutionLanguage.Python, "Python", true, version, path);
        }

        return new LanguageRuntimeInfo(
            SolutionLanguage.Python,
            "Python",
            false,
            "интерпретатор Python не найден или не запускается");
    }

    private static LanguageRuntimeInfo DetectCpp()
    {
        foreach (var name in ExecutableNames("g++", "clang++"))
        {
            var path = FindExecutable(name);
            if (path == null || !TryProbe(path, ["--version"], out var version))
                continue;

            return new LanguageRuntimeInfo(SolutionLanguage.Cpp, "C++", true, version, path);
        }

        return new LanguageRuntimeInfo(
            SolutionLanguage.Cpp,
            "C++",
            false,
            "компилятор g++ или clang++ не найден в PATH");
    }

    private static LanguageRuntimeInfo DetectCSharp()
    {
        var dotnet = FindExecutable(OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        if (dotnet == null || !TryProbe(dotnet, ["--list-sdks"], out var sdkList))
        {
            return new LanguageRuntimeInfo(
                SolutionLanguage.CSharp,
                "C#",
                false,
                "SDK .NET не найден в PATH");
        }

        var sdkMajor = sdkList
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(version => version != null)
            .Select(version => int.TryParse(version!.Split('.')[0], out var major) ? major : 0)
            .DefaultIfEmpty(0)
            .Max();
        if (sdkMajor < 6)
        {
            return new LanguageRuntimeInfo(
                SolutionLanguage.CSharp,
                "C#",
                false,
                "нужен установленный SDK .NET версии 6 или новее");
        }

        return new LanguageRuntimeInfo(
            SolutionLanguage.CSharp,
            "C#",
            true,
            $"SDK .NET {sdkMajor}",
            dotnet,
            $"net{sdkMajor}.0");
    }

    private static LanguageRuntimeInfo DetectPascal()
    {
        var path = FindExecutable(OperatingSystem.IsWindows() ? "fpc.exe" : "fpc");
        if (path != null && TryProbe(path, ["-iV"], out var version))
            return new LanguageRuntimeInfo(SolutionLanguage.Pascal, "Pascal", true, $"Free Pascal {version}", path);

        return new LanguageRuntimeInfo(SolutionLanguage.Pascal, "Pascal", false, "компилятор Free Pascal (fpc) не найден");
    }

    private static IEnumerable<string> ExecutableNames(params string[] names)
    {
        var extension = OperatingSystem.IsWindows() ? ".exe" : "";
        return names.Select(name => name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? name : name + extension);
    }

    private static string? FindExecutable(string name)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var candidate = Path.Combine(directory.Trim('"'), name);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static bool TryProbe(string executablePath, IReadOnlyList<string> arguments, out string version)
    {
        var result = RunProcessAsync(executablePath, arguments, Environment.CurrentDirectory, "", (int)RuntimeProbeTimeout.TotalMilliseconds)
            .GetAwaiter()
            .GetResult();
        version = result.StandardOutput.Trim();
        if (version.Length == 0)
            version = result.StandardError.Trim();
        if (version.Length > 80)
            version = version[..80];
        return result.ExitCode == 0 && result.ErrorMessage == null && version.Length > 0;
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        string standardInput,
        int timeoutMilliseconds)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                return new ProcessResult(-1, "", "", $"Не удалось запустить программу: {executablePath}");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or IOException or UnauthorizedAccessException)
        {
            return new ProcessResult(-1, "", "", $"Не удалось запустить «{executablePath}»: {ex.Message}");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        try
        {
            if (!string.IsNullOrEmpty(standardInput))
            {
                await process.StandardInput.WriteAsync(standardInput);
                if (!standardInput.EndsWith('\n'))
                    await process.StandardInput.WriteAsync("\n");
            }
            process.StandardInput.Close();
        }
        catch (IOException ex)
        {
            TryKill(process);
            return new ProcessResult(-1, "", "", $"Не удалось передать входные данные: {ex.Message}");
        }

        using var cancellation = new CancellationTokenSource(timeoutMilliseconds);
        try
        {
            await process.WaitForExitAsync(cancellation.Token);
            return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask, null);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            return new ProcessResult(-1, await stdoutTask, await stderrTask, "Превышено время выполнения.");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // The process may already have exited.
        }
    }

    private sealed record PreparedCommand(
        string? ExecutablePath,
        IReadOnlyList<string> Arguments,
        string? ErrorMessage,
        string StandardOutput,
        string StandardError);

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, string? ErrorMessage);
}
