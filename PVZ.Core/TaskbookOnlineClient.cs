using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace PVZ.Core;

public sealed class ServerUnauthorizedException(string message) : InvalidOperationException(message);

public static class TaskbookOnlineClient
{
    /// <summary>Постоянный адрес сервера PVZS.</summary>
    public const string DefaultServerUrl = "https://pvz-server.vercel.app";

    public const int MaximumPackageBytes = 16 * 1024 * 1024;
    public const int MaximumExchangeFileBytes = 32 * 1024 * 1024;
    private static readonly TimeSpan ExchangeUploadTimeout = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly SocketsHttpHandler SharedHandler = new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };

    public static Task<string> ResolveServerUrlAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromResult(ResolveServerUrl());
    }

    /// <summary>Адрес сервера: переменная PVZ_SERVER_URL (для разработки) или постоянный адрес.</summary>
    public static string ResolveServerUrl()
    {
        var configured = Environment.GetEnvironmentVariable("PVZ_SERVER_URL");
        return string.IsNullOrWhiteSpace(configured)
            ? NormalizeServerUrl(DefaultServerUrl)
            : NormalizeServerUrl(configured);
    }

    public static async Task<IReadOnlyList<TaskbookCatalogItemDto>> ListCatalogTaskbooksAsync(
        string subject,
        int classNumber,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        if (!TaskbookCatalogSubjects.IsValid(subject))
            throw new ArgumentException("Неизвестный предмет.", nameof(subject));
        if (!TaskbookCatalogSubjects.IsValidClass(subject, classNumber))
            throw new ArgumentOutOfRangeException(nameof(classNumber), "Для выбранного предмета этот класс недоступен.");

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var response = await client.GetAsync(
                $"api/catalog/{Uri.EscapeDataString(subject)}/{classNumber}",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);
            return await response.Content.ReadFromJsonAsync<TaskbookCatalogItemDto[]>(JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Сервер вернул пустой каталог задачников.");
        });
    }

    public static async Task<byte[]> DownloadCatalogTaskbookAsync(
        string subject,
        int classNumber,
        string fileName,
        string serverUrl,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        if (!TaskbookCatalogSubjects.IsValid(subject))
            throw new ArgumentException("Неизвестный предмет.", nameof(subject));
        if (!TaskbookCatalogSubjects.IsValidClass(subject, classNumber))
            throw new ArgumentOutOfRangeException(nameof(classNumber), "Для выбранного предмета этот класс недоступен.");
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/catalog/{Uri.EscapeDataString(subject)}/{classNumber}/{Uri.EscapeDataString(fileName)}");
            if (!string.IsNullOrWhiteSpace(accessToken))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);

            return await ReadBoundedAsync(response, cancellationToken);
        });
    }

    public static async Task<TeacherLoginReceipt> LoginAsync(
        string password,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var response = await client.PostAsJsonAsync(
                "api/teacher/login",
                new { password },
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(
                    response,
                    cancellationToken,
                    "Пароль доступа неверен.");

            return await response.Content.ReadFromJsonAsync<TeacherLoginReceipt>(JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Сервер не выдал токен доступа.");
        });
    }

    /// <summary>Вход в аккаунт учителя по имени аккаунта или email.</summary>
    public static async Task<AccountAuthReceipt> AccountLoginAsync(
        string identifier,
        string password,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var response = await client.PostAsJsonAsync(
                "api/account/login",
                new { identifier, password },
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(
                    response,
                    cancellationToken,
                    "Неверное имя аккаунта или пароль.");

            return await response.Content.ReadFromJsonAsync<AccountAuthReceipt>(JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Сервер не выдал токен доступа.");
        });
    }

    /// <summary>Регистрация аккаунта учителя: имя аккаунта, ФИО, школа, email и пароль.</summary>
    public static async Task<AccountAuthReceipt> AccountRegisterAsync(
        string username,
        string fullName,
        string school,
        string email,
        string password,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(school);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var response = await client.PostAsJsonAsync(
                "api/account/register",
                new { username, fullName, school, email, password },
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<AccountAuthReceipt>(JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Сервер не выдал токен доступа.");
        });
    }

    public static async Task<TeacherSessionUploadReceipt> UploadAsync(
        string packagePath,
        string serverUrl,
        string accessToken,
        StoragePlan plan = StoragePlan.FreeTwoWeeks,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl, ExchangeUploadTimeout);
            await using var package = File.OpenRead(packagePath);
            if (package.Length <= 0 || package.Length > MaximumPackageBytes)
                throw new InvalidDataException("Размер .pvz файла недопустим.");

            var planParameter = plan == StoragePlan.PaidOneMonth ? "paid-month" : "free-weeks";
            var begin = await BeginUploadAsync(
                client,
                accessToken,
                new UploadBeginRequest { Target = "teacher-session", Size = package.Length, Plan = planParameter },
                cancellationToken);
            await PutUploadAsync(client, begin, package, cancellationToken);
            var complete = await CompleteUploadAsync<SessionUploadComplete>(
                client,
                accessToken,
                begin.UploadId,
                cancellationToken);
            return complete.Receipt;
        });
    }

    public static async Task<IReadOnlyList<TeacherSessionDto>> ListTeacherSessionsAsync(
        string accessToken,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/teacher/sessions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(
                    response,
                    cancellationToken,
                    "Сессия доступа истекла. Войдите снова.");
            return await response.Content.ReadFromJsonAsync<TeacherSessionDto[]>(JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Сервер вернул пустой список задачников.");
        });
    }

    public static async Task<DateTimeOffset> ExtendSessionAsync(
        string accessToken,
        string sessionId,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"api/teacher/sessions/{Uri.EscapeDataString(sessionId)}/extend");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);

            var receipt = await response.Content.ReadFromJsonAsync<SessionExtendReceipt>(JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Сервер не вернул новый срок хранения.");
            return receipt.ExpiresAt;
        });
    }

    public static async Task CloseSessionAsync(
        string accessToken,
        string sessionId,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"api/teacher/sessions/{Uri.EscapeDataString(sessionId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);
        });
    }

    public static async Task<byte[]> DownloadSessionAsync(
        string accessToken,
        string sessionId,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/teacher/sessions/{Uri.EscapeDataString(sessionId)}/download");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);

            return await ReadBoundedAsync(response, cancellationToken);
        });
    }

    public static async Task<OnlineStudentSession> StartStudentSessionAsync(
        string token,
        OnlineStudentInfo student,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var response = await client.PostAsJsonAsync(
                $"api/taskbooks/{Uri.EscapeDataString(token)}/sessions",
                student,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<OnlineStudentSession>(JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Сервер не вернул сессию ученика.");
        });
    }

    public static async Task ReportProgressAsync(
        OnlineProgressUpdate update,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var response = await client.PutAsJsonAsync(
                $"api/student/sessions/{Uri.EscapeDataString(update.SessionId)}/progress",
                update,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);
        });
    }

    /// <summary>Ученик спрашивает, смотрят ли его экран сейчас.</summary>
    public static async Task<bool> CheckScreenWatchAsync(
        string studentSessionId,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentSessionId);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var response = await client.GetAsync(
                $"api/student/sessions/{Uri.EscapeDataString(studentSessionId)}/watch",
                cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return false;
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);
            var state = await response.Content.ReadFromJsonAsync<ScreenWatchStateDto>(JsonOptions, cancellationToken);
            return state?.Watching ?? false;
        });
    }

    /// <summary>Ученик отправляет кадр экрана; возвращает, продолжается ли просмотр.</summary>
    public static async Task<bool> SendScreenFrameAsync(
        string studentSessionId,
        byte[] jpeg,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentSessionId);
        ArgumentNullException.ThrowIfNull(jpeg);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var content = new ByteArrayContent(jpeg);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            using var response = await client.PostAsync(
                $"api/student/sessions/{Uri.EscapeDataString(studentSessionId)}/screen",
                content,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);
            var state = await response.Content.ReadFromJsonAsync<ScreenWatchStateDto>(JsonOptions, cancellationToken);
            return state?.Watching ?? false;
        });
    }

    /// <summary>Ученик нажал «Стоп» — сообщаем серверу прекратить просмотр.</summary>
    public static async Task StopScreenShareAsync(
        string studentSessionId,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentSessionId);

        await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var response = await client.DeleteAsync(
                $"api/student/sessions/{Uri.EscapeDataString(studentSessionId)}/screen",
                cancellationToken);
            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
                throw await CreateServerExceptionAsync(response, cancellationToken);
        });
    }

    public static async Task<TaskbookDocument> DownloadAsync(
        string token,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var response = await client.GetAsync(
                $"api/taskbooks/{Uri.EscapeDataString(token)}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);

            var package = await ReadBoundedAsync(response, cancellationToken);
            var tempPath = Path.Combine(Path.GetTempPath(), $"pvz-download-{Guid.NewGuid():N}.pvz");
            try
            {
                await File.WriteAllBytesAsync(tempPath, package, cancellationToken);
                return TaskbookPackageIO.Load(tempPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        });
    }

    /// <summary>Список файлов задачника глазами ученика: все материалы и только свои работы.</summary>
    public static async Task<StudentExchangeFilesDto> ListStudentFilesAsync(
        string studentSessionId,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentSessionId);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var response = await client.GetAsync(
                $"api/student/sessions/{Uri.EscapeDataString(studentSessionId)}/files",
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);
            return await response.Content.ReadFromJsonAsync<StudentExchangeFilesDto>(JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Сервер вернул пустой список файлов.");
        });
    }

    /// <summary>Скачивание материала от учителя или своей работы учеником.</summary>
    public static async Task<byte[]> DownloadStudentFileAsync(
        string studentSessionId,
        string fileId,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentSessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl, ExchangeUploadTimeout);
            using var response = await client.GetAsync(
                $"api/student/sessions/{Uri.EscapeDataString(studentSessionId)}/files/{Uri.EscapeDataString(fileId)}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);
            return await ReadBoundedAsync(response, cancellationToken, MaximumExchangeFileBytes);
        });
    }

    /// <summary>Отправка своей работы учителю; имя файла — имя выбранного файла на диске.</summary>
    public static async Task<ExchangeFileDto> UploadStudentWorkAsync(
        string studentSessionId,
        string filePath,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(studentSessionId);
        return await UploadExchangeFileAsync(
            "student-work",
            filePath,
            accessToken: null,
            serverUrl,
            sessionId: null,
            studentSessionId,
            cancellationToken);
    }

    /// <summary>Все файлы своего задачника глазами учителя: материалы и работы с именами учеников.</summary>
    public static async Task<IReadOnlyList<ExchangeFileDto>> ListSessionFilesAsync(
        string accessToken,
        string sessionId,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/teacher/sessions/{Uri.EscapeDataString(sessionId)}/files");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(
                    response,
                    cancellationToken,
                    "Сессия доступа истекла. Войдите снова.");
            return await response.Content.ReadFromJsonAsync<ExchangeFileDto[]>(JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("Сервер вернул пустой список файлов.");
        });
    }

    /// <summary>Отправка материала всем ученикам задачника.</summary>
    public static async Task<ExchangeFileDto> UploadSessionFileAsync(
        string accessToken,
        string sessionId,
        string filePath,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return await UploadExchangeFileAsync(
            "teacher-material",
            filePath,
            accessToken,
            serverUrl,
            sessionId,
            studentSessionId: null,
            cancellationToken);
    }

    /// <summary>Скачивание работы ученика (или своего материала) учителем.</summary>
    public static async Task<byte[]> DownloadSessionFileAsync(
        string accessToken,
        string sessionId,
        string fileId,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl, ExchangeUploadTimeout);
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/teacher/sessions/{Uri.EscapeDataString(sessionId)}/files/{Uri.EscapeDataString(fileId)}/download");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);
            return await ReadBoundedAsync(response, cancellationToken, MaximumExchangeFileBytes);
        });
    }

    public static async Task DeleteSessionFileAsync(
        string accessToken,
        string sessionId,
        string fileId,
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);

        await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl);
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"api/teacher/sessions/{Uri.EscapeDataString(sessionId)}/files/{Uri.EscapeDataString(fileId)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw await CreateServerExceptionAsync(response, cancellationToken);
        });
    }

    private sealed class UploadBeginRequest
    {
        public required string Target { get; init; }
        public long Size { get; init; }
        public string? Plan { get; init; }
        public string? Name { get; init; }
        public string? SessionId { get; init; }
        public string? StudentSessionId { get; init; }
    }

    private sealed record UploadBeginReceipt(
        string UploadId,
        string UploadUrl,
        Dictionary<string, string>? Headers);

    private sealed record SessionUploadComplete(string Kind, TeacherSessionUploadReceipt Receipt);

    private sealed record FileUploadComplete(string Kind, ExchangeFileDto Info);

    private static async Task<ExchangeFileDto> UploadExchangeFileAsync(
        string target,
        string filePath,
        string? accessToken,
        string serverUrl,
        string? sessionId,
        string? studentSessionId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        return await WithTransportErrorsAsync(serverUrl, cancellationToken, async () =>
        {
            using var client = CreateClient(serverUrl, ExchangeUploadTimeout);
            await using var stream = File.OpenRead(filePath);
            if (stream.Length <= 0 || stream.Length > MaximumExchangeFileBytes)
                throw new InvalidDataException("Размер файла должен быть от 1 байта до 32 МиБ.");

            var begin = await BeginUploadAsync(
                client,
                accessToken,
                new UploadBeginRequest
                {
                    Target = target,
                    Size = stream.Length,
                    Name = Path.GetFileName(filePath),
                    SessionId = sessionId,
                    StudentSessionId = studentSessionId,
                },
                cancellationToken);
            await PutUploadAsync(client, begin, stream, cancellationToken);
            var complete = await CompleteUploadAsync<FileUploadComplete>(
                client,
                accessToken,
                begin.UploadId,
                cancellationToken);
            return complete.Info;
        });
    }

    /// <summary>Сервер выдаёт адрес и заголовки для прямой загрузки файла (на Vercel — в Blob).</summary>
    private static async Task<UploadBeginReceipt> BeginUploadAsync(
        HttpClient client,
        string? accessToken,
        UploadBeginRequest requestBody,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/upload/begin")
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions),
        };
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateServerExceptionAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<UploadBeginReceipt>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Сервер не подготовил загрузку файла.");
    }

    private static async Task PutUploadAsync(
        HttpClient client,
        UploadBeginReceipt begin,
        Stream content,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri(begin.UploadUrl, UriKind.RelativeOrAbsolute));
        foreach (var header in begin.Headers ?? [])
        {
            if (string.Equals(header.Key, "content-type", StringComparison.OrdinalIgnoreCase))
                continue;
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        request.Content = new StreamContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var details = await response.Content.ReadAsStringAsync(cancellationToken);
            var hint = string.IsNullOrWhiteSpace(details) ? "" : $" {details}";
            throw new InvalidOperationException($"Не удалось загрузить файл на сервер ({(int)response.StatusCode}).{hint}".Trim());
        }
    }

    private static async Task<T> CompleteUploadAsync<T>(
        HttpClient client,
        string? accessToken,
        string uploadId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/upload/complete")
        {
            Content = JsonContent.Create(new { uploadId }, options: JsonOptions),
        };
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateServerExceptionAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Сервер не подтвердил загрузку файла.");
    }

    public static string NormalizeServerUrl(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        value = value.Trim();
        if (!value.Contains("://", StringComparison.Ordinal))
        {
            var hostPort = value.Split('/', 2)[0];
            var scheme = Uri.TryCreate($"http://{hostPort}/", UriKind.Absolute, out var probe) &&
                         (probe.IsLoopback || probe.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                ? "http"
                : "https";
            value = $"{scheme}://{value}";
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            absolute.Scheme is "http" or "https" &&
            string.IsNullOrEmpty(absolute.UserInfo))
            return absolute.GetLeftPart(UriPartial.Authority) + "/";

        throw new InvalidOperationException($"Некорректный адрес сервера: {value}");
    }

    private static async Task<T> WithTransportErrorsAsync<T>(
        string serverUrl,
        CancellationToken cancellationToken,
        Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (HttpRequestException exception)
        {
            throw ServerUnreachableException.CreateTransport(exception, serverUrl);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw ServerUnreachableException.CreateTimeout(serverUrl, exception);
        }
    }

    private static async Task WithTransportErrorsAsync(
        string serverUrl,
        CancellationToken cancellationToken,
        Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (HttpRequestException exception)
        {
            throw ServerUnreachableException.CreateTransport(exception, serverUrl);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw ServerUnreachableException.CreateTimeout(serverUrl, exception);
        }
    }

    private static HttpClient CreateClient(string serverUrl, TimeSpan? timeout = null)
    {
        var baseAddress = NormalizeServerUrl(serverUrl);
        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("Адрес сервера должен быть корректным http(s) URL без учётных данных.");
        if (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback &&
            !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Для сервера на другом компьютере требуется HTTPS.");

        return new HttpClient(SharedHandler, disposeHandler: false)
        {
            BaseAddress = uri,
            Timeout = timeout ?? TimeSpan.FromSeconds(60),
        };
    }

    private static async Task<byte[]> ReadBoundedAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        int maximumBytes = MaximumPackageBytes)
    {
        if (response.Content.Headers.ContentLength is long contentLength && contentLength > maximumBytes)
            throw new InvalidDataException("Скачанный файл превышает допустимый размер.");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > maximumBytes)
                throw new InvalidDataException("Скачанный файл превышает допустимый размер.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.ToArray();
    }

    private static async Task<Exception> CreateServerExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string? unauthorizedMessage = null)
    {
        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        var isUnauthorized = response.StatusCode is
            System.Net.HttpStatusCode.Unauthorized or
            System.Net.HttpStatusCode.Forbidden;
        var status = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                unauthorizedMessage ?? "Доступ запрещён. Войдите снова.",
            System.Net.HttpStatusCode.NotFound =>
                "Задачник с таким токеном не найден или срок его хранения истёк.",
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

        var message = !string.IsNullOrWhiteSpace(status)
            ? status
            : !string.IsNullOrWhiteSpace(details)
                ? details
                : $"Ошибка сервера PVZ ({(int)response.StatusCode}).";
        return isUnauthorized
            ? new ServerUnauthorizedException(message)
            : new InvalidOperationException(message);
    }
}
