using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using PVZ.Core;

// Интеграционные тесты клиента PVZ.Core против настоящего сервера pvz-server (Node.js,
// тот же код, что работает на Vercel). Сервер запускается локально с временными данными.

var repositoryRoot = FindRepositoryRoot();
var serverScript = Path.Combine(repositoryRoot, "pvz-server", "server.js");
Require(File.Exists(serverScript), "pvz-server/server.js существует");

var port = FreePort();
var serverUrl = $"http://127.0.0.1:{port}";
var tempRoot = Path.Combine(Path.GetTempPath(), $"pvz-node-test-{Guid.NewGuid():N}");
var dataDirectory = Path.Combine(tempRoot, "data");
var catalogDirectory = Path.Combine(dataDirectory, "blobs", "pvz", "catalog");
var downloadsDirectory = Path.Combine(tempRoot, "downloads");
Directory.CreateDirectory(Path.Combine(catalogDirectory, TaskbookCatalogSubjects.Math, "9"));
Directory.CreateDirectory(downloadsDirectory);

var document = new TaskbookDocument
{
    Subject = TaskbookCatalogSubjects.Math,
    ClassNumber = 9,
    Tasks =
    [
        new TaskbookTaskDto
        {
            Description = "Persisted test task",
            BonusPointsForAllTests = 10,
            Tests = [new TestCaseDto { Input = "a", Output = "b", Points = 20 }],
        },
    ],
};

var packagePath = Path.Combine(tempRoot, "test.pvz");
TaskbookPackageIO.Save(packagePath, document);
var cabinetPackagePath = Path.Combine(tempRoot, "cabinet.pvz");
TaskbookPackageIO.Save(cabinetPackagePath, document);
var catalogFileName = "math-9-test.pvz";
File.Copy(packagePath, Path.Combine(catalogDirectory, TaskbookCatalogSubjects.Math, "9", catalogFileName));
var studentBuild = new byte[] { 0x4d, 0x5a, 0x50, 0x56, 0x5a, 0x50 };
var teacherBuild = new byte[] { 0x4d, 0x5a, 0x50, 0x56, 0x5a, 0x54 };
File.WriteAllBytes(Path.Combine(downloadsDirectory, "PVZP.exe"), studentBuild);
File.WriteAllBytes(Path.Combine(downloadsDirectory, "PVZT.exe"), teacherBuild);

Process? server = null;
try
{
    Require(
        TaskbookOnlineClient.NormalizeServerUrl(TaskbookOnlineClient.DefaultServerUrl) == "https://pvz-server.vercel.app/",
        "Без настройки клиент использует постоянный адрес Vercel");

    var addressBackup = Environment.GetEnvironmentVariable("PVZ_SERVER_URL");
    try
    {
        Environment.SetEnvironmentVariable("PVZ_SERVER_URL", "https://env.example.com");
        Require(
            TaskbookOnlineClient.ResolveServerUrl() == "https://env.example.com/",
            "PVZ_SERVER_URL переопределяет адрес сервера");
    }
    finally
    {
        Environment.SetEnvironmentVariable("PVZ_SERVER_URL", addressBackup);
    }

    Require(
        TaskbookOnlineClient.NormalizeServerUrl("localhost:1729") == "http://localhost:1729/",
        "Локальный адрес сохраняет http и порт");

    server = StartServer(serverScript, serverUrl, dataDirectory, catalogDirectory, downloadsDirectory);
    using var http = new HttpClient { BaseAddress = new Uri(serverUrl), Timeout = TimeSpan.FromSeconds(15) };
    await WaitForServerAsync(http, server);

    // ---- чистая логика без сети ----

    const string blankSample = "I ___ to school. She ___ not.";
    Require(FillBlankRules.CountBlanks(blankSample) == 2, "Каждый ряд подчёркиваний — один пропуск");
    Require(FillBlankRules.SplitSegments(blankSample).Length == 3, "Текст делится на пропуски + 1 сегмент");
    Require(FillBlankRules.Normalize("  Don’t   Like ") == "don't like", "Нормализация апострофов и пробелов");
    var rulesBlank = new FillBlankDto { Answer = "does not", AcceptedAnswers = ["doesn't", "doesnt"] };
    Require(
        (FillBlankRules.Matches(rulesBlank, "DOES NOT") && FillBlankRules.Matches(rulesBlank, "doesn’t")) &&
        !FillBlankRules.Matches(rulesBlank, "do not"),
        "Проверка пропусков учитывает варианты ответа");

    Require(TaskbookScoring.GetTaskMaxScore(document.Tasks[0]) == 30, "Максимум задания = тесты + бонус");
    var loaded = TaskbookPackageIO.Load(packagePath);
    Require(loaded.Tasks.Count == 1 && loaded.Tasks[0].Description == document.Tasks[0].Description,
        "Пакет .pvz читается обратно без потерь");

    // ---- сайт и скачивания ----

    foreach (var page in new[] { "/", "/pricing", "/login", "/register", "/cabinet", "/admin" })
    {
        var response = await http.GetAsync(page);
        Require(response.IsSuccessStatusCode, $"Страница {page} отдаётся");
        Require(response.Content.Headers.ContentType?.MediaType == "text/html", $"Страница {page} — HTML");
    }

    var health = await http.GetFromJsonAsync<JsonElement>("/health");
    Require(health.GetProperty("status").GetString() == "ok", "Health-check отвечает");

    var downloads = await http.GetFromJsonAsync<JsonElement>("/api/downloads");
    Require(downloads.GetArrayLength() == 2, "Публичный список сборок содержит ученика и учителя");
    var studentDownload = await http.GetAsync("/downloads/pvzp");
    Require(studentDownload.IsSuccessStatusCode, "Сборка ученика скачивается без входа");
    Require((await studentDownload.Content.ReadAsByteArrayAsync()).SequenceEqual(studentBuild),
        "Сборка ученика отдаётся байт в байт");
    Require((await http.GetAsync("/downloads/pvzs")).StatusCode == HttpStatusCode.NotFound,
        "Серверная сборка не публикуется для скачивания");

    var invalidCatalog = await http.GetAsync("/api/catalog/nope/5");
    Require(invalidCatalog.StatusCode == HttpStatusCode.BadRequest, "Неизвестный предмет каталога отклоняется");

    // ---- вход и аккаунты ----

    Require((await http.PostAsJsonAsync("/api/teacher/login", new { password = "" })).StatusCode == HttpStatusCode.Unauthorized,
        "Пустой пароль доступа отклоняется");
    Require((await http.PostAsJsonAsync("/api/teacher/login", new { password = "wrong" })).StatusCode == HttpStatusCode.Unauthorized,
        "Неверный пароль доступа отклоняется");

    var login = await TaskbookOnlineClient.LoginAsync("ezfridgembxd", serverUrl);
    Require(login.AccessToken.Length == 43 && login.ExpiresAt > DateTimeOffset.UtcNow,
        "Вход выдаёт 43-символьный токен с будущим сроком");
    var adminToken = login.AccessToken;

    Require((await http.GetAsync("/api/teacher/sessions")).StatusCode == HttpStatusCode.Unauthorized,
        "Список сессий требует токен");
    using (var forbidden = new HttpRequestMessage(HttpMethod.Get, "/api/teacher/sessions"))
    {
        forbidden.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new string('a', 43));
        Require((await http.SendAsync(forbidden)).StatusCode == HttpStatusCode.Unauthorized,
            "Неизвестный токен отклоняется");
    }

    var teacher = await TaskbookOnlineClient.AccountRegisterAsync(
        "maria.petrova", "Мария Петрова", "Гимназия №1, Минск", "maria@school.by", "teacher-pass-1", serverUrl);
    Require(teacher.AccessToken.Length == 43 && teacher.Profile.Username == "maria.petrova",
        "Регистрация учителя выдаёт веб-токен и профиль");
    Require((await http.PostAsJsonAsync("/api/account/register", new
    {
        username = "other.name", fullName = "Другая", school = "Школа", email = "maria@school.by", password = "teacher-pass-2",
    })).StatusCode == HttpStatusCode.BadRequest, "Повторный email отклоняется");

    var wrongPassword = await CaptureAsync(() =>
        TaskbookOnlineClient.AccountLoginAsync("maria.petrova", "nope", serverUrl));
    Require(wrongPassword is InvalidOperationException, "Неверный пароль кабинета отклоняется");
    var signedIn = await TaskbookOnlineClient.AccountLoginAsync("maria@school.by", "teacher-pass-1", serverUrl);
    Require(signedIn.Profile.Email == "maria@school.by", "Вход по email работает");

    var cabinetToken = signedIn.AccessToken;
    var cabinetPackage = await File.ReadAllBytesAsync(cabinetPackagePath);
    var cabinetUpload = await TaskbookOnlineClient.UploadAsync(cabinetPackagePath, serverUrl, cabinetToken, StoragePlan.FreeTwoWeeks);
    Require(cabinetUpload.Token.Length == 6 && cabinetUpload.Retention == RetentionPeriod.TwoWeeks,
        "Задачник из кабинета получает токен и двухнедельный срок");

    // ---- каталог с подпиской ----

    var catalogItems = await TaskbookOnlineClient.ListCatalogTaskbooksAsync(
        TaskbookCatalogSubjects.Math, 9, serverUrl);
    Require(catalogItems.Count == 1 && catalogItems[0].FileName == catalogFileName, "Каталог отдаёт задачник класса");

    var anonymousCatalog = await TryAsync(async () =>
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/catalog/{TaskbookCatalogSubjects.Math}/9/{catalogFileName}");
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}");
        return await response.Content.ReadAsByteArrayAsync();
    });
    Require(anonymousCatalog.Error is InvalidOperationException, "Каталог без входа запрещён");

    using (var noSubscription = new HttpRequestMessage(
               HttpMethod.Get,
               $"api/catalog/{TaskbookCatalogSubjects.Math}/9/{catalogFileName}"))
    {
        noSubscription.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cabinetToken);
        Require((await http.SendAsync(noSubscription)).StatusCode == HttpStatusCode.Forbidden,
            "Без подписки каталог закрыт");
    }

    using (var keyRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/keys"))
    {
        keyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        keyRequest.Content = JsonContent.Create(new
        {
            kind = "class",
            subject = TaskbookCatalogSubjects.Math,
            classNumber = 9,
            durationDays = 30,
        });
        var keyResponse = await http.SendAsync(keyRequest);
        Require(keyResponse.IsSuccessStatusCode, "Администратор создаёт ключ класса");
        var key = await keyResponse.Content.ReadFromJsonAsync<JsonElement>();
        var code = key.GetProperty("code").GetString();
        Require(code is { Length: 18 } && code.StartsWith("PVZ-", StringComparison.Ordinal), "Код ключа в формате PVZ-XXXX-XXXX-XXXX");

        using var activation = new HttpRequestMessage(HttpMethod.Post, "/api/account/keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", cabinetToken) },
            Content = JsonContent.Create(new { code }),
        };
        var activationResponse = await http.SendAsync(activation);
        Require(activationResponse.IsSuccessStatusCode, "Ключ активируется в кабинете");

        using var duplicate = new HttpRequestMessage(HttpMethod.Post, "/api/account/keys")
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", cabinetToken) },
            Content = JsonContent.Create(new { code }),
        };
        Require((await http.SendAsync(duplicate)).StatusCode == HttpStatusCode.BadRequest,
            "Повторная активация ключа отклоняется");
    }

    var catalogPackage = await TaskbookOnlineClient.DownloadCatalogTaskbookAsync(
        TaskbookCatalogSubjects.Math, 9, catalogFileName, serverUrl, cabinetToken);
    Require(catalogPackage.SequenceEqual(File.ReadAllBytes(Path.Combine(catalogDirectory, TaskbookCatalogSubjects.Math, "9", catalogFileName))),
        "Подписка открывает скачивание каталога");

    // ---- публикация, ученик, прогресс ----

    var upload = await TaskbookOnlineClient.UploadAsync(packagePath, serverUrl, adminToken);
    Require(upload.Id.Length == 32 && upload.Token.Length == 6 && upload.Retention == RetentionPeriod.TwoWeeks,
        "Публикация создаёт сессию с токеном из 6 символов");

    Require(upload.Token.All(character => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Contains(character)),
        "Токен использует однозначный алфавит");

    var invalidStudent = await CaptureAsync(() =>
        TaskbookOnlineClient.StartStudentSessionAsync("!!!", new OnlineStudentInfo("Анна", "Иванова", "7А"), serverUrl));
    Require(invalidStudent is InvalidOperationException, "Некорректный токен ученика отклоняется");

    var sessions = await TaskbookOnlineClient.ListTeacherSessionsAsync(adminToken, serverUrl);
    var legacySession = sessions.Single(session => session.Id == upload.Id);
    Require(legacySession.TokenHint == upload.Token[..2] + "****", "Подсказка токена показывает первые два символа");
    Require(legacySession.TotalTasks == 1 && legacySession.MaximumPoints == 30 && legacySession.Students.Count == 0,
        "Сессия показывает задания и максимум баллов");

    var ownSessions = await TaskbookOnlineClient.ListTeacherSessionsAsync(cabinetToken, serverUrl);
    Require(ownSessions.Count == 1 && ownSessions[0].Id == cabinetUpload.Id && ownSessions[0].OwnerName == "Мария Петрова",
        "Учитель видит только свои задачники");

    var downloaded = await TaskbookOnlineClient.DownloadAsync(upload.Token, serverUrl);
    Require(downloaded.Tasks.Count == 1 && downloaded.Tasks[0].Description == document.Tasks[0].Description,
        "Ученик скачивает и проверяет пакет по токену");

    var student = await TaskbookOnlineClient.StartStudentSessionAsync(
        upload.Token, new OnlineStudentInfo("Анна", "Иванова", "7А"), serverUrl);
    Require(student.SessionId.Length == 43 && student.StartedAt <= DateTimeOffset.UtcNow,
        "Ученик получает сессию");

    await TaskbookOnlineClient.ReportProgressAsync(
        new OnlineProgressUpdate(student.SessionId, [new StudentTaskProgressDto(30, true)]), serverUrl);

    var afterProgress = await TaskbookOnlineClient.ListTeacherSessionsAsync(adminToken, serverUrl);
    var anna = afterProgress.Single(session => session.Id == upload.Id).Students.Single();
    Require(anna is { FirstName: "Анна", LastName: "Иванова", ClassName: "7А", EarnedPoints: 30, MaximumPoints: 30, CompletedTasks: 1 },
        "Учитель видит прогресс ученика");

    var overMax = await CaptureAsync(() => TaskbookOnlineClient.ReportProgressAsync(
        new OnlineProgressUpdate(student.SessionId, [new StudentTaskProgressDto(999, true)]), serverUrl));
    Require(overMax is InvalidOperationException, "Баллы выше максимума задания отклоняются");

    // ---- пересылка файлов ----

    var materialPath = Path.Combine(tempRoot, "материал.txt");
    await File.WriteAllTextAsync(materialPath, "hello material");
    var material = await TaskbookOnlineClient.UploadSessionFileAsync(adminToken, upload.Id, materialPath, serverUrl);
    Require(material.Kind == ExchangeFileKinds.Material && material.Name == "материал.txt", "Учитель загружает материал");

    var sessionFiles = await TaskbookOnlineClient.ListSessionFilesAsync(adminToken, upload.Id, serverUrl);
    Require(sessionFiles.Count == 1 && sessionFiles[0].Id == material.Id, "Список файлов задачника содержит материал");
    Require((await TaskbookOnlineClient.DownloadSessionFileAsync(adminToken, upload.Id, material.Id, serverUrl))
        .SequenceEqual(File.ReadAllBytes(materialPath)), "Учитель скачивает материал байт в байт");

    var studentFiles = await TaskbookOnlineClient.ListStudentFilesAsync(student.SessionId, serverUrl);
    Require(studentFiles.Materials.Count == 1 && studentFiles.Works.Count == 0, "Ученик видит материалы и свои работы");
    Require((await TaskbookOnlineClient.DownloadStudentFileAsync(student.SessionId, material.Id, serverUrl))
        .SequenceEqual(File.ReadAllBytes(materialPath)), "Ученик скачивает материал");

    var workPath = Path.Combine(tempRoot, "ответ.txt");
    await File.WriteAllTextAsync(workPath, "student answer");
    var work = await TaskbookOnlineClient.UploadStudentWorkAsync(student.SessionId, workPath, serverUrl);
    Require(work.Kind == ExchangeFileKinds.Work && work.StudentName == "Анна Иванова (7А)", "Ученик отправляет работу");
    Require((await TaskbookOnlineClient.ListStudentFilesAsync(student.SessionId, serverUrl)).Works.Count == 1,
        "Работа видна ученику");
    Require((await TaskbookOnlineClient.DownloadStudentFileAsync(student.SessionId, work.Id, serverUrl))
        .SequenceEqual(File.ReadAllBytes(workPath)), "Ученик скачивает свою работу");

    await TaskbookOnlineClient.DeleteSessionFileAsync(adminToken, upload.Id, material.Id, serverUrl);
    Require((await TaskbookOnlineClient.ListSessionFilesAsync(adminToken, upload.Id, serverUrl)).Count == 1,
        "Учитель удаляет материал");

    // ---- просмотр экрана ----

    Require(!await TaskbookOnlineClient.CheckScreenWatchAsync(student.SessionId, serverUrl),
        "По умолчанию экран ученика не смотрят");

    using (var watch = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/students/{student.SessionId}/watch"))
    {
        watch.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        Require((await http.SendAsync(watch)).StatusCode == HttpStatusCode.NoContent, "Учитель включает просмотр экрана");
    }

    Require(await TaskbookOnlineClient.CheckScreenWatchAsync(student.SessionId, serverUrl), "Ученик узнаёт, что экран смотрят");

    var frame = new byte[] { 0xff, 0xd8, 0xff, 0xe0, 1, 2, 3 };
    Require(await TaskbookOnlineClient.SendScreenFrameAsync(student.SessionId, frame, serverUrl), "Кадр экрана принят");

    using (var frameRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/students/{student.SessionId}/watch/frame"))
    {
        frameRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var frameResponse = await http.SendAsync(frameRequest);
        Require(frameResponse.IsSuccessStatusCode && (await frameResponse.Content.ReadAsByteArrayAsync()).SequenceEqual(frame),
            "Учитель получает кадр экрана");
    }

    await TaskbookOnlineClient.StopScreenShareAsync(student.SessionId, serverUrl);
    Require(!await TaskbookOnlineClient.CheckScreenWatchAsync(student.SessionId, serverUrl), "Ученик останавливает показ экрана");

    // ---- продление и закрытие ----

    var extended = await TaskbookOnlineClient.ExtendSessionAsync(adminToken, upload.Id, serverUrl);
    Require(extended > DateTimeOffset.UtcNow + TimeSpan.FromDays(13.9), "Продление возвращает новый срок");

    await TaskbookOnlineClient.CloseSessionAsync(adminToken, upload.Id, serverUrl);
    var closed = await CaptureAsync(() => TaskbookOnlineClient.DownloadAsync(upload.Token, serverUrl));
    Require(closed is InvalidOperationException, "Закрытая сессия больше не скачивается");

    Require((await TaskbookOnlineClient.ListTeacherSessionsAsync(cabinetToken, serverUrl)).Count == 1,
        "После закрытия у учителя остаётся свой задачник");

    // ---- повреждённый пакет ----

    var brokenPath = Path.Combine(tempRoot, "broken.pvz");
    await File.WriteAllBytesAsync(brokenPath, "not a pvz package"u8.ToArray());
    var broken = await CaptureAsync(() =>
        TaskbookOnlineClient.UploadAsync(brokenPath, serverUrl, cabinetToken));
    Require(broken is InvalidOperationException, "Повреждённый .pvz отклоняется сервером");

    Console.WriteLine();
    Console.WriteLine("Все проверки пройдены.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"ОШИБКА: {exception}");
    return 1;
}
finally
{
    if (server is { HasExited: false })
    {
        try
        {
            server.Kill(entireProcessTree: true);
            server.WaitForExit(5000);
        }
        catch
        {
            // сервер уже завершился
        }
    }

    try
    {
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, recursive: true);
    }
    catch (IOException)
    {
    }
}

static async Task<Exception?> CaptureAsync(Func<Task> action)
{
    try
    {
        await action();
        return null;
    }
    catch (Exception exception)
    {
        return exception;
    }
}

static async Task<(T? Value, Exception? Error)> TryAsync<T>(Func<Task<T>> action)
{
    try
    {
        return (await action(), null);
    }
    catch (Exception exception)
    {
        return (default, exception);
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
    Console.WriteLine($"OK  {message}");
}

static Process StartServer(string script, string url, string dataDirectory, string catalogDirectory, string downloadsDirectory)
{
    var port = new Uri(url).Port;
    var start = new ProcessStartInfo("node", $"\"{script}\"")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    start.Environment["PORT"] = port.ToString();
    start.Environment["HOST"] = "127.0.0.1";
    start.Environment["PVZ_SERVER_DATA_DIR"] = dataDirectory;
    start.Environment["PVZ_TASKBOOK_CATALOG_DIR"] = catalogDirectory;
    start.Environment["PVZ_DOWNLOAD_DIR"] = downloadsDirectory;
    start.Environment["PVZ_RATE_LIMIT_MULTIPLIER"] = "1000";
    start.Environment["REDIS_URL"] = "";
    start.Environment["BLOB_READ_WRITE_TOKEN"] = "";
    var process = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить pvz-server.");
    process.OutputDataReceived += (_, _) => { };
    process.ErrorDataReceived += (_, _) => { };
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}

static async Task WaitForServerAsync(HttpClient client, Process process)
{
    for (var attempt = 0; attempt < 100; attempt++)
    {
        if (process.HasExited)
            throw new InvalidOperationException($"pvz-server завершился при запуске с кодом {process.ExitCode}.");
        try
        {
            using var response = await client.GetAsync("/health");
            if (response.IsSuccessStatusCode)
                return;
        }
        catch (HttpRequestException)
        {
        }
        await Task.Delay(150);
    }
    throw new TimeoutException("pvz-server не запустился.");
}

static int FreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "PVZ.slnx")))
            return directory.FullName;
        directory = directory.Parent;
    }
    throw new InvalidOperationException("Не найден корень репозитория PVZ.");
}
