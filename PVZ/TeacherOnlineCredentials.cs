using System.Text.Json;
using PVZ.Core;

namespace PVZ;

internal static class TeacherOnlineCredentials
{
    private static readonly string CredentialsPath =
        Path.Combine(Application.UserAppDataPath, "teacher-online.json");

    public static string? TryLoadApiKey()
    {
        var fromEnv = Environment.GetEnvironmentVariable("PVZ_TEACHER_API_KEY")?.Trim();
        if (fromEnv?.Length == 8)
            return fromEnv;

        if (!File.Exists(CredentialsPath))
            return null;

        try
        {
            var stored = JsonSerializer.Deserialize<StoredCredentials>(File.ReadAllText(CredentialsPath));
            return stored?.ApiKey?.Length == 8 ? stored.ApiKey : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void ClearStoredApiKey()
    {
        if (File.Exists(CredentialsPath))
            File.Delete(CredentialsPath);
    }

    public static async Task<string> EnsureApiKeyAsync(string serverUrl, CancellationToken cancellationToken = default)
    {
        var existing = TryLoadApiKey();
        if (existing is not null)
            return existing;

        var displayName = string.IsNullOrWhiteSpace(Environment.UserName)
            ? "Учитель PVZ"
            : Environment.UserName;
        var receipt = await TaskbookOnlineClient.RegisterTeacherAsync(displayName, serverUrl, cancellationToken);
        SaveApiKey(receipt.ApiKey);
        return receipt.ApiKey;
    }

    private static void SaveApiKey(string apiKey)
    {
        Directory.CreateDirectory(Application.UserAppDataPath);
        var json = JsonSerializer.Serialize(new StoredCredentials(apiKey));
        File.WriteAllText(CredentialsPath, json);
    }

    private sealed record StoredCredentials(string ApiKey);
}
