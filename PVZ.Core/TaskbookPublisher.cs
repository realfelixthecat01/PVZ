namespace PVZ.Core;

public sealed class TaskbookPublishResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? OutputExePath { get; init; }
    public string? OutputPath { get; init; }
    public string? SessionId { get; init; }
    public string? AccessToken { get; init; }
    public DateTimeOffset? TokenExpiresAt { get; init; }
    public RetentionPeriod Retention { get; init; } = RetentionPeriod.TwoWeeks;
    public bool Unauthorized { get; init; }
}

public static class TaskbookPublisher
{
    public static string TemplateRelativePath => Path.Combine("PlayerTemplate", "PVZP.exe");

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
                    "Не найден шаблон PVZP.exe.\n" +
                    "Положите файл в папку PlayerTemplate рядом с программой редактора\n" +
                    "или пересоберите решение (сначала PVZP, затем PVZT).",
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

    public static async Task<TaskbookPublishResult> UploadOnlineAsync(
        TaskbookDocument document,
        string serverUrl,
        string accessToken,
        StoragePlan plan = StoragePlan.FreeTwoWeeks,
        CancellationToken cancellationToken = default)
    {
        var packagePath = Path.Combine(Path.GetTempPath(), $"pvz-upload-{Guid.NewGuid():N}.pvz");
        try
        {
            TaskbookPackageIO.Save(packagePath, document);
            var receipt = await TaskbookOnlineClient.UploadAsync(packagePath, serverUrl, accessToken, plan, cancellationToken);
            return new TaskbookPublishResult
            {
                Success = true,
                SessionId = receipt.Id,
                AccessToken = receipt.Token,
                TokenExpiresAt = receipt.ExpiresAt,
                Retention = receipt.Retention,
            };
        }
        catch (ServerUnauthorizedException ex)
        {
            return new TaskbookPublishResult
            {
                Success = false,
                Unauthorized = true,
                ErrorMessage = ex.Message,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidDataException or InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            return new TaskbookPublishResult
            {
                Success = false,
                ErrorMessage = $"Не удалось загрузить задачник на сервер: {ex.Message}",
            };
        }
        finally
        {
            if (File.Exists(packagePath))
                File.Delete(packagePath);
        }
    }

    public static string? FindPlayerTemplate()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, TemplateRelativePath),
            Path.Combine(baseDir, "PVZP.exe"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
