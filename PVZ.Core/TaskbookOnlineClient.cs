using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PVZ.Core;

public sealed record TaskbookUploadReceipt(string Token, DateTimeOffset UploadedAt, DateTimeOffset ExpiresAt);

public static class TaskbookOnlineClient
{
    public const int MaximumPackageBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string GetConfiguredServerUrl() =>
        Environment.GetEnvironmentVariable("PVZ_SERVER_URL") ?? "http://localhost:5080";

    public static async Task<TaskbookUploadReceipt> UploadAsync(
        string packagePath,
        string serverUrl,
        string adminKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(adminKey);
        using var client = CreateClient(serverUrl);
        await using var package = File.OpenRead(packagePath);
        if (package.Length <= 0 || package.Length > MaximumPackageBytes)
            throw new InvalidDataException("Размер .pvz файла недопустим.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/admin/taskbooks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminKey);
        request.Content = new StreamContent(package);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateServerExceptionAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<TaskbookUploadReceipt>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Сервер вернул пустой результат загрузки.");
    }

    public static async Task<TaskbookDocument> DownloadAsync(
        string token,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        using var client = CreateClient(serverUrl);
        using var response = await client.GetAsync(
            $"api/taskbooks/{Uri.EscapeDataString(token)}",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateServerExceptionAsync(response, cancellationToken);

        if (response.Content.Headers.ContentLength is > MaximumPackageBytes)
            throw new InvalidDataException("Скачанный .pvz файл превышает допустимый размер.");

        var tempPath = Path.Combine(Path.GetTempPath(), $"pvz-download-{Guid.NewGuid():N}.pvz");
        try
        {
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = File.Create(tempPath))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    if (output.Length + read > MaximumPackageBytes)
                        throw new InvalidDataException("Скачанный .pvz файл превышает допустимый размер.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            return TaskbookPackageIO.Load(tempPath);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static HttpClient CreateClient(string serverUrl)
    {
        if (!Uri.TryCreate(serverUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseAddress) ||
            baseAddress.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(baseAddress.UserInfo))
            throw new InvalidOperationException("PVZ_SERVER_URL должен содержать корректный http(s) URL без учётных данных.");
        if (baseAddress.Scheme != Uri.UriSchemeHttps && !baseAddress.IsLoopback)
            throw new InvalidOperationException("Для сервера на другом компьютере настройте HTTPS в PVZ_SERVER_URL.");

        return new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(60) };
    }

    private static async Task<InvalidOperationException> CreateServerExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(details))
            details = response.ReasonPhrase ?? "Unknown server error.";
        return new InvalidOperationException($"PVZ server returned {(int)response.StatusCode}: {details}");
    }
}
