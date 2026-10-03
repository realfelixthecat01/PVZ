using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PVZ.Core;

public sealed record TaskbookUploadReceipt(string Token, DateTimeOffset UploadedAt, DateTimeOffset ExpiresAt);
public sealed record TeacherRegistrationReceipt(string TeacherId, string ApiKey);
public sealed record TaskbookTokenSummaryDto(
    string Id,
    string TokenHint,
    DateTimeOffset UploadedAt,
    DateTimeOffset ExpiresAt,
    bool IsActive);
public sealed record StudentProgressSummaryDto(
    string FirstName,
    string LastName,
    string ClassName,
    DateTimeOffset StartedAt,
    DateTimeOffset LastActivityAt,
    long ElapsedSeconds,
    int EarnedPoints,
    int MaximumPoints,
    int CompletedTasks,
    int TotalTasks,
    bool IsOnline);

public static class TaskbookOnlineClient
{
    public const int MaximumPackageBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SocketsHttpHandler SharedHandler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };

    public static string GetConfiguredServerUrl() =>
        Environment.GetEnvironmentVariable("PVZ_SERVER_URL")
        ?? throw new InvalidOperationException("Задайте PVZ_SERVER_URL с адресом общего сервера.");

    public static async Task<TeacherRegistrationReceipt> RegisterTeacherAsync(
        string teacherName,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(teacherName);
        using var client = CreateClient(serverUrl);
        using var response = await client.PostAsJsonAsync(
            "api/teachers/register",
            new { displayName = teacherName },
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateServerExceptionAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<TeacherRegistrationReceipt>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Сервер не вернул ключ учителя.");
    }

    public static async Task<TaskbookUploadReceipt> UploadAsync(
        string packagePath,
        string serverUrl,
        string teacherApiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(teacherApiKey);
        if (teacherApiKey.Length != 8)
            throw new ArgumentException("Ключ учителя должен состоять ровно из 8 символов.", nameof(teacherApiKey));
        using var client = CreateClient(serverUrl);
        await using var package = File.OpenRead(packagePath);
        if (package.Length <= 0 || package.Length > MaximumPackageBytes)
            throw new InvalidDataException("Размер .pvz файла недопустим.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/teacher/taskbooks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherApiKey);
        request.Content = new StreamContent(package);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateServerExceptionAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<TaskbookUploadReceipt>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Сервер вернул пустой результат загрузки.");
    }

    public static async Task<OnlineStudentSession> StartStudentSessionAsync(
        string token,
        OnlineStudentInfo student,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        using var client = CreateClient(serverUrl);
        using var response = await client.PostAsJsonAsync(
            $"api/taskbooks/{Uri.EscapeDataString(token)}/sessions",
            student,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateServerExceptionAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<OnlineStudentSession>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Сервер не вернул сессию ученика.");
    }

    public static async Task<IReadOnlyList<TaskbookTokenSummaryDto>> ListTeacherTaskbooksAsync(
        string teacherApiKey,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(serverUrl);
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/teacher/taskbooks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherApiKey);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateServerExceptionAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<TaskbookTokenSummaryDto[]>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Сервер вернул пустой список задачников.");
    }

    public static async Task<IReadOnlyList<StudentProgressSummaryDto>> ListStudentProgressAsync(
        string teacherApiKey,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        using var client = CreateClient(serverUrl);
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/teacher/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherApiKey);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateServerExceptionAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<StudentProgressSummaryDto[]>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Сервер вернул пустой список учеников.");
    }

    public static async Task ReportProgressAsync(
        OnlineProgressUpdate update,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        using var client = CreateClient(serverUrl);
        using var response = await client.PutAsJsonAsync(
            $"api/student/sessions/{Uri.EscapeDataString(update.SessionId)}/progress",
            update,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateServerExceptionAsync(response, cancellationToken);
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

        return new HttpClient(SharedHandler, disposeHandler: false)
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(60),
        };
    }

    private static async Task<InvalidOperationException> CreateServerExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        var status = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                "Ключ учителя неверен или у него нет доступа.",
            System.Net.HttpStatusCode.NotFound =>
                "Задачник с таким токеном не найден, истёк или отозван.",
            (System.Net.HttpStatusCode)429 =>
                "Слишком много запросов к серверу. Подождите минуту и попробуйте снова.",
            _ => "",
        };

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if ((mediaType is "application/json" or "application/problem+json") &&
            !string.IsNullOrWhiteSpace(details))
        {
            try
            {
                using var json = JsonDocument.Parse(details);
                if (json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                    details = error.GetString() ?? "";
                else if (json.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    details = detail.GetString() ?? "";
                else
                    details = "";
            }
            catch (JsonException)
            {
                details = "";
            }
        }
        else if (mediaType != "text/plain")
        {
            details = "";
        }

        return new InvalidOperationException(
            !string.IsNullOrWhiteSpace(status)
                ? status
                : !string.IsNullOrWhiteSpace(details)
                    ? details
                    : $"Ошибка сервера PVZ ({(int)response.StatusCode}).");
    }
}
