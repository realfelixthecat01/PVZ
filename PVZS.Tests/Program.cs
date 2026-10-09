using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PVZ.Core;
using PVZS;

var repositoryRoot = FindRepositoryRoot();
var serverAssembly = new[]
{
    Environment.GetEnvironmentVariable("PVZ_SERVER_TEST_ASSEMBLY"),
    Path.Combine(AppContext.BaseDirectory, "PVZS.dll"),
    Path.Combine(repositoryRoot, "PVZS", "verify-output", "Debug", "net10.0", "PVZS.dll"),
    Path.Combine(repositoryRoot, "PVZS", "bin", "Debug", "net10.0", "PVZS.dll"),
}.FirstOrDefault(path => path is not null && File.Exists(path))
    ?? throw new FileNotFoundException("Build PVZS before running integration tests.");

var portListener = new TcpListener(IPAddress.Loopback, 0);
portListener.Start();
var port = ((IPEndPoint)portListener.LocalEndpoint).Port;
portListener.Stop();
var serverUrl = $"http://127.0.0.1:{port}";
var dataDirectory = Path.Combine(Path.GetTempPath(), $"pvz-server-test-{Guid.NewGuid():N}");
var catalogDirectory = Path.Combine(dataDirectory, "catalog");
var downloadsDirectory = Path.Combine(dataDirectory, "downloads");
var expiryDirectory = Path.Combine(Path.GetTempPath(), $"pvz-expiry-test-{Guid.NewGuid():N}");
using var client = new HttpClient { BaseAddress = new Uri(serverUrl), Timeout = TimeSpan.FromSeconds(5) };
Process? server = null;

try
{
    var addressEnvBackup = Environment.GetEnvironmentVariable("PVZ_SERVER_URL");
    var addressFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PVZ",
        "server-address.txt");
    var addressFileBackup = File.Exists(addressFile) ? File.ReadAllText(addressFile) : null;
    try
    {
        Environment.SetEnvironmentVariable("PVZ_SERVER_URL", null);
        ServerAddressStore.Save(null);
        if (TaskbookOnlineClient.RequireExplicitServerUrl)
            Require(
                ThrowsAddressRequired(TaskbookOnlineClient.ResolveServerUrl),
                "Without configuration the client requires an explicit server address");
        else
            Require(
                TaskbookOnlineClient.ResolveServerUrl() ==
                TaskbookOnlineClient.NormalizeServerUrl(TaskbookOnlineClient.DefaultServerUrl),
                "Without configuration the client falls back to the default server address");

        ServerAddressStore.Save("https://abc-xyz.trycloudflare.com");
        Require(
            TaskbookOnlineClient.ResolveServerUrl() == "https://abc-xyz.trycloudflare.com/",
            "The address saved through the UI is used for resolution");

        Environment.SetEnvironmentVariable("PVZ_SERVER_URL", "https://env.example.com");
        Require(
            TaskbookOnlineClient.ResolveServerUrl() == "https://env.example.com/",
            "PVZ_SERVER_URL wins over the stored address");
        Environment.SetEnvironmentVariable("PVZ_SERVER_URL", null);

        Require(
            TaskbookOnlineClient.NormalizeServerUrl("abc-xyz.trycloudflare.com") ==
            "https://abc-xyz.trycloudflare.com/",
            "Scheme-less addresses are normalized to https");
        Require(
            TaskbookOnlineClient.NormalizeServerUrl("localhost:1729") == "http://localhost:1729/",
            "Localhost addresses keep http and the port is accepted");

        ServerAddressStore.Save("   ");
        Require(ServerAddressStore.Load() is null, "An empty address clears the stored value");
        if (TaskbookOnlineClient.RequireExplicitServerUrl)
            Require(
                ThrowsAddressRequired(TaskbookOnlineClient.ResolveServerUrl),
                "After clearing the store the address is required again");
        else
            Require(
                TaskbookOnlineClient.ResolveServerUrl() ==
                TaskbookOnlineClient.NormalizeServerUrl(TaskbookOnlineClient.DefaultServerUrl),
                "After clearing the store the default address is used again");
    }
    finally
    {
        Environment.SetEnvironmentVariable("PVZ_SERVER_URL", addressEnvBackup);
        if (addressFileBackup is null)
        {
            if (File.Exists(addressFile))
                File.Delete(addressFile);
        }
        else
        {
            File.WriteAllText(addressFile, addressFileBackup);
        }
    }

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
    server = StartServer(serverAssembly, serverUrl, dataDirectory, catalogDirectory, downloadsDirectory);
    await WaitForServerAsync(client, server);

    // Кладём сборки в downloads, чтобы сайт и админ-панель увидели их доступными.
    Directory.CreateDirectory(downloadsDirectory);
    var studentBuild = Encoding.UTF8.GetBytes("pvzp-student-build");
    var teacherBuild = Encoding.UTF8.GetBytes("pvzt-teacher-build");
    var serverBuild = Encoding.UTF8.GetBytes("pvzs-server-build");
    await File.WriteAllBytesAsync(Path.Combine(downloadsDirectory, "PVZP.exe"), studentBuild);
    await File.WriteAllBytesAsync(Path.Combine(downloadsDirectory, "PVZT.exe"), teacherBuild);
    await File.WriteAllBytesAsync(Path.Combine(downloadsDirectory, "PVZS.exe"), serverBuild);

    using var noRedirectClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
    {
        BaseAddress = new Uri(serverUrl),
    };
    using (var landing = await noRedirectClient.GetAsync("/", HttpCompletionOption.ResponseHeadersRead))
    {
        Require(landing.IsSuccessStatusCode, "Landing page is served at the root");
        var landingHtml = await landing.Content.ReadAsStringAsync();
        Require(landingHtml.Contains("Пункт Выдачи Задачек", StringComparison.Ordinal) &&
            landingHtml.Contains("href=\"/pricing\"", StringComparison.Ordinal) &&
            landingHtml.Contains("href=\"/admin\"", StringComparison.Ordinal),
            "Landing shows the product brand and links to pricing and the teacher panel");
        Require(landingHtml.Contains("reveal", StringComparison.Ordinal) &&
            landingHtml.Contains("data-i18n=\"landing.hero.title\"", StringComparison.Ordinal) &&
            landingHtml.Contains("data-lang=\"en\"", StringComparison.Ordinal) &&
            landingHtml.Contains("data-currency=\"RUB\"", StringComparison.Ordinal),
            "Landing carries fade-in reveal blocks and RU/EN with BYN/USD/RUB switchers");
        Require(landingHtml.Contains("data-i18n=\"downloads.title\"", StringComparison.Ordinal) &&
            landingHtml.Contains("href=\"/downloads/pvzt\"", StringComparison.Ordinal) &&
            landingHtml.Contains("href=\"/downloads/pvzp\"", StringComparison.Ordinal),
            "Landing offers the PVZT and PVZP downloads when the builds are present");
    }

    using (var downloads = await client.GetAsync("/api/downloads"))
    {
        Require(downloads.IsSuccessStatusCode, "Download list is public");
        var items = await downloads.Content.ReadFromJsonAsync<DownloadInfoDto[]>();
        Require(items is { Length: 2 } &&
            items.All(item => item.Available) &&
            items.Any(item => item.Id == DownloadCatalog.TeacherId && item.Role == "teacher") &&
            items.Any(item => item.Id == DownloadCatalog.StudentId && item.Role == "student"),
            "Download list exposes the available PVZT and PVZP builds");
    }

    using (var studentDownload = await client.GetAsync("/downloads/pvzp"))
    {
        studentDownload.EnsureSuccessStatusCode();
        Require((await studentDownload.Content.ReadAsByteArrayAsync()).SequenceEqual(studentBuild),
            "The student build downloads byte-for-byte");
        Require(studentDownload.Content.Headers.ContentDisposition?.FileName?.Contains("PVZP.exe", StringComparison.Ordinal) is true,
            "The student build is named PVZP.exe");
    }

    using (var hiddenServer = await client.GetAsync("/downloads/pvzs"))
        Require(hiddenServer.StatusCode == HttpStatusCode.NotFound, "The server build is not downloadable without admin rights");
    using (var unknownBuild = await client.GetAsync("/downloads/nonsense"))
        Require(unknownBuild.StatusCode == HttpStatusCode.NotFound, "Unknown builds are rejected");
    using (var adminDownloads = await client.GetAsync("/api/admin/downloads"))
        Require(adminDownloads.StatusCode == HttpStatusCode.Unauthorized, "The admin build list asks for admin rights");


    using (var pricing = await client.GetAsync("/pricing"))
    {
        Require(pricing.IsSuccessStatusCode, "Pricing page route");
        var pricingHtml = await pricing.Content.ReadAsStringAsync();
        Require(pricingHtml.Contains("Один предмет", StringComparison.Ordinal) &&
            pricingHtml.Contains("data-price=\"9.99\"", StringComparison.Ordinal) &&
            pricingHtml.Contains("data-price=\"4.99\"", StringComparison.Ordinal) &&
            pricingHtml.Contains("48 часов", StringComparison.Ordinal) &&
            pricingHtml.Contains("Про+", StringComparison.Ordinal),
            "Pricing page lists the new storage tiers and subscription prices");
        Require(pricingHtml.Contains("data-i18n=\"pricing.faq.title\"", StringComparison.Ordinal) &&
            pricingHtml.Contains("data-i18n=\"pricing.plan.activate\"", StringComparison.Ordinal),
            "Pricing page keys every section for RU/EN switching");
    }

    using (var dashboard = await client.GetAsync("/admin"))
    {
        Require(dashboard.IsSuccessStatusCode, "Teacher dashboard route");
        var html = await dashboard.Content.ReadAsStringAsync();
        Require(html.Contains(">Войти<", StringComparison.Ordinal) &&
            html.Contains("Имя аккаунта или email", StringComparison.Ordinal),
            "Dashboard asks for the account name (or email) and password");
        Require(html.Contains("Продлить", StringComparison.Ordinal) &&
            html.Contains("Закрыть", StringComparison.Ordinal) &&
            html.Contains("Скачать", StringComparison.Ordinal),
            "Dashboard offers extend, close and download actions");
        Require(html.Contains("2 нед", StringComparison.Ordinal), "Dashboard shows the free two-week plan badge");
        Require(!html.Contains("Создать аккаунт", StringComparison.Ordinal) &&
            !html.Contains("API-ключ", StringComparison.Ordinal),
            "Dashboard no longer offers teacher registration or API keys");
        Require(html.Contains("Ключи активации", StringComparison.Ordinal) &&
            html.Contains("Учителя", StringComparison.Ordinal) &&
            html.Contains("Активные сессии", StringComparison.Ordinal),
            "Dashboard includes the activation key generator and the all-teachers board with active sessions");
        Require(html.Contains("Сборки приложений", StringComparison.Ordinal) &&
            html.Contains("id=\"buildsCard\"", StringComparison.Ordinal) &&
            html.Contains("/api/admin/downloads", StringComparison.Ordinal),
            "Dashboard includes the admin-only builds card with the server download");
    }

    using (var loginPage = await client.GetAsync("/login"))
    {
        Require(loginPage.IsSuccessStatusCode, "Teacher login page route");
        var loginHtml = await loginPage.Content.ReadAsStringAsync();
        Require(loginHtml.Contains("Вход для учителя", StringComparison.Ordinal) &&
            loginHtml.Contains("id=\"identifier\"", StringComparison.Ordinal) &&
            loginHtml.Contains("href=\"/register\"", StringComparison.Ordinal),
            "Login page asks for the account name and links to registration");
    }

    using (var registerPage = await client.GetAsync("/register"))
    {
        Require(registerPage.IsSuccessStatusCode, "Teacher registration page route");
        var registerHtml = await registerPage.Content.ReadAsStringAsync();
        Require(registerHtml.Contains("Регистрация учителя", StringComparison.Ordinal) &&
            registerHtml.Contains("id=\"username\"", StringComparison.Ordinal) &&
            registerHtml.Contains("href=\"/login\"", StringComparison.Ordinal),
            "Registration page collects the account name and links to login");
    }

    using (var cabinetPage = await client.GetAsync("/cabinet"))
    {
        Require(cabinetPage.IsSuccessStatusCode, "Teacher cabinet page route");
        var cabinetHtml = await cabinetPage.Content.ReadAsStringAsync();
        Require(cabinetHtml.Contains("Личный кабинет", StringComparison.Ordinal) &&
            cabinetHtml.Contains("Профиль", StringComparison.Ordinal) &&
            cabinetHtml.Contains("cabinet.username", StringComparison.Ordinal) &&
            cabinetHtml.Contains("Файлы в облаке", StringComparison.Ordinal) &&
            cabinetHtml.Contains("Ключ активации", StringComparison.Ordinal),
            "Cabinet page offers profile settings with the account name, activation keys and cloud files with delete timers");
    }

    using (var invalidCatalogSubject = await client.GetAsync("/api/catalog/not-a-subject/9"))
        Require(invalidCatalogSubject.StatusCode == HttpStatusCode.BadRequest, "Reject unknown catalog subjects");
    using (var invalidCatalogClass = await client.GetAsync("/api/catalog/math/12"))
        Require(invalidCatalogClass.StatusCode == HttpStatusCode.BadRequest, "Reject catalog classes outside 1 through 11");
    using (var lowInformaticsClass = await client.GetAsync("/api/catalog/informatics/5"))
        Require(lowInformaticsClass.StatusCode == HttpStatusCode.BadRequest, "Reject informatics classes below grade 6");
    using (var lowEnglishClass = await client.GetAsync("/api/catalog/english/2"))
        Require(lowEnglishClass.StatusCode == HttpStatusCode.BadRequest, "Reject English classes below grade 3");

    Require(!TaskbookCatalogSubjects.IsValidClass(TaskbookCatalogSubjects.Informatics, 5) &&
        TaskbookCatalogSubjects.IsValidClass(TaskbookCatalogSubjects.Informatics, 6) &&
        !TaskbookCatalogSubjects.IsValidClass(TaskbookCatalogSubjects.English, 2) &&
        TaskbookCatalogSubjects.IsValidClass(TaskbookCatalogSubjects.English, 3) &&
        TaskbookCatalogSubjects.IsValidClass(TaskbookCatalogSubjects.Math, 1),
        "Subject grade ranges are correct");

    var blankSample = "Students _ English at school. They ___ books every day.";
    Require(FillBlankRules.CountBlanks(blankSample) == 2, "Each underscore run counts as one blank");
    Require(FillBlankRules.SplitSegments(blankSample).Length == 3, "Text splits into blanks + 1 segments");
    Require(FillBlankRules.Normalize("  Don’t   Like ") == "don't like",
        "Answer normalization folds case, apostrophes and repeated spaces");
    var rulesBlank = new FillBlankDto { Answer = "doesn't", AcceptedAnswers = ["does not"] };
    Require(FillBlankRules.Matches(rulesBlank, "DOES NOT") && FillBlankRules.Matches(rulesBlank, "doesn’t"),
        "Accepted variants match across case and apostrophe styles");
    Require(!FillBlankRules.Matches(rulesBlank, "doesnt") && !FillBlankRules.Matches(rulesBlank, "   "),
        "Wrong and empty answers are rejected");
    var (splitAnswer, splitVariants) = FillBlankRules.SplitAnswerVariants("went | go | goes | WENT");
    Require(splitAnswer == "went" && splitVariants is ["go", "goes"],
        "Editor pipe syntax keeps the first answer and deduplicates variants");
    var syncedBlanks = new List<FillBlankDto> { new() };
    Require(FillBlankRules.SyncBlanks(syncedBlanks, blankSample) && syncedBlanks.Count == 2,
        "Blank list follows the underscore count in the text");

    using (var emptyLogin = await client.PostAsJsonAsync("/api/teacher/login", new { password = "" }))
        Require(emptyLogin.StatusCode == HttpStatusCode.Unauthorized, "Reject empty password");
    using (var wrongLogin = await client.PostAsJsonAsync("/api/teacher/login", new { password = "wrong-password" }))
        Require(wrongLogin.StatusCode == HttpStatusCode.Unauthorized, "Reject wrong access password on the server");
    var login = await TaskbookOnlineClient.LoginAsync(TaskbookStore.DefaultAccessPassword, serverUrl);
    Require(login.AccessToken.Length == 43, "Server login issues a 43-character bearer token");
    Require(login.ExpiresAt > DateTimeOffset.UtcNow, "Access token has a future expiry");
    var accessToken = login.AccessToken;

    using (var noToken = await client.GetAsync("/api/teacher/sessions"))
        Require(noToken.StatusCode == HttpStatusCode.Unauthorized, "Reject session list without token");
    using (var fakeToken = new HttpRequestMessage(HttpMethod.Get, "/api/teacher/sessions"))
    {
        fakeToken.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new string('A', 43));
        using var response = await client.SendAsync(fakeToken);
        Require(response.StatusCode == HttpStatusCode.Unauthorized, "Reject unknown access token");
    }
    using (var noTokenUpload = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/sessions"))
    {
        noTokenUpload.Content = new ByteArrayContent([1, 2, 3]);
        noTokenUpload.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await client.SendAsync(noTokenUpload);
        Require(response.StatusCode == HttpStatusCode.Unauthorized, "Reject upload without access token");
    }

    using (var invalidRequest = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/sessions"))
    {
        invalidRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        invalidRequest.Content = new ByteArrayContent([1, 2, 3]);
        invalidRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var invalidResponse = await client.SendAsync(invalidRequest);
        Require(invalidResponse.StatusCode == HttpStatusCode.BadRequest, "Reject invalid package upload");
    }

    using (var invalidStudentToken = await client.GetAsync("/api/taskbooks/TOO-LONG"))
        Require(invalidStudentToken.StatusCode == HttpStatusCode.BadRequest, "Reject malformed student token");

    var packagePath = Path.Combine(Path.GetTempPath(), $"pvz-server-test-{Guid.NewGuid():N}.pvz");
    var fillPackagePath = Path.Combine(Path.GetTempPath(), $"pvz-fill-test-{Guid.NewGuid():N}.pvz");
    try
    {
        TaskbookPackageIO.Save(packagePath, document);

        var fillDocument = new TaskbookDocument
        {
            Subject = TaskbookCatalogSubjects.English,
            ClassNumber = 5,
            Tasks =
            [
                new TaskbookTaskDto
                {
                    Kind = TaskbookTaskKind.FillInBlanks,
                    Description = "She _ like apples. We _ bananas every morning.",
                    BonusPointsForAllTests = 10,
                    Blanks =
                    [
                        new FillBlankDto { Answer = "doesn't", AcceptedAnswers = ["does not"], Points = 10 },
                        new FillBlankDto { Answer = "eat", AcceptedAnswers = ["have"], Points = 10 },
                    ],
                },
            ],
        };
        TaskbookPackageIO.Save(fillPackagePath, fillDocument);
        var fillLoaded = TaskbookPackageIO.Load(fillPackagePath);
        var fillTask = fillLoaded.Tasks.Single();
        Require(fillLoaded.Subject == TaskbookCatalogSubjects.English &&
            fillTask.Kind == TaskbookTaskKind.FillInBlanks &&
            fillTask.Blanks.Count == 2 &&
            fillTask.Blanks[0].Answer == "doesn't" &&
            fillTask.Blanks[0].AcceptedAnswers is ["does not"] &&
            fillTask.Blanks[1].Answer == "eat" &&
            TaskbookScoring.GetTaskMaxScore(fillTask) == 30,
            "Fill-blank task with accepted variants survives the package round-trip and scores 30 points");

        var englishClassDirectory = Path.Combine(catalogDirectory, TaskbookCatalogSubjects.English, "5");
        Directory.CreateDirectory(englishClassDirectory);
        var englishFileName = "English5ClassFillBlanks.pvz";
        File.Copy(fillPackagePath, Path.Combine(englishClassDirectory, englishFileName));
        var englishItems = await TaskbookOnlineClient.ListCatalogTaskbooksAsync(
            TaskbookCatalogSubjects.English,
            5,
            serverUrl);
        Require(englishItems.Count == 1 && englishItems[0].FileName == englishFileName,
            "Catalog lists English fill-blank packages for the selected class");
        var englishPackage = await TaskbookOnlineClient.DownloadCatalogTaskbookAsync(
            TaskbookCatalogSubjects.English,
            5,
            englishFileName,
            serverUrl,
            accessToken);
        var englishDownloadPath = Path.Combine(Path.GetTempPath(), $"pvz-english-catalog-{Guid.NewGuid():N}.pvz");
        try
        {
            await File.WriteAllBytesAsync(englishDownloadPath, englishPackage);
            var englishDocument = TaskbookPackageIO.Load(englishDownloadPath);
            Require(englishDocument.Subject == TaskbookCatalogSubjects.English &&
                englishDocument.ClassNumber == 5 &&
                englishDocument.Tasks.Single().Kind == TaskbookTaskKind.FillInBlanks &&
                englishDocument.Tasks.Single().Blanks.Count == 2,
                "Downloaded English catalog package keeps the fill-blank task");
        }
        finally
        {
            if (File.Exists(englishDownloadPath))
                File.Delete(englishDownloadPath);
        }

        var catalogClassDirectory = Path.Combine(catalogDirectory, TaskbookCatalogSubjects.Math, "9");
        Directory.CreateDirectory(catalogClassDirectory);
        var catalogFileName = "Math9ClassParagraph1.pvz";
        File.Copy(packagePath, Path.Combine(catalogClassDirectory, catalogFileName));
        var catalogItems = await TaskbookOnlineClient.ListCatalogTaskbooksAsync(
            TaskbookCatalogSubjects.Math,
            9,
            serverUrl);
        Require(catalogItems.Count == 1 && catalogItems[0].FileName == catalogFileName,
            "Catalog lists packages from subject and class folders");
        var catalogPackage = await TaskbookOnlineClient.DownloadCatalogTaskbookAsync(
            TaskbookCatalogSubjects.Math,
            9,
            catalogFileName,
            serverUrl,
            accessToken);
        var catalogDownloadPath = Path.Combine(Path.GetTempPath(), $"pvz-catalog-test-{Guid.NewGuid():N}.pvz");
        try
        {
            await File.WriteAllBytesAsync(catalogDownloadPath, catalogPackage);
            var catalogDocument = TaskbookPackageIO.Load(catalogDownloadPath);
            Require(catalogDocument.Subject == TaskbookCatalogSubjects.Math && catalogDocument.ClassNumber == 9,
                "Catalog downloads the selected valid PVZ package");
        }
        finally
        {
            if (File.Exists(catalogDownloadPath))
                File.Delete(catalogDownloadPath);
        }

        using (var anonymousCatalog = await client.GetAsync($"/api/catalog/{TaskbookCatalogSubjects.Math}/9/{catalogFileName}"))
            Require(anonymousCatalog.StatusCode == HttpStatusCode.Unauthorized,
                "Anonymous catalog download asks for a cabinet login");

        var registerResponse = await client.PostAsJsonAsync("/api/account/register", new
        {
            username = "maria.petrova",
            fullName = "Мария Петрова",
            school = "Гимназия №1, Минск",
            email = "maria@school.by",
            password = "SuperSecret1",
        });
        Require(registerResponse.StatusCode == HttpStatusCode.OK, "Teacher registration succeeds from the site form");
        var registration = await registerResponse.Content.ReadFromJsonAsync<TestAccountAuthResponse>();
        Require(registration is { AccessToken.Length: 43 }, "Registration issues a 43-character web token");
        Require(registration!.Profile.Username == "maria.petrova" &&
            registration.Profile.Plan == StorageLimits.Free &&
            registration.Profile.PlanDisplayName == "Бесплатный" &&
            registration.Profile.Storage.Count == 1 &&
            registration.Profile.Storage[0].Retention == RetentionPeriod.Hours48 &&
            registration.Profile.Storage[0].Limit == 5,
            "A fresh cabinet starts on the free 48-hour plan with five taskbooks");
        var accountToken = registration.AccessToken;

        using (var duplicateEmail = await client.PostAsJsonAsync("/api/account/register", new
        {
            username = "drugoy.uchitel",
            fullName = "Другой Учитель",
            school = "Школа №2",
            email = "MARIA@school.by",
            password = "SuperSecret1",
        }))
            Require(duplicateEmail.StatusCode == HttpStatusCode.BadRequest, "Duplicate email registration is rejected");

        using (var duplicateUsername = await client.PostAsJsonAsync("/api/account/register", new
        {
            username = "Maria.Petrova",
            fullName = "Третий Учитель",
            school = "Школа №3",
            email = "third@school.by",
            password = "SuperSecret1",
        }))
            Require(duplicateUsername.StatusCode == HttpStatusCode.BadRequest, "Duplicate account name registration is rejected regardless of case");

        using (var cyrillicUsername = await client.PostAsJsonAsync("/api/account/register", new
        {
            username = "мария",
            fullName = "Четвёртый Учитель",
            school = "Школа №4",
            email = "fourth@school.by",
            password = "SuperSecret1",
        }))
            Require(cyrillicUsername.StatusCode == HttpStatusCode.BadRequest, "Account names accept only latin letters, digits, dot, dash and underscore");

        using (var wrongAccountLogin = await client.PostAsJsonAsync("/api/account/login", new { identifier = "maria.petrova", password = "wrong-password" }))
            Require(wrongAccountLogin.StatusCode == HttpStatusCode.Unauthorized, "Wrong cabinet password is rejected");

        using (var accountLogin = await client.PostAsJsonAsync("/api/account/login", new { identifier = "maria.petrova", password = "SuperSecret1" }))
        {
            Require(accountLogin.StatusCode == HttpStatusCode.OK, "Cabinet login by account name succeeds");
            var signedIn = await accountLogin.Content.ReadFromJsonAsync<TestAccountAuthResponse>();
            Require(signedIn!.Profile.Email == "maria@school.by" && signedIn.Profile.School == "Гимназия №1, Минск",
                "Login returns the saved profile");
        }

        using (var emailLogin = await client.PostAsJsonAsync("/api/account/login", new { identifier = "MARIA@school.by", password = "SuperSecret1" }))
            Require(emailLogin.StatusCode == HttpStatusCode.OK, "Cabinet login by email still works");

        using (var updateProfile = new HttpRequestMessage(HttpMethod.Put, "/api/account/me"))
        {
            updateProfile.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            updateProfile.Content = JsonContent.Create(new { fullName = "Мария Петрова-Сидорова", school = "Лицей №2, Минск" });
            using var response = await client.SendAsync(updateProfile);
            Require(response.StatusCode == HttpStatusCode.OK, "Teacher edits the cabinet profile");
            var updated = await response.Content.ReadFromJsonAsync<AccountProfileDto>();
            Require(updated!.FullName == "Мария Петрова-Сидорова" && updated.School == "Лицей №2, Минск",
                "Profile update is stored in the cabinet");
        }

        using (var freeCatalog = new HttpRequestMessage(HttpMethod.Get, $"/api/catalog/{TaskbookCatalogSubjects.Math}/9/{catalogFileName}"))
        {
            freeCatalog.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            using var response = await client.SendAsync(freeCatalog);
            Require(response.StatusCode == HttpStatusCode.Forbidden, "Catalog download without a subscription is forbidden");
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Require(problem.GetProperty("error").GetString()?.Contains("Нужна подписка", StringComparison.Ordinal) is true,
                "Catalog gate explains that a subscription key is required");
        }

        var cabinetPackage = await File.ReadAllBytesAsync(packagePath);
        var cabinetIds = new List<string>();
        for (var index = 0; index < 5; index++)
        {
            using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/account/files")
            {
                Content = BuildCabinetUpload(cabinetPackage, "Hours48"),
            };
            uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            using var response = await client.SendAsync(uploadRequest);
            Require(response.StatusCode == HttpStatusCode.OK, $"Cabinet upload {index + 1} of five fits the free plan");
            var receipt = await response.Content.ReadFromJsonAsync<TeacherSessionUploadReceipt>();
            Require(receipt is { Retention: RetentionPeriod.Hours48 } &&
                receipt.ExpiresAt < DateTimeOffset.UtcNow + TimeSpan.FromHours(48.1),
                "Free cabinet upload stores the file for 48 hours");
            cabinetIds.Add(receipt!.Id);
        }

        using (var overQuota = new HttpRequestMessage(HttpMethod.Post, "/api/account/files")
        {
            Content = BuildCabinetUpload(cabinetPackage, "Hours48"),
        })
        {
            overQuota.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            using var response = await client.SendAsync(overQuota);
            Require(response.StatusCode == HttpStatusCode.BadRequest, "The sixth free taskbook is rejected");
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Require(problem.GetProperty("error").GetString()?.Contains("можно хранить 5 задачников", StringComparison.Ordinal) is true,
                "Quota error explains the free plan limit");
        }

        using (var cabinetFiles = new HttpRequestMessage(HttpMethod.Get, "/api/account/files"))
        {
            cabinetFiles.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            using var response = await client.SendAsync(cabinetFiles);
            response.EnsureSuccessStatusCode();
            var files = await response.Content.ReadFromJsonAsync<CabinetFileDto[]>();
            Require(files is { Length: 5 } && files.All(file => file.Retention == RetentionPeriod.Hours48),
                "Cabinet lists five files with their deletion timer");
        }

        using (var ownerDownload = new HttpRequestMessage(HttpMethod.Get, $"/api/account/files/{cabinetIds[1]}/download"))
        {
            ownerDownload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            using var response = await client.SendAsync(ownerDownload);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Require(bytes.SequenceEqual(cabinetPackage), "The owner downloads the cabinet file back byte-for-byte");
        }

        using (var anonymousDownload = await client.GetAsync($"/api/account/files/{cabinetIds[1]}/download"))
            Require(anonymousDownload.StatusCode == HttpStatusCode.Unauthorized, "Cabinet download asks for a login");

        using (var foreignDownload = new HttpRequestMessage(HttpMethod.Get, $"/api/account/files/{cabinetIds[1]}/download"))
        {
            foreignDownload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(foreignDownload);
            Require(response.StatusCode == HttpStatusCode.NotFound, "Another account cannot download someone else's cabinet file");
        }

        using (var deleteFile = new HttpRequestMessage(HttpMethod.Delete, $"/api/account/files/{cabinetIds[0]}"))
        {
            deleteFile.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            using var response = await client.SendAsync(deleteFile);
            Require(response.StatusCode == HttpStatusCode.NoContent, "Teacher deletes a cabinet file");
        }

        using (var nonAdminKey = new HttpRequestMessage(HttpMethod.Post, "/api/admin/keys"))
        {
            nonAdminKey.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            nonAdminKey.Content = JsonContent.Create(new { kind = "storage", storagePlan = "pro", durationDays = 30 });
            using var response = await client.SendAsync(nonAdminKey);
            Require(response.StatusCode == HttpStatusCode.Forbidden, "Only the administrator creates activation keys");
        }

        var storageKey = await CreateKeyAsync(client, accessToken, new { kind = "storage", storagePlan = "pro", durationDays = 30 });
        Require(storageKey.Code.StartsWith("PVZ-", StringComparison.Ordinal) && storageKey.Code.Length == 18,
            "Activation keys look like PVZ-XXXX-XXXX-XXXX");
        var storageActivation = await ActivateKeyAsync(client, accountToken, storageKey.Code);
        Require(storageActivation.Message.Contains("Про", StringComparison.Ordinal), "Activation confirms the storage plan");
        Require(storageActivation.Profile.Plan == StorageLimits.Pro &&
            storageActivation.Profile.Storage.Any(bucket => bucket.Retention == RetentionPeriod.Hours48 && bucket.Limit == 20) &&
            storageActivation.Profile.Storage.Any(bucket => bucket.Retention == RetentionPeriod.Week1 && bucket.Limit == 15) &&
            storageActivation.Profile.Storage.Any(bucket => bucket.Retention == RetentionPeriod.Month1 && bucket.Limit == 5) &&
            storageActivation.Profile.Storage.Any(bucket => bucket.Retention == RetentionPeriod.Year1 && bucket.Limit == 1),
            "Pro plan stores twenty 48-hour taskbooks and unlocks week, month and year buckets");

        using (var reuseKey = new HttpRequestMessage(HttpMethod.Post, "/api/account/keys"))
        {
            reuseKey.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            reuseKey.Content = JsonContent.Create(new { code = storageKey.Code });
            using var response = await client.SendAsync(reuseKey);
            Require(response.StatusCode == HttpStatusCode.BadRequest, "An activated key cannot be reused");
        }

        var subjectKey = await CreateKeyAsync(client, accessToken, new { kind = "subject", durationDays = 30, subject = "math" });
        var subjectActivation = await ActivateKeyAsync(client, accountToken, subjectKey.Code);
        Require(subjectActivation.Profile.Subscriptions.Any(subscription =>
                subscription.Kind == "subject" && subscription.Subject == TaskbookCatalogSubjects.Math && subscription.ClassNumber is null),
            "Subject key subscribes the account to every class of the subject");

        using (var mathByAccount = new HttpRequestMessage(HttpMethod.Get, $"/api/catalog/{TaskbookCatalogSubjects.Math}/9/{catalogFileName}"))
        {
            mathByAccount.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            using var response = await client.SendAsync(mathByAccount);
            response.EnsureSuccessStatusCode();
            var package = await response.Content.ReadAsByteArrayAsync();
            Require(package.Length == catalogPackage.Length, "Subject subscription unlocks the catalog download");
        }

        var classKey = await CreateKeyAsync(client, accessToken, new { kind = "class", durationDays = 30, subject = "english", classNumber = 5 });
        await ActivateKeyAsync(client, accountToken, classKey.Code);
        using (var englishByAccount = new HttpRequestMessage(HttpMethod.Get, $"/api/catalog/{TaskbookCatalogSubjects.English}/5/{englishFileName}"))
        {
            englishByAccount.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            using var response = await client.SendAsync(englishByAccount);
            response.EnsureSuccessStatusCode();
        }

        using (var otherClass = new HttpRequestMessage(HttpMethod.Get, $"/api/catalog/{TaskbookCatalogSubjects.English}/3/other.pvz"))
        {
            otherClass.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            using var response = await client.SendAsync(otherClass);
            Require(response.StatusCode == HttpStatusCode.Forbidden, "A class key does not unlock other classes");
        }

        var allKey = await CreateKeyAsync(client, accessToken, new { kind = "all", durationDays = 30 });
        Require(allKey.Kind == "all", "All-subjects keys carry the new subscription kind");
        var allActivation = await ActivateKeyAsync(client, accountToken, allKey.Code);
        Require(allActivation.Profile.Subscriptions.Any(subscription => subscription.Kind == "all"),
            "All-subjects key subscribes the account to every subject and class");

        using (var teachersAsTeacher = new HttpRequestMessage(HttpMethod.Get, "/api/admin/teachers"))
        {
            teachersAsTeacher.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            using var response = await client.SendAsync(teachersAsTeacher);
            Require(response.StatusCode == HttpStatusCode.Forbidden, "Only the administrator lists all teachers");
        }

        using (var teachersAsAdmin = new HttpRequestMessage(HttpMethod.Get, "/api/admin/teachers"))
        {
            teachersAsAdmin.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(teachersAsAdmin);
            response.EnsureSuccessStatusCode();
            var teachers = await response.Content.ReadFromJsonAsync<AdminTeacherDto[]>();
            var maria = teachers!.Single(teacher => teacher.Username == "maria.petrova");
            Require(maria.Email == "maria@school.by" && maria.School == "Лицей №2, Минск",
                "Admin board shows every teacher's profile");
            Require(maria.Plan == StorageLimits.Pro &&
                maria.PlanDisplayName == "Про" &&
                maria.PlanUntil is not null,
                "Admin board shows the teacher's storage plan and its end date");
            Require(maria.Sessions.Count == 4 &&
                maria.Sessions.All(session => session.Retention == RetentionPeriod.Hours48),
                "Admin board lists the teacher's active sessions with retention");
        }

        using (var buildsAsAdmin = new HttpRequestMessage(HttpMethod.Get, "/api/admin/downloads"))
        {
            buildsAsAdmin.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(buildsAsAdmin);
            response.EnsureSuccessStatusCode();
            var items = await response.Content.ReadFromJsonAsync<DownloadInfoDto[]>();
            Require(items is { Length: 3 } &&
                items.All(item => item.Available) &&
                items.Any(item => item.Id == DownloadCatalog.ServerId && item.Role == "server"),
                "Admin build list includes the PVZS server build");
        }

        using (var serverDownload = new HttpRequestMessage(HttpMethod.Get, "/api/admin/downloads/pvzs"))
        {
            serverDownload.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(serverDownload);
            response.EnsureSuccessStatusCode();
            Require((await response.Content.ReadAsByteArrayAsync()).SequenceEqual(serverBuild),
                "Admin downloads the PVZS server build byte-for-byte");
        }

        using (var serverDownloadAsTeacher = new HttpRequestMessage(HttpMethod.Get, "/api/admin/downloads/pvzs"))
        {
            serverDownloadAsTeacher.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
            using var response = await client.SendAsync(serverDownloadAsTeacher);
            Require(response.StatusCode == HttpStatusCode.Forbidden, "Only the administrator downloads the server build");
        }

        using (var serverInfo = new HttpRequestMessage(HttpMethod.Get, "/api/admin/server"))
        {
            serverInfo.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(serverInfo);
            response.EnsureSuccessStatusCode();
            var info = await response.Content.ReadFromJsonAsync<ServerInfoDto>();
            Require(info is { Version.Length: > 0 } &&
                info.CatalogCollections == PVZ.Catalog.CatalogSeeder.TotalCollections &&
                !string.IsNullOrWhiteSpace(info.DownloadsDirectory) &&
                !string.IsNullOrWhiteSpace(info.ProcessPath),
                "Admin sees server version, paths and the seeded catalog size");
        }

        using (var serverInfoAnon = new HttpRequestMessage(HttpMethod.Get, "/api/admin/server"))
        using (var response = await client.SendAsync(serverInfoAnon))
        {
            Require(response.StatusCode == HttpStatusCode.Unauthorized, "Server info needs the administrator token");
        }

        using (var execAnon = new HttpRequestMessage(HttpMethod.Post, "/api/admin/exec"))
        using (var response = await client.SendAsync(execAnon))
        {
            Require(response.StatusCode == HttpStatusCode.Unauthorized, "Running an exe needs the administrator token");
        }

        using (var execMissing = new HttpRequestMessage(HttpMethod.Post, "/api/admin/exec-path"))
        {
            execMissing.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            execMissing.Content = JsonContent.Create(new RunPathRequest(@"C:\pvz-does-not-exist\missing.exe", null));
            using var response = await client.SendAsync(execMissing);
            Require(response.StatusCode == HttpStatusCode.BadRequest, "Running a missing file is rejected");
        }

        using (var updateBad = new HttpRequestMessage(HttpMethod.Post, "/api/admin/update"))
        {
            updateBad.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            updateBad.Content = JsonContent.Create(new UpdateRequest("ftp://example.com/x.exe", null, null, false));
            using var response = await client.SendAsync(updateBad);
            Require(response.StatusCode == HttpStatusCode.BadRequest, "Update requires an http(s) url");
        }

        if (OperatingSystem.IsWindows())
        {
            var cmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            using var execReal = new HttpRequestMessage(HttpMethod.Post, "/api/admin/exec-path")
            {
                Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
                Content = JsonContent.Create(new RunPathRequest(cmd, "/c echo pvz-exec-ok")),
            };
            using var response = await client.SendAsync(execReal);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ExecResultDto>();
            Require(result is { ExitCode: 0 } && result.Output.Contains("pvz-exec-ok", StringComparison.Ordinal),
                "Admin runs an exe on the server and sees its output");
        }

        var seededDirectory = Path.Combine(Path.GetTempPath(), $"pvz-seed-test-{Guid.NewGuid():N}");
        try
        {
            var seeded = PVZ.Catalog.CatalogSeeder.Ensure(seededDirectory, TaskbookStore.DefaultAccessPassword);
            Require(seeded == PVZ.Catalog.CatalogSeeder.TotalCollections &&
                Directory.EnumerateFiles(Path.Combine(seededDirectory, TaskbookCatalogSubjects.English, "5"), "*.pvz").Any() &&
                Directory.EnumerateFiles(Path.Combine(seededDirectory, TaskbookCatalogSubjects.Math, "9"), "*.pvz").Any(),
                "Catalog seeder lays out every subject and class without overwriting existing files");
            var again = PVZ.Catalog.CatalogSeeder.Ensure(seededDirectory, TaskbookStore.DefaultAccessPassword);
            Require(again == 0, "Catalog seeder is idempotent and keeps existing taskbooks");
        }
        finally
        {
            if (Directory.Exists(seededDirectory))
                Directory.Delete(seededDirectory, recursive: true);
        }

        var upload = await TaskbookOnlineClient.UploadAsync(packagePath, serverUrl, accessToken);
        Require(upload.Token.Length == 6, "Student token has six characters");
        Require(upload.Token.All(character => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Contains(character)), "Token uses unambiguous alphabet");
        Require(upload.Id.Length == 32 && upload.Id.All(char.IsAsciiHexDigit), "Session id is a 32-character hex folder name");
        Require(upload.ExpiresAt > DateTimeOffset.UtcNow + TimeSpan.FromDays(13.9),
            "Session is stored for two weeks");

        var sessions = await TaskbookOnlineClient.ListTeacherSessionsAsync(accessToken, serverUrl);
        var legacySession = sessions.Single(session => session.Id == upload.Id);
        Require(legacySession.TokenHint.StartsWith(upload.Token[..2], StringComparison.Ordinal) && legacySession.TokenHint.EndsWith("****", StringComparison.Ordinal),
            "Session list shows only a masked token hint");
        Require(legacySession.TotalTasks == 1 && legacySession.MaximumPoints == 30 && legacySession.Students.Count == 0,
            "Session summary reports task and score totals");
        Require(sessions.Count(session => session.OwnerId is null) == 1 &&
            sessions.Where(session => session.OwnerId is not null).All(session => session.OwnerName == "Мария Петрова-Сидорова"),
            "Admin sees every teacher's taskbooks with the owner name");

        var ownSessions = await TaskbookOnlineClient.ListTeacherSessionsAsync(accountToken, serverUrl);
        Require(ownSessions.Count == 4 &&
            ownSessions.All(session => session.Id != upload.Id) &&
            ownSessions.All(session => session.OwnerName == "Мария Петрова-Сидорова"),
            "A teacher sees only their own taskbooks");

        var session = await TaskbookOnlineClient.StartStudentSessionAsync(
            upload.Token,
            new OnlineStudentInfo("Анна", "Иванова", "7А"),
            serverUrl);
        var downloaded = await TaskbookOnlineClient.DownloadAsync(upload.Token, serverUrl);
        Require(downloaded.Tasks[0].Description == document.Tasks[0].Description, "Student downloads and validates the PVZ package");

        await TaskbookOnlineClient.ReportProgressAsync(
            new OnlineProgressUpdate(session.SessionId, [new StudentTaskProgressDto(30, true)]),
            serverUrl);

        sessions = await TaskbookOnlineClient.ListTeacherSessionsAsync(accessToken, serverUrl);
        var anna = sessions.Single(session => session.Id == upload.Id).Students.Single();
        Require(anna.FirstName == "Анна" && anna.LastName == "Иванова" && anna.ClassName == "7А",
            "Student identity is recorded");
        Require(anna.EarnedPoints == 30 && anna.MaximumPoints == 30 &&
            anna.CompletedTasks == 1 && anna.TotalTasks == 1 && anna.IsOnline,
            "Live student score and progress are recorded");
        Require(anna.Tasks.Single() == new StudentTaskProgressDto(30, true),
            "Per-task progress is exposed to the teacher");

        // ---- Пересылка файлов: материал от учителя и работа ученика ----
        var materialPath = Path.Combine(Path.GetTempPath(), $"pvz-material-{Guid.NewGuid():N}.txt");
        var workPath = Path.Combine(Path.GetTempPath(), $"pvz-work-{Guid.NewGuid():N}.py");
        try
        {
            const string materialText = "Инструкция для класса";
            const string workText = "print('hello')";
            await File.WriteAllTextAsync(materialPath, materialText);
            var material = await TaskbookOnlineClient.UploadSessionFileAsync(accessToken, upload.Id, materialPath, serverUrl);
            Require(material.Kind == ExchangeFileKinds.Material &&
                material.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                material.SizeBytes == Encoding.UTF8.GetByteCount(materialText),
                "Teacher uploads a material file with its name and size");

            var studentFiles = await TaskbookOnlineClient.ListStudentFilesAsync(session.SessionId, serverUrl);
            Require(studentFiles.Materials.Count == 1 && studentFiles.Works.Count == 0,
                "Student sees the material and no works yet");
            var materialBytes = await TaskbookOnlineClient.DownloadStudentFileAsync(session.SessionId, material.Id, serverUrl);
            Require(Encoding.UTF8.GetString(materialBytes) == materialText,
                "Student downloads the material content");

            await File.WriteAllTextAsync(workPath, workText);
            var work = await TaskbookOnlineClient.UploadStudentWorkAsync(session.SessionId, workPath, serverUrl);
            Require(work.Kind == ExchangeFileKinds.Work && work.StudentName is not null &&
                work.StudentName.Contains("Анна", StringComparison.Ordinal),
                "Student upload records who sent the work");

            var teacherFiles = await TaskbookOnlineClient.ListSessionFilesAsync(accessToken, upload.Id, serverUrl);
            Require(teacherFiles.Count == 2 &&
                teacherFiles.Any(file => file.Kind == ExchangeFileKinds.Work && file.Id == work.Id),
                "Teacher sees both the material and the student work");
            var teacherWorkBytes = await TaskbookOnlineClient.DownloadSessionFileAsync(accessToken, upload.Id, work.Id, serverUrl);
            Require(Encoding.UTF8.GetString(teacherWorkBytes) == workText,
                "Teacher downloads the student work content");

            var secondStudent = await TaskbookOnlineClient.StartStudentSessionAsync(
                upload.Token,
                new OnlineStudentInfo("Пётр", "Сидоров", "7А"),
                serverUrl);
            var secondFiles = await TaskbookOnlineClient.ListStudentFilesAsync(secondStudent.SessionId, serverUrl);
            Require(secondFiles.Materials.Count == 1 && secondFiles.Works.Count == 0,
                "Another student sees materials but never foreign works");
            using (var foreignWork = await client.GetAsync(
                $"/api/student/sessions/{secondStudent.SessionId}/files/{work.Id}"))
                Require(foreignWork.StatusCode == HttpStatusCode.NotFound,
                    "A student cannot download another student's work");

            await TaskbookOnlineClient.DeleteSessionFileAsync(accessToken, upload.Id, material.Id, serverUrl);
            studentFiles = await TaskbookOnlineClient.ListStudentFilesAsync(session.SessionId, serverUrl);
            Require(studentFiles.Materials.Count == 0 && studentFiles.Works.Count == 1,
                "Deleted material disappears for students while works remain");

            using (var anonymousTeacherFiles = await client.GetAsync($"/api/teacher/sessions/{upload.Id}/files"))
                Require(anonymousTeacherFiles.StatusCode == HttpStatusCode.Unauthorized,
                    "Teacher file list requires a bearer token");
            using (var unknownStudentFiles = await client.GetAsync("/api/student/sessions/missing/files"))
                Require(unknownStudentFiles.StatusCode == HttpStatusCode.NotFound,
                    "Unknown student session gets 404 on the file list");
        }
        finally
        {
            if (File.Exists(materialPath))
                File.Delete(materialPath);
            if (File.Exists(workPath))
                File.Delete(workPath);
        }

        var extended = await TaskbookOnlineClient.ExtendSessionAsync(accessToken, upload.Id, serverUrl);
        Require(extended > DateTimeOffset.UtcNow + TimeSpan.FromDays(13.9), "Extend pushes expiry another two weeks");

        var sessionPackage = await TaskbookOnlineClient.DownloadSessionAsync(accessToken, upload.Id, serverUrl);
        var sessionDownloadPath = Path.Combine(Path.GetTempPath(), $"pvz-session-test-{Guid.NewGuid():N}.pvz");
        try
        {
            await File.WriteAllBytesAsync(sessionDownloadPath, sessionPackage);
            Require(TaskbookPackageIO.Load(sessionDownloadPath).Tasks[0].Description == document.Tasks[0].Description,
                "Teacher downloads the stored session package");
        }
        finally
        {
            if (File.Exists(sessionDownloadPath))
                File.Delete(sessionDownloadPath);
        }

        server.Kill(entireProcessTree: true);
        await server.WaitForExitAsync();
        server.Dispose();
        server = StartServer(serverAssembly, serverUrl, dataDirectory, catalogDirectory, downloadsDirectory);
        await WaitForServerAsync(client, server);
        var persisted = await TaskbookOnlineClient.DownloadAsync(upload.Token, serverUrl);
        Require(persisted.Tasks[0].Description == document.Tasks[0].Description, "Taskbook persists across server restart");

        var persistedSessions = await TaskbookOnlineClient.ListTeacherSessionsAsync(accessToken, serverUrl);
        var persistedUpload = persistedSessions.Single(session => session.Id == upload.Id);
        Require(persistedUpload.Students.Count == 2, "Both student sessions survive a server restart");
        var persistedAnna = persistedUpload.Students.Single(student =>
            student.FirstName == "Анна" && student.LastName == "Иванова");
        Require(persistedAnna.EarnedPoints == 30 && persistedAnna.Tasks[0].Completed,
            "Access token and student progress survive a server restart (SQLite-backed)");

        using var closeRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/sessions/{upload.Id}");
        closeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using (var close = await client.SendAsync(closeRequest))
            Require(close.StatusCode == HttpStatusCode.NoContent, "Teacher closes a session");
        using (var closed = await client.GetAsync($"/api/taskbooks/{Uri.EscapeDataString(upload.Token)}"))
            Require(closed.StatusCode == HttpStatusCode.NotFound, "Closed session token cannot download");
        using (var closedList = new HttpRequestMessage(HttpMethod.Get, "/api/teacher/sessions"))
        {
            closedList.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await client.SendAsync(closedList);
            response.EnsureSuccessStatusCode();
            var remaining = await response.Content.ReadFromJsonAsync<TeacherSessionDto[]>();
            Require(remaining is not null && remaining.All(session => session.Id != upload.Id),
                "Closed session disappears from the teacher list");
        }

        var paidUpload = await TaskbookOnlineClient.UploadAsync(
            packagePath,
            serverUrl,
            accessToken,
            StoragePlan.PaidOneMonth);
        Require(paidUpload.Retention == RetentionPeriod.Month1, "Upload receipt carries the month retention");
        Require(paidUpload.ExpiresAt > DateTimeOffset.UtcNow + TimeSpan.FromDays(29.9),
            "Paid month plan stores a real month");
        var paidSessions = await TaskbookOnlineClient.ListTeacherSessionsAsync(accessToken, serverUrl);
        Require(paidSessions.Single(session => session.Id == paidUpload.Id).Retention == RetentionPeriod.Month1,
            "Teacher list exposes the retention of the session");
        using (var closePaid = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/sessions/{paidUpload.Id}"))
        {
            closePaid.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var paidClose = await client.SendAsync(closePaid);
            Require(paidClose.StatusCode == HttpStatusCode.NoContent, "Paid month session can be closed");
        }

        var deadPortListener = new TcpListener(IPAddress.Loopback, 0);
        deadPortListener.Start();
        var deadPort = ((IPEndPoint)deadPortListener.LocalEndpoint).Port;
        deadPortListener.Stop();
        var deadServerUrl = $"http://127.0.0.1:{deadPort}";
        ServerUnreachableException? unreachable = null;
        try
        {
            await TaskbookOnlineClient.ListCatalogTaskbooksAsync(TaskbookCatalogSubjects.Math, 9, deadServerUrl);
        }
        catch (ServerUnreachableException exception)
        {
            unreachable = exception;
        }
        Require(unreachable is not null &&
            unreachable.Message.Contains("Не удалось подключиться к серверу «127.0.0.1»", StringComparison.Ordinal) &&
            unreachable.Message.Contains("пропал интернет", StringComparison.Ordinal),
            "A dead server raises a friendly Russian ServerUnreachableException with the host name");
    }
    finally
    {
        if (File.Exists(packagePath))
            File.Delete(packagePath);
        if (File.Exists(fillPackagePath))
            File.Delete(fillPackagePath);
    }

    var expiryStore = new TaskbookStore(expiryDirectory, TimeSpan.FromSeconds(1));
    Require(await expiryStore.TryLoginAsync("wrong-password") is null, "Store rejects wrong access password");
    var expiryLogin = await expiryStore.TryLoginAsync(TaskbookStore.DefaultAccessPassword);
    Require(expiryLogin is not null && expiryLogin.AccessToken.Length == 43, "Store login issues an access token");
    var expiryAccessToken = expiryLogin!.AccessToken;
    var expiryPackage = Path.Combine(Path.GetTempPath(), $"pvz-expiry-package-{Guid.NewGuid():N}.pvz");
    try
    {
        TaskbookPackageIO.Save(expiryPackage, document);
        var expiryUpload = await expiryStore.UploadAsync(
            expiryAccessToken,
            await File.ReadAllBytesAsync(expiryPackage),
            StoragePlan.PaidOneMonth);
        Require(expiryUpload.Token.Length == 6, "Store token length for expiry lifecycle");
        Require(expiryUpload.Retention == RetentionPeriod.Month1, "Store records the requested retention");
        Require(Directory.Exists(Path.Combine(expiryDirectory, expiryUpload.Id)), "Session folder is created under the sessions root");
        var extended = await expiryStore.ExtendSessionAsync(expiryAccessToken, expiryUpload.Id);
        Require(extended is not null, "Store extends an active session");
        await Task.Delay(TimeSpan.FromMilliseconds(1400));
        Require(await expiryStore.DownloadAsync(expiryUpload.Token) is null, "Expired token is rejected");
        Require(await expiryStore.DownloadSessionAsync(expiryAccessToken, expiryUpload.Id) is null, "Expired session cannot be downloaded by the teacher");
        Require(await expiryStore.ExtendSessionAsync(expiryAccessToken, expiryUpload.Id) is null, "Expired session cannot be extended");
        var expiredList = await expiryStore.ListSessionsAsync(expiryAccessToken);
        Require(expiredList.Count == 0, "Expired sessions disappear from the listing");
        Require(Directory.EnumerateDirectories(expiryDirectory).Any() is false, "Expired session folder is deleted from disk");
    }
    finally
    {
        if (File.Exists(expiryPackage))
            File.Delete(expiryPackage);
    }

    Console.WriteLine("PASS: landing/pricing/login/register/cabinet pages with reveal animations and RU/EN + BYN/USD/RUB switchers, account-name registration and login by name or email, teacher dashboard with the activation key generator and the all-teachers board, password-only server login, two-week session folders with extend/close/download, retention recorded and listed, catalog listing and subscription-gated downloads, public client downloads and admin-only server download, server control (run exe/update) and auto-seeded catalog, SQLite-backed accounts with registration/login/profile edits, cabinet quotas with owner-only downloads and file deletion, activation keys for storage/subject/class/all-subjects, admin and teacher session visibility with owner names, package validation, six-character tokens, student identity/live progress, teacher↔student file exchange (material distribution, work uploads, foreign-work protection, deletion), persistence across restarts, session expiry deletion, fill-blank underscore rules, fill-task package round-trip and English catalog download, friendly unreachable-server errors");
}
finally
{
    if (server is { HasExited: false })
    {
        server.Kill(entireProcessTree: true);
        await server.WaitForExitAsync();
    }
    server?.Dispose();
    if (Directory.Exists(dataDirectory))
        Directory.Delete(dataDirectory, recursive: true);
    if (Directory.Exists(expiryDirectory))
        Directory.Delete(expiryDirectory, recursive: true);
}

static Process StartServer(string assembly, string url, string dataDirectory, string catalogDirectory, string downloadsDirectory)
{
    var start = new ProcessStartInfo("dotnet", $"\"{assembly}\"")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };
    start.Environment["ASPNETCORE_URLS"] = $"http://0.0.0.0:{new Uri(url).Port}";
    start.Environment["PVZ_SERVER_DATA_DIR"] = dataDirectory;
    start.Environment["PVZ_TASKBOOK_CATALOG_DIR"] = catalogDirectory;
    start.Environment["PVZ_DOWNLOAD_DIR"] = downloadsDirectory;
    // Тесты сами раскладывают каталог, поэтому автозаполнение выключено.
    start.Environment["PVZ_AUTO_SEED_CATALOG"] = "0";
    var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start PVZS.");
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}

static async Task WaitForServerAsync(HttpClient client, Process process)
{
    for (var attempt = 0; attempt < 80; attempt++)
    {
        if (process.HasExited)
            throw new InvalidOperationException($"PVZS exited during startup with code {process.ExitCode}.");
        try
        {
            using var response = await client.GetAsync("/health");
            if (response.IsSuccessStatusCode)
                return;
        }
        catch (HttpRequestException)
        {
        }
        await Task.Delay(100);
    }

    throw new TimeoutException("PVZS did not become ready.");
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PVZ.sln")))
        directory = directory.Parent;
    return directory?.FullName ?? throw new DirectoryNotFoundException("Run the server integration test from within the repository.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static bool ThrowsAddressRequired(Func<string> action)
{
    try
    {
        _ = action();
        return false;
    }
    catch (ServerAddressRequiredException)
    {
        return true;
    }
}

static MultipartFormDataContent BuildCabinetUpload(byte[] package, string retention)
{
    var form = new MultipartFormDataContent();
    var file = new ByteArrayContent(package);
    file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    form.Add(file, "file", "taskbook.pvz");
    form.Add(new StringContent(retention), "retention");
    return form;
}

static async Task<ActivationKeyDto> CreateKeyAsync(HttpClient client, string adminToken, object request)
{
    using var message = new HttpRequestMessage(HttpMethod.Post, "/api/admin/keys")
    {
        Content = JsonContent.Create(request),
    };
    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
    using var response = await client.SendAsync(message);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<ActivationKeyDto>())!;
}

static async Task<TestActivationResponse> ActivateKeyAsync(HttpClient client, string accountToken, string code)
{
    using var message = new HttpRequestMessage(HttpMethod.Post, "/api/account/keys")
    {
        Content = JsonContent.Create(new { code }),
    };
    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accountToken);
    using var response = await client.SendAsync(message);
    response.EnsureSuccessStatusCode();
    return (await response.Content.ReadFromJsonAsync<TestActivationResponse>())!;
}

internal sealed record TestAccountAuthResponse(string AccessToken, DateTimeOffset ExpiresAt, AccountProfileDto Profile);

internal sealed record TestActivationResponse(string Message, AccountProfileDto Profile);
