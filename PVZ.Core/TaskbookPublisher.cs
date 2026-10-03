namespace PVZ.Core;

public sealed class TaskbookPublishResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? OutputExePath { get; init; }
    public string? OutputPath { get; init; }
}

public static class TaskbookPublisher
{
    public static string TemplateRelativePath => Path.Combine("PlayerTemplate", "PVZ.Player.exe");

    public static TaskbookDocument CreateDocument(
        IEnumerable<TaskbookTaskDto> tasks,
        string teacherPassword,
        bool sequentialUnlock,
        bool topMost,
        bool fullScreen,
        bool killExplorer,
        TaskbookCompilerFlags allowedCompilers = TaskbookCompilerFlags.All)
    {
        var (hash, salt) = PasswordUtil.CreateHash(teacherPassword);
        if (allowedCompilers == TaskbookCompilerFlags.None)
            allowedCompilers = TaskbookCompilerFlags.All;

        return new TaskbookDocument
        {
            TeacherPasswordHash = hash,
            TeacherPasswordSalt = salt,
            SequentialUnlock = sequentialUnlock,
            TopMost = topMost,
            FullScreen = fullScreen,
            KillExplorer = killExplorer,
            AllowedCompilers = allowedCompilers,
            Tasks = tasks.ToList(),
        };
    }

    public static Task<TaskbookPublishResult> PublishExeAsync(
        string outputExePath,
        TaskbookDocument document,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => PublishExe(outputExePath, document, progress), cancellationToken);
    }

    public static TaskbookPublishResult PublishExe(
        string outputExePath,
        TaskbookDocument document,
        IProgress<string>? progress = null)
    {
        var templatePath = FindPlayerTemplate();
        if (templatePath == null)
        {
            return new TaskbookPublishResult
            {
                Success = false,
                ErrorMessage =
                    "Не найден шаблон PVZ.Player.exe.\n" +
                    "Положите файл в папку PlayerTemplate рядом с программой редактора\n" +
                    "или пересоберите решение (сначала PVZ.Player, затем PVZ).",
            };
        }

        progress?.Report("Создание задачника…");

        var targetDir = Path.GetDirectoryName(outputExePath);
        if (!string.IsNullOrEmpty(targetDir))
            Directory.CreateDirectory(targetDir);

        try
        {
            File.Copy(templatePath, outputExePath, true);
            TaskbookPayload.WriteAppended(outputExePath, document);
        }
        catch (Exception ex)
        {
            return new TaskbookPublishResult
            {
                Success = false,
                ErrorMessage = $"Не удалось создать задачник: {ex.Message}",
            };
        }

        return new TaskbookPublishResult
        {
            Success = true,
            OutputExePath = outputExePath,
        };
    }

    public static TaskbookPublishResult PublishPackage(string outputPath, TaskbookDocument document)
    {
        try
        {
            TaskbookPackageIO.Save(outputPath, document);
            return new TaskbookPublishResult
            {
                Success = true,
                OutputPath = outputPath,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return new TaskbookPublishResult
            {
                Success = false,
                ErrorMessage = $"Не удалось сохранить файл задачника: {ex.Message}",
            };
        }
    }

    public static TaskbookPublishResult PrepareOnlineUploadPackage(string outputPath, TaskbookDocument document) =>
        PublishPackage(outputPath, document);

    public static string? FindPlayerTemplate()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, TemplateRelativePath),
            Path.Combine(baseDir, "PVZ.Player.exe"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
