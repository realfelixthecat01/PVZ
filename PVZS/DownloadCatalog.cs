namespace PVZS;

/// <summary>
/// Скачиваемые сборки PVZ: клиент ученика (PVZP), клиент учителя (PVZT) и сам сервер (PVZS).
/// Файлы берутся из папки downloads рядом с сервером (или из <c>PVZ_DOWNLOAD_DIR</c>):
/// PVZP.exe, PVZT.exe, PVZS.exe. Если PVZS.exe рядом нет, отдаётся запущенный сервер.
/// </summary>
public sealed class DownloadCatalog
{
    public const string StudentId = "pvzp";
    public const string TeacherId = "pvzt";
    public const string ServerId = "pvzs";

    /// <summary>Максимальный размер загружаемого для запуска/обновления файла.</summary>
    public const int MaximumControlUploadBytes = 512 * 1024 * 1024;

    /// <summary>Сколько символов вывода запущенного процесса возвращать администратору.</summary>
    public const int MaximumControlOutputChars = 20000;

    private static readonly string[] PublicIds = [StudentId, TeacherId];
    private static readonly string[] AllIds = [StudentId, TeacherId, ServerId];

    private static readonly IReadOnlyDictionary<string, string> FileNames = new Dictionary<string, string>
    {
        [StudentId] = "PVZP.exe",
        [TeacherId] = "PVZT.exe",
        [ServerId] = "PVZS.exe",
    };

    private readonly string _directory;

    public DownloadCatalog(string directory)
    {
        _directory = directory;
        try
        {
            System.IO.Directory.CreateDirectory(_directory);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public string DirectoryPath => _directory;

    /// <summary>Клиенты, доступные без входа: ученик и учитель.</summary>
    public IReadOnlyList<DownloadInfoDto> ListPublic() => PublicIds.Select(Describe).ToArray();

    /// <summary>Все сборки, включая сервер. Показывается только администратору.</summary>
    public IReadOnlyList<DownloadInfoDto> ListAll() => AllIds.Select(Describe).ToArray();

    /// <summary>Публичная сборка по id (pvzp/pvzt) или null, если id не публичный.</summary>
    public DownloadInfoDto? FindPublic(string id) =>
        TryNormalize(id, PublicIds, out var normalized) ? Describe(normalized) : null;

    /// <summary>Любая сборка по id (pvzp/pvzt/pvzs) или null.</summary>
    public DownloadInfoDto? Find(string id) =>
        TryNormalize(id, AllIds, out var normalized) ? Describe(normalized) : null;

    public string? ResolvePath(string id)
    {
        if (!FileNames.TryGetValue(id, out var fileName))
            return null;
        var path = Path.Combine(_directory, fileName);
        if (File.Exists(path))
            return path;
        return id == ServerId ? RunningServerPath() : null;
    }

    /// <summary>Открывает сборку на чтение или возвращает null, если файла нет.</summary>
    public FileStream? TryOpen(string id)
    {
        var path = ResolvePath(id);
        if (path is null)
            return null;
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private DownloadInfoDto Describe(string id)
    {
        var path = ResolvePath(id);
        var size = 0L;
        if (path is not null)
        {
            try
            {
                size = new FileInfo(path).Length;
            }
            catch (IOException)
            {
                size = 0;
            }
        }

        var role = id switch
        {
            StudentId => "student",
            TeacherId => "teacher",
            _ => "server",
        };
        return new DownloadInfoDto(id, FileNames[id], role, size, path is not null);
    }

    private static bool TryNormalize(string id, string[] ids, out string normalized)
    {
        normalized = id.ToLowerInvariant();
        foreach (var candidate in ids)
        {
            if (string.Equals(candidate, normalized, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string? RunningServerPath()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
            return null;
        // При запуске через `dotnet PVZS.dll` ProcessPath — это dotnet.exe, а не сборка сервера.
        if (!Path.GetFileName(processPath).Equals("PVZS.exe", StringComparison.OrdinalIgnoreCase))
            return null;
        return File.Exists(processPath) ? processPath : null;
    }
}

public sealed record DownloadInfoDto(
    string Id,
    string FileName,
    string Role,
    long SizeBytes,
    bool Available);
