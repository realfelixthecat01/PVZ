// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using PVZ.Core;

// Интеграционные тесты клиента PVZ.Core против настоящего сервера pvz-server (Node.js,
// тот же код, что работает на Vercel). Сервер запускается локально с временными данными.

int checks = 0;

var repositoryRoot = FindRepositoryRoot();
var serverScript = Path.Combine(repositoryRoot, "pvz-server", "server.js");
Require(File.Exists(serverScript), "pvz-server/server.js существует");

var port = FreePort();
const string RootTestPassword = "pvz-test-root-password";
var serverUrl = $"http://127.0.0.1:{port}";
var tempRoot = Path.Combine(Path.GetTempPath(), $"pvz-node-test-{Guid.NewGuid():N}");
var dataDirectory = Path.Combine(tempRoot, "data");
var catalogDirectory = Path.Combine(dataDirectory, "blobs", "pvz", "catalog");
var downloadsDirectory = Path.Combine(tempRoot, "downloads");

Process? server = null;
Process? secureServer = null;
string? secureRoot = null;
try
{
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

    server = StartServer(serverScript, serverUrl, dataDirectory, catalogDirectory, downloadsDirectory, RootTestPassword);
    using var http = new HttpClient { BaseAddress = new Uri(serverUrl), Timeout = TimeSpan.FromSeconds(15) };
    await WaitForServerAsync(http, server);

    // ---- чистая логика без сети ----

    const string blankSample = "I ___ to school. She ___ not.";
    Require(FillBlankRules.CountBlanks(blankSample) == 2, "Каждый ряд подчёркиваний - один пропуск");
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

    // ---- новые типы заданий: выбор, соответствие, короткий ответ ----

    var mixedDocument = new TaskbookDocument
    {
        Subject = TaskbookCatalogSubjects.Math,
        ClassNumber = 9,
        Tasks =
        [
            new TaskbookTaskDto
            {
                Description = "Выберите верное утверждение",
                Kind = TaskbookTaskKind.Choice,
                BonusPointsForAllTests = 5,
                Options =
                [
                    new ChoiceOptionDto { Text = "2 x 2 = 4", Correct = true, Points = 20 },
                    new ChoiceOptionDto { Text = "2 x 2 = 5", Points = 15 },
                    new ChoiceOptionDto { Text = "2 x 3 = 7", Points = 15 },
                ],
            },
            new TaskbookTaskDto
            {
                Description = "Соотнесите слова и переводы",
                Kind = TaskbookTaskKind.Matching,
                BonusPointsForAllTests = 5,
                Pairs =
                [
                    new MatchPairDto { Left = "cat", Right = "кот", Points = 15 },
                    new MatchPairDto { Left = "dog", Right = "пёс", Points = 15 },
                ],
            },
            new TaskbookTaskDto
            {
                Description = "Сколько будет 7 умножить на 8?",
                Kind = TaskbookTaskKind.ShortAnswer,
                BonusPointsForAllTests = 5,
                Blanks = [new FillBlankDto { Answer = "56", AcceptedAnswers = ["пятьдесят шесть"], Points = 25 }],
            },
        ],
    };

    Require(TaskbookScoring.GetTaskMaxScore(mixedDocument.Tasks[0]) == 25,
        "Максимум выбора считают только верные варианты");
    Require(TaskbookScoring.GetTaskMaxScore(mixedDocument.Tasks[1]) == 35,
        "Максимум соответствия = пары + бонус");
    Require(TaskbookScoring.GetTaskMaxScore(mixedDocument.Tasks[2]) == 30,
        "Максимум короткого ответа = ответ + бонус");

    var mixedPath = Path.Combine(tempRoot, "mixed.pvz");
    TaskbookPackageIO.Save(mixedPath, mixedDocument);
    var mixedLoaded = TaskbookPackageIO.Load(mixedPath);
    Require(
        mixedLoaded.Tasks[0].Options.Count == 3 && mixedLoaded.Tasks[0].Options[0].Correct &&
        mixedLoaded.Tasks[1].Pairs[1].Right == "пёс" &&
        mixedLoaded.Tasks[2].Blanks[0].AcceptedAnswers.Count == 1,
        "Новые типы заданий переживают сохранение в .pvz");
    Require(TaskbookScoring.GetDocumentMaxScore(mixedLoaded) == 90, "Максимумы новых заданий сходятся в сумме");

    // ---- сборка задачника одним .exe ----

    var templatePath = new[]
    {
        Path.Combine(repositoryRoot, "PVZT", "bin", "Debug", "net10.0-windows", "PlayerTemplate", "PVZP.exe"),
        Path.Combine(repositoryRoot, "PVZP", "bin", "Debug", "net10.0-windows", "PVZP.exe"),
        Path.Combine(repositoryRoot, "PVZP", "bin", "Debug", "net10.0-windows", "win-x64", "publish", "PVZP.exe"),
    }.FirstOrDefault(File.Exists);

    if (templatePath == null)
    {
        Console.WriteLine("ПРОПУСК сборка .exe не проверена: соберите PVZP и PVZT");
    }
    else
    {
        var builtExe = Path.Combine(tempRoot, "zadachnik.exe");
        var build = TaskbookPublisher.PublishExe(builtExe, document, templatePath: templatePath);
        Require(build.Success && build.OutputExePath == builtExe, "Редактор собирает задачник одним .exe");

        var builtLength = new FileInfo(builtExe).Length;
        Require(builtLength > new FileInfo(templatePath).Length, "К шаблону дописан payload с задачником");
        Require(
            TaskbookPayload.TryReadAppended(builtExe, out var embedded) && embedded?.Tasks.Count == 1,
            "Задачник читается прямо из собранного .exe");

        Require(TaskbookPublisher.PublishExe(builtExe, document, templatePath: templatePath).Success,
            "Повторная сборка того же файла проходит");
        Require(new FileInfo(builtExe).Length == builtLength, "Повторная сборка заменяет задачник, а не дописывает второй копией");

        var missingTemplate = TaskbookPublisher.PublishExe(
            Path.Combine(tempRoot, "bez-shablona.exe"), document, templatePath: Path.Combine(tempRoot, "net-takogo-fayla.exe"));
        Require(!missingTemplate.Success && missingTemplate.ErrorMessage?.Contains("PVZP.exe") == true,
            "Без шаблона сборка отдаёт внятную ошибку");
    }

    // ---- сайт и скачивания ----

    foreach (var page in new[] { "/", "/login", "/register", "/cabinet", "/admin" })
    {
        var response = await http.GetAsync(page);
        Require(response.IsSuccessStatusCode, $"Страница {page} отдаётся");
        Require(response.Content.Headers.ContentType?.MediaType == "text/html", $"Страница {page} - HTML");
    }

    var health = await http.GetFromJsonAsync<JsonElement>("/health");
    Require(health.GetProperty("status").GetString() == "ok", "Health-check отвечает");

    var downloads = await http.GetFromJsonAsync<JsonElement>("/api/downloads");
    Require(downloads.GetArrayLength() == 2
        && downloads.EnumerateArray().Count(item => item.GetProperty("id").GetString() == "pvzp") == 1
        && downloads.EnumerateArray().Count(item => item.GetProperty("id").GetString() == "pvzt") == 1,
        "Публичный список сборок содержит ученика и учителя");
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

    var login = await TaskbookOnlineClient.LoginAsync(RootTestPassword, serverUrl);
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
        AccountRoles.Teacher, "Мария Петрова", "teacher-pass-1", "", serverUrl,
        school: "Гимназия №1, Минск", email: "maria@school.by");
    Require(teacher.AccessToken.Length == 43 && teacher.Profile.Role == "teacher"
        && teacher.Profile.Username.Length > 0,
        "Регистрация учителя по двум полям выдаёт веб-токен и имя аккаунта");

    Require((await http.PostAsJsonAsync("/api/account/register", new
    {
        role = "teacher", fullName = "Другая", email = "maria@school.by", password = "teacher-pass-2",
    })).StatusCode == HttpStatusCode.BadRequest, "Повторный email отклоняется");

    var badClass = (await http.PostAsJsonAsync("/api/account/register", new
    {
        role = "student", fullName = "Витя Тестов", className = "девять", password = "parol-viti-1",
    })).StatusCode;
    Require(badClass == HttpStatusCode.BadRequest, "Класс ученика только цифрами и буквой");

    var wrongPassword = await CaptureAsync(() =>
        TaskbookOnlineClient.AccountLoginAsync("Мария Петрова", "nope", serverUrl));
    Require(wrongPassword is InvalidOperationException, "Неверный пароль кабинета отклоняется");
    var signedIn = await TaskbookOnlineClient.AccountLoginAsync("мария   петрова", "teacher-pass-1", serverUrl);
    Require(signedIn.Profile.Email == "maria@school.by", "Вход по русскому имени работает даже с двойными пробелами");
    var byEmail = await TaskbookOnlineClient.AccountLoginAsync("maria@school.by", "teacher-pass-1", serverUrl);
    Require(byEmail.Profile.Id == signedIn.Profile.Id, "Вход по email работает");

    var cabinetToken = signedIn.AccessToken;
    var cabinetUpload = await TaskbookOnlineClient.UploadAsync(cabinetPackagePath, serverUrl, cabinetToken);
    Require(cabinetUpload.Token.Length == 6 && cabinetUpload.Retention == RetentionPeriod.TwoWeeks,
        "Задачник из кабинета получает токен и двухнедельный срок");

    // ---- гость без аккаунта ----

    var guestSecret = Convert.ToBase64String(new byte[48]);
    var guest = await TaskbookOnlineClient.GuestLoginAsync(guestSecret, serverUrl);
    Require(guest.Profile.IsGuest &&
            guest.Profile.Storage.Any(bucket => bucket.Retention == RetentionPeriod.Hours48 && bucket.Limit == -1),
        "Гостевой вход выдаёт профиль гостя с корзиной хранения на 48 часов");
    var guestAgain = await TaskbookOnlineClient.GuestLoginAsync(guestSecret, serverUrl);
    Require(guestAgain.Profile.Id == guest.Profile.Id,
        "Повторный гостевой вход узнаёт того же гостя");

    var guestUpload = await TaskbookOnlineClient.UploadAsync(packagePath, serverUrl, guest.AccessToken);
    Require(guestUpload.Retention == RetentionPeriod.Hours48,
        "Гостю задачник живёт ровно 48 часов");
    Require(
        await CaptureAsync(() => TaskbookOnlineClient.UploadAsync(packagePath, serverUrl, guest.AccessToken))
            is InvalidOperationException,
        "Второй задачник гостя запрещён");

    using (var guestStorage = new HttpRequestMessage(HttpMethod.Post, "/api/upload/begin")
    {
        Headers = { Authorization = new AuthenticationHeaderValue("Bearer", guest.AccessToken) },
        Content = JsonContent.Create(new { target = "account-file", size = 100, retention = "Month1" }),
    })
    {
        Require((await http.SendAsync(guestStorage)).StatusCode == HttpStatusCode.BadRequest,
            "Гостю недоступно личное хранилище");
    }

    // ---- каталог с входом ----

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

    var catalogPackage = await TaskbookOnlineClient.DownloadCatalogTaskbookAsync(
        TaskbookCatalogSubjects.Math, 9, catalogFileName, serverUrl, cabinetToken);
    Require(catalogPackage.SequenceEqual(File.ReadAllBytes(Path.Combine(catalogDirectory, TaskbookCatalogSubjects.Math, "9", catalogFileName))),
        "Учитель без ключей и подписок скачивает задачник из каталога");

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

    var mixedUpload = await TaskbookOnlineClient.UploadAsync(mixedPath, serverUrl, adminToken);
    var mixedSession = (await TaskbookOnlineClient.ListTeacherSessionsAsync(adminToken, serverUrl))
        .Single(session => session.Id == mixedUpload.Id);
    Require(mixedSession.TotalTasks == 3 && mixedSession.MaximumPoints == 90,
        "Сервер считает максимум для выбора, соответствия и короткого ответа");

    var mixedStudent = await TaskbookOnlineClient.StartStudentSessionAsync(
        mixedUpload.Token, new OnlineStudentInfo("Пётр", "Смирнов", "9Б"), serverUrl);
    await TaskbookOnlineClient.ReportProgressAsync(new OnlineProgressUpdate(
        mixedStudent.SessionId,
        [new StudentTaskProgressDto(20, true), new StudentTaskProgressDto(15, true), new StudentTaskProgressDto(25, false)]),
        serverUrl);

    var mixedAfter = await TaskbookOnlineClient.ListTeacherSessionsAsync(adminToken, serverUrl);
    var petr = mixedAfter.Single(session => session.Id == mixedUpload.Id).Students.Single();
    Require(petr is { EarnedPoints: 60, MaximumPoints: 90, CompletedTasks: 2 },
        "Учитель видит частичный прогресс по заданиям новых типов");
    await TaskbookOnlineClient.CloseSessionAsync(adminToken, mixedUpload.Id, serverUrl);

    // ---- аккаунт ученика и его история ----

    var studentAccount = await TaskbookOnlineClient.AccountRegisterAsync(
        AccountRoles.Student, "Аня Козлова", "parol-ani-123", "9а", serverUrl);
    Require(studentAccount.Profile.Role == "student" && studentAccount.Profile.ClassName == "9А",
        "Регистрация ученика запоминает класс и роль");

    var historyUpload = await TaskbookOnlineClient.UploadAsync(cabinetPackagePath, serverUrl, cabinetToken);
    var historyStudent = await TaskbookOnlineClient.StartStudentSessionAsync(
        historyUpload.Token, new OnlineStudentInfo("Аня", "Козлова", "9А"), serverUrl, studentAccount.AccessToken);
    await TaskbookOnlineClient.ReportProgressAsync(
        new OnlineProgressUpdate(historyStudent.SessionId, [new StudentTaskProgressDto(20, true)]), serverUrl);

    var history = await TaskbookOnlineClient.StudentHistoryAsync(studentAccount.AccessToken, serverUrl);
    Require(history.Count == 1, "История ученика появилась после первого решённого задачника");
    var historyRow = history[0];
    Require(historyRow is { EarnedPoints: 20, MaximumPoints: 30, CompletedTasks: 1, TotalTasks: 1 },
        "В истории видны баллы ученика");
    Require(historyRow.Subject == TaskbookCatalogSubjects.Math && historyRow.ClassNumber == 9
        && historyRow.TeacherName == "Мария Петрова",
        "В истории видны предмет, класс и учитель");

    await TaskbookOnlineClient.CloseSessionAsync(cabinetToken, historyUpload.Id, serverUrl);
    Require((await TaskbookOnlineClient.StudentHistoryAsync(studentAccount.AccessToken, serverUrl)).Count == 1,
        "История ученика остаётся после закрытия задачника");

    var otherStudent = await TaskbookOnlineClient.AccountRegisterAsync(
        AccountRoles.Student, "Боря Смирнов", "parol-bori-1", "7Б", serverUrl);
    Require((await TaskbookOnlineClient.StudentHistoryAsync(otherStudent.AccessToken, serverUrl)).Count == 0,
        "Чужая история ученику не видна");

    var studentRelogin = await TaskbookOnlineClient.AccountLoginAsync("аня козлова", "parol-ani-123", serverUrl);
    Require(studentRelogin.Profile.ClassName == "9А", "Ученик входит по своему имени");

    // ---- отчёты учителя ----

    var stats = await TaskbookOnlineClient.GetSessionStatsAsync(adminToken, upload.Id, serverUrl);
    Require(stats is { Students: 1, Online: 1, SolvedEverything: 1, MaximumPoints: 30 },
        "Сервер сводит результаты сессии");
    Require(stats.Tasks.Count == 1 &&
            stats.Tasks[0] is { Title: "Persisted test task", MaxPoints: 30, Solved: 1, Partially: 0, Untouched: 0, AveragePercent: 100 },
        "Сводка по заданию берёт название из задачника");
    Require(stats.BestStudent is { EarnedPoints: 30 } && stats.BestStudent.Name.Contains("Иванова"),
        "Сводка называет лучшего ученика");

    var emptyStats = await TaskbookOnlineClient.GetSessionStatsAsync(cabinetToken, cabinetUpload.Id, serverUrl);
    Require(emptyStats.Students == 0 && emptyStats.Online == 0 && emptyStats.BestStudent is null,
        "Статистика задачника без учеников пустая");

    var csv = System.Text.Encoding.UTF8.GetString(
        await TaskbookOnlineClient.DownloadResultsCsvAsync(adminToken, upload.Id, serverUrl));
    Require(csv.StartsWith('\uFEFF'), "CSV начинается с BOM, чтобы Excel прочитал кириллицу");
    Require(csv.Contains("Иванова") && csv.Contains("Анна") && csv.Contains("30") && csv.Contains("Persisted test task"),
        "CSV содержит ученика, его баллы и название задания");
    Require(csv.Contains("Статистика по заданиям"), "В CSV есть сводка по заданиям");
    Require(csv.Split('\n').Count(line => line.Contains("Иванова")) == 1, "В CSV одна строка на ученика");

    var foreignStats = await CaptureAsync(() =>
        TaskbookOnlineClient.GetSessionStatsAsync(cabinetToken, upload.Id, serverUrl));
    Require(foreignStats is InvalidOperationException, "Чужой задачник статистику не отдаёт");
    Require((await http.GetAsync($"/api/teacher/sessions/{upload.Id}/stats")).StatusCode == HttpStatusCode.Unauthorized,
        "Статистика требует входа");

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

    // ---- безопасность и лимиты на отдельном сервере ----
    // Второй экземпляр pvz-server запускается с жёсткими настройками: три неудачных
    // входа блокируют адрес, а лимит запросов считаем без запаса в тысячу раз.

    const string SecureRootPassword = "pvz-secure-root-password";
    secureRoot = Path.Combine(Path.GetTempPath(), $"pvz-node-secure-{Guid.NewGuid():N}");
    var secureData = Path.Combine(secureRoot, "data");
    var secureCatalog = Path.Combine(secureData, "blobs", "pvz", "catalog");
    var secureDownloads = Path.Combine(secureRoot, "downloads");
    Directory.CreateDirectory(Path.Combine(secureCatalog, TaskbookCatalogSubjects.Math, "9"));
    Directory.CreateDirectory(secureDownloads);
    var secureUrl = $"http://127.0.0.1:{FreePort()}";
    secureServer = StartServer(
        serverScript, secureUrl, secureData, secureCatalog, secureDownloads, SecureRootPassword,
        rateLimitMultiplier: 1, loginFailureLimit: 3);
    using var secure = new HttpClient { BaseAddress = new Uri(secureUrl), Timeout = TimeSpan.FromSeconds(60) };
    await WaitForServerAsync(secure, secureServer);

    var cabinetPage = await secure.GetAsync("/cabinet");
    Require(HasHeader(cabinetPage, "X-Content-Type-Options", "nosniff"), "HTML-страницы запрещают браузеру угадывать тип содержимого");
    Require(HasHeader(cabinetPage, "X-Frame-Options", "DENY"), "Страницы сервера нельзя встроить в чужой iframe");
    Require(HasHeader(cabinetPage, "Content-Security-Policy", "frame-ancestors"), "CSP дополнительно закрывает встраивание страниц");
    Require(!cabinetPage.Headers.Contains("X-Powered-By"), "Сервер не выдаёт версию своего фреймворка");

    const string LockedAddress = "203.0.113.7";
    for (var attempt = 1; attempt <= 3; attempt++)
    {
        using var wrong = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/login")
        {
            Content = JsonContent.Create(new { password = "неверный пароль доступа" }),
        };
        wrong.Headers.Add("X-Forwarded-For", LockedAddress);
        using var wrongResponse = await secure.SendAsync(wrong);
        Require(wrongResponse.StatusCode == HttpStatusCode.Unauthorized, $"Неверный пароль отклонён, попытка {attempt} из 3");
    }

    using (var afterLockout = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/login")
    {
        Content = JsonContent.Create(new { password = SecureRootPassword }),
    })
    {
        afterLockout.Headers.Add("X-Forwarded-For", LockedAddress);
        using var locked = await secure.SendAsync(afterLockout);
        var lockedBody = await locked.Content.ReadAsStringAsync();
        Require(
            locked.StatusCode == HttpStatusCode.TooManyRequests
            && lockedBody.Contains("неудачных попыток")
            && lockedBody.Contains("15 минут"),
            "После трёх неудач вход с этого адреса закрывается на 15 минут, даже пароль верный");
    }

    var secureAdmin = await TaskbookOnlineClient.LoginAsync(SecureRootPassword, secureUrl);
    Require(secureAdmin.AccessToken.Length == 43, "С другого адреса вход по-прежнему работает");

    using (var brokenJson = new HttpRequestMessage(HttpMethod.Post, "/api/account/login")
    {
        Content = new StringContent("{ это не json", Encoding.UTF8, "application/json"),
    })
    {
        using var brokenJsonResponse = await secure.SendAsync(brokenJson);
        Require(brokenJsonResponse.StatusCode == HttpStatusCode.BadRequest, "Нечитаемый JSON в запросе отклоняется");
    }

    const int PackageLimitBytes = 16 * 1024 * 1024;
    using (var oversizePackage = new HttpRequestMessage(HttpMethod.Post, "api/teacher/sessions")
    {
        Content = new ByteArrayContent(new byte[PackageLimitBytes + 500])
        {
            Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") },
        },
    })
    {
        oversizePackage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secureAdmin.AccessToken);
        using var oversizeResponse = await secure.SendAsync(oversizePackage);
        Require(
            oversizeResponse.StatusCode == HttpStatusCode.BadRequest
            && (await oversizeResponse.Content.ReadAsStringAsync()).Contains("16 МиБ"),
            "Задачник весом больше 16 МиБ сервер не принимает");
    }

    var secureUpload = await TaskbookOnlineClient.UploadAsync(packagePath, secureUrl, secureAdmin.AccessToken);

    using (var oversizedWork = new HttpRequestMessage(HttpMethod.Post, "api/upload/begin")
    {
        Content = JsonContent.Create(new
        {
            target = "teacher-material",
            sessionId = secureUpload.Id,
            size = 32 * 1024 * 1024 + 1,
            name = "ogromnyy-fayl.zip",
        }),
    })
    {
        oversizedWork.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secureAdmin.AccessToken);
        using var workResponse = await secure.SendAsync(oversizedWork);
        Require(
            workResponse.StatusCode == HttpStatusCode.BadRequest
            && (await workResponse.Content.ReadAsStringAsync()).Contains("32 МиБ"),
            "Файл обмена крупнее 32 МиБ не проходит");
    }

    using (var traversal = new HttpRequestMessage(
               HttpMethod.Get, $"api/catalog/{TaskbookCatalogSubjects.Math}/9/%2e%2e%2f%2e%2e%2fserver.js"))
    {
        traversal.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secureAdmin.AccessToken);
        using var traversalResponse = await secure.SendAsync(traversal);
        Require(traversalResponse.StatusCode == HttpStatusCode.NotFound, "Выход за папку каталога через ../ не удался");
    }

    using (var wrongClass = new HttpRequestMessage(
               HttpMethod.Get, $"api/catalog/{TaskbookCatalogSubjects.Math}/99/test.pvz"))
    {
        wrongClass.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secureAdmin.AccessToken);
        using var wrongClassResponse = await secure.SendAsync(wrongClass);
        Require(wrongClassResponse.StatusCode == HttpStatusCode.BadRequest, "Класс вне диапазона отклоняется");
    }

    var longName = new string('а', 300);
    var overlongStudent = await CaptureAsync(() => TaskbookOnlineClient.StartStudentSessionAsync(
        secureUpload.Token, new OnlineStudentInfo(longName, "Иванова", "7А"), secureUrl));
    Require(overlongStudent is InvalidOperationException, "Имя ученика длиннее 60 символов не принимается");

    var secureStudent = await TaskbookOnlineClient.StartStudentSessionAsync(
        secureUpload.Token, new OnlineStudentInfo("Пётр", "Сидоров", "7Б"), secureUrl);

    using (var watch = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/students/{secureStudent.SessionId}/watch"))
    {
        watch.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secureAdmin.AccessToken);
        using var _ = await secure.SendAsync(watch);
    }

    Require(
        !await TaskbookOnlineClient.SendScreenFrameAsync(secureStudent.SessionId, new byte[2 * 1024 * 1024 + 1], secureUrl),
        "Кадр экрана крупнее 2 МиБ сервер не сохраняет");
    using (var emptyFrame = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/students/{secureStudent.SessionId}/watch/frame"))
    {
        emptyFrame.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secureAdmin.AccessToken);
        using var frameResponse = await secure.SendAsync(emptyFrame);
        Require(frameResponse.StatusCode == HttpStatusCode.NoContent, "Учитель не получает слишком большой кадр");
    }

    var teacherOne = await TaskbookOnlineClient.AccountRegisterAsync(
        AccountRoles.Teacher, "Анна Коваленко", "pass-kovalenko-1", "", secureUrl);
    var teacherTwo = await TaskbookOnlineClient.AccountRegisterAsync(
        AccountRoles.Teacher, "Олег Дубровский", "pass-dubrovsky-1", "", secureUrl);
    var firstSession = await TaskbookOnlineClient.UploadAsync(packagePath, secureUrl, teacherOne.AccessToken);

    Require((await TaskbookOnlineClient.ListTeacherSessionsAsync(teacherTwo.AccessToken, secureUrl)).Count == 0,
        "Второй учитель не видит чужие задачники");
    Require(
        await CaptureAsync(() => TaskbookOnlineClient.GetSessionStatsAsync(teacherTwo.AccessToken, firstSession.Id, secureUrl))
            is InvalidOperationException,
        "Чужой задачник не отдаёт статистику второму учителю");
    Require(
        await CaptureAsync(() => TaskbookOnlineClient.CloseSessionAsync(teacherTwo.AccessToken, firstSession.Id, secureUrl))
            is InvalidOperationException,
        "Чужой задачник нельзя закрыть второму учителю");
    Require((await TaskbookOnlineClient.ListTeacherSessionsAsync(teacherOne.AccessToken, secureUrl)).Count == 1,
        "Владелец по-прежнему видит свой задачник");

    using var cronResponse = await secure.PostAsync("api/cron/cleanup", null);
    Require(cronResponse.StatusCode == HttpStatusCode.Unauthorized, "Очистка хранилища запускается только с секретом");

    using (var hijackBegin = new HttpRequestMessage(HttpMethod.Post, "api/upload/begin")
    {
        Content = JsonContent.Create(new { target = "teacher-session", size = 4096 }),
    })
    {
        hijackBegin.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherOne.AccessToken);
        using var hijackResponse = await secure.SendAsync(hijackBegin);
        using var startedJson = JsonDocument.Parse(await hijackResponse.Content.ReadAsStringAsync());
        var uploadId = startedJson.RootElement.GetProperty("uploadId").GetString();
        using var hijackComplete = new HttpRequestMessage(HttpMethod.Post, "api/upload/complete")
        {
            Content = JsonContent.Create(new { uploadId }),
        };
        hijackComplete.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacherTwo.AccessToken);
        using var stolen = await secure.SendAsync(hijackComplete);
        Require(stolen.StatusCode == HttpStatusCode.Unauthorized, "Чужую загрузку нельзя завершить от своего имени");
    }

    HttpStatusCode lastLoginAttempt = HttpStatusCode.OK;
    string? floodMessage = null;
    for (var attempt = 1; attempt <= 12; attempt++)
    {
        using var flood = new HttpRequestMessage(HttpMethod.Post, "/api/account/login")
        {
            Content = JsonContent.Create(new { identifier = $"net-takogo-{attempt}", password = "parol" }),
        };
        using var floodResponse = await secure.SendAsync(flood);
        lastLoginAttempt = floodResponse.StatusCode;
        if (floodMessage is null && floodResponse.StatusCode == HttpStatusCode.TooManyRequests)
            floodMessage = await floodResponse.Content.ReadAsStringAsync();
    }

    Require(lastLoginAttempt == HttpStatusCode.TooManyRequests, "Больше десяти попыток входа в минуту не проходит");
    Require(floodMessage?.Contains("Слишком много запросов") == true, "Сервер объясняет причину ограничения запросов");

    Console.WriteLine();
    Console.WriteLine($"Проверок: {checks}. Все пройдены.");
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
    StopServer(server);
    StopServer(secureServer);
    DeleteDirectory(tempRoot);
    DeleteDirectory(secureRoot);
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

void Require(bool condition, string message)
{
    checks++;
    if (!condition)
        throw new InvalidOperationException(message);
    Console.WriteLine($"OK  {message}");
}

static void StopServer(Process? process)
{
    if (process is not { HasExited: false })
        return;

    try
    {
        process.Kill(entireProcessTree: true);
        process.WaitForExit(5000);
    }
    catch
    {
        // сервер уже завершился
    }
}

static void DeleteDirectory(string? path)
{
    try
    {
        if (path is not null && Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
    catch (IOException)
    {
    }
}

static bool HasHeader(HttpResponseMessage message, string name, string expected)
{
    return message.Headers.TryGetValues(name, out var values)
        && values.Any(value => value.Contains(expected, StringComparison.Ordinal));
}

static Process StartServer(
    string script,
    string url,
    string dataDirectory,
    string catalogDirectory,
    string downloadsDirectory,
    string rootPassword,
    int rateLimitMultiplier = 1000,
    int loginFailureLimit = 5)
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
    start.Environment["PVZ_RATE_LIMIT_MULTIPLIER"] = rateLimitMultiplier.ToString();
    start.Environment["PVZ_ROOT_PASSWORD"] = rootPassword;
    start.Environment["PVZ_LOGIN_FAILURE_LIMIT"] = loginFailureLimit.ToString();
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
