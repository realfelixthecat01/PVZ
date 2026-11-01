// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace PVZ.Core;

public static class TaskbookUserFolders
{
    public static string RootPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "PVZ");

    public static string DownloadsPath => Path.Combine(RootPath, "downloads");

    public static string ProjectsPath => Path.Combine(RootPath, "projects");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DownloadsPath);
        Directory.CreateDirectory(ProjectsPath);
    }
}

/// <summary>Локальный вход на сервер: токен и минимум данных, чтобы не спрашивать пользователя снова.</summary>
public sealed record LocalIdentity(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string Role,
    string FullName,
    string ClassName,
    bool IsGuest);

/// <summary>Гостевой ключ и токены ученика лежат в файлах рядом с задачниками пользователя.</summary>
public static class LocalIdentityStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static LocalIdentity? Load(string fileName)
    {
        try
        {
            var path = Path.Combine(TaskbookUserFolders.RootPath, fileName);
            if (!File.Exists(path)) return null;
            var identity = JsonSerializer.Deserialize<LocalIdentity>(File.ReadAllText(path), JsonOptions);
            if (identity is null || string.IsNullOrWhiteSpace(identity.AccessToken)) return null;
            // Часы на компьютере могут отставать: проверяем запас, чтобы не принимать мёртвый токен.
            return identity.ExpiresAt > DateTimeOffset.Now + TimeSpan.FromMinutes(5) ? identity : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void Save(string fileName, LocalIdentity identity)
    {
        TaskbookUserFolders.EnsureCreated();
        var path = Path.Combine(TaskbookUserFolders.RootPath, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(identity, JsonOptions));
    }

    public static void Delete(string fileName)
    {
        try
        {
            var path = Path.Combine(TaskbookUserFolders.RootPath, fileName);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Длинный случайный ключ гостя: по нему сервер находит одного и того же гостя. Создаётся один раз.</summary>
    public static string LoadOrCreateGuestSecret()
    {
        TaskbookUserFolders.EnsureCreated();
        var path = Path.Combine(TaskbookUserFolders.RootPath, "guest.json");
        try
        {
            if (File.Exists(path))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (document.RootElement.TryGetProperty("secret", out var secret)
                    && secret.GetString() is { Length: >= 32 } value)
                {
                    return value;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        var created = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(48));
        File.WriteAllText(path, JsonSerializer.Serialize(new { secret = created }, JsonOptions));
        return created;
    }
}
