using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Hosting;
using PVZ.Core;
using PVZS;

var builder = WebApplication.CreateBuilder(args);
// Интеграция со службой Windows: при запуске через sc.exe сервер регистрируется в SCM.
builder.Host.UseWindowsService(options => options.ServiceName = "PVZS");
// Данные лежат рядом с exe: sessions/ (задачники и pvz.db) и zadachniki/ (каталог).
var dataDirectory = Environment.GetEnvironmentVariable("PVZ_SERVER_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDirectory))
    dataDirectory = Path.Combine(AppContext.BaseDirectory, "sessions");
var catalogDirectory = Environment.GetEnvironmentVariable("PVZ_TASKBOOK_CATALOG_DIR");
if (string.IsNullOrWhiteSpace(catalogDirectory))
    catalogDirectory = Path.Combine(AppContext.BaseDirectory, "zadachniki");
// Сборки клиентов и сервера для скачивания с сайта лежат в downloads рядышком с exe.
var downloadsDirectory = Environment.GetEnvironmentVariable("PVZ_DOWNLOAD_DIR");
if (string.IsNullOrWhiteSpace(downloadsDirectory))
    downloadsDirectory = Path.Combine(AppContext.BaseDirectory, "downloads");
var databasePath = Environment.GetEnvironmentVariable("PVZ_SERVER_DB_PATH");
if (string.IsNullOrWhiteSpace(databasePath))
    databasePath = Path.Combine(dataDirectory, "pvz.db");

// При запуске раскладываем стандартные задачники (английский и математика по классам),
// чтобы каталог сразу был наполнен. Отключается через PVZ_AUTO_SEED_CATALOG=0 (нужно тестам).
var autoSeed = Environment.GetEnvironmentVariable("PVZ_AUTO_SEED_CATALOG");
var autoSeedEnabled = !string.Equals(autoSeed, "0", StringComparison.Ordinal) &&
    !string.Equals(autoSeed, "false", StringComparison.OrdinalIgnoreCase);
if (autoSeedEnabled)
{
    try
    {
        var seeded = PVZ.Catalog.CatalogSeeder.Ensure(
            catalogDirectory,
            TaskbookStore.DefaultAccessPassword);
        if (seeded > 0)
            Console.WriteLine($"Каталог задачников наполнен: {seeded} сборников в {catalogDirectory}.");
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
    {
        Console.Error.WriteLine($"Не удалось наполнить каталог задачниками: {exception.Message}");
    }
}

builder.WebHost.UseUrls(
    Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://127.0.0.1:1729");
// Пересылка файлов допускает файлы до 32 МиБ, поэтому лимит тела запроса
// поднят выше стандартных 30 МБ (с запасом на multipart-обвязку).
builder.WebHost.ConfigureKestrel(serverOptions =>
    serverOptions.Limits.MaxRequestBodySize = 40 * 1024 * 1024);
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
var trustedProxies = Environment.GetEnvironmentVariable("PVZ_TRUSTED_PROXIES");
if (!string.IsNullOrWhiteSpace(trustedProxies))
{
    foreach (var address in trustedProxies.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
    {
        if (!System.Net.IPAddress.TryParse(address, out var proxyAddress))
            throw new InvalidOperationException("PVZ_TRUSTED_PROXIES must contain comma-separated IP addresses.");
        forwardedHeaders.KnownProxies.Add(proxyAddress);
    }
}
// Клиенты ходят напрямую по своему IP, но целый класс за школьным шлюзом
// делит один адрес, поэтому при желании бюджеты API можно поднять множителем.
var rateLimitMultiplier = 1;
var rateLimitMultiplierText = Environment.GetEnvironmentVariable("PVZ_RATE_LIMIT_MULTIPLIER");
if (!string.IsNullOrWhiteSpace(rateLimitMultiplierText))
{
    if (!int.TryParse(rateLimitMultiplierText, out rateLimitMultiplier) || rateLimitMultiplier < 1 || rateLimitMultiplier > 1000)
        throw new InvalidOperationException("PVZ_RATE_LIMIT_MULTIPLIER must be an integer between 1 and 1000.");
}
builder.Services.AddSingleton(new PvzDatabase(databasePath));
builder.Services.AddSingleton(services => new AccountService(services.GetRequiredService<PvzDatabase>()));
builder.Services.AddSingleton(new DownloadCatalog(downloadsDirectory));
builder.Services.AddSingleton<ServerControl>();
builder.Services.AddSingleton(services => new TaskbookStore(
    dataDirectory,
    sessionLifetime: null,
    catalogDirectory: catalogDirectory,
    accounts: services.GetRequiredService<AccountService>()));
builder.Services.AddHostedService<SessionCleanupService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("teacher-api", context => RateLimitPartition.GetFixedWindowLimiter(
        GetClientAddress(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 90 * rateLimitMultiplier,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    options.AddPolicy("student-api", context => RateLimitPartition.GetFixedWindowLimiter(
        GetClientAddress(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120 * rateLimitMultiplier,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    options.AddPolicy("account-api", context => RateLimitPartition.GetFixedWindowLimiter(
        GetClientAddress(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120 * rateLimitMultiplier,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    options.AddPolicy("teacher-login", context => RateLimitPartition.GetFixedWindowLimiter(
        GetClientAddress(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    options.AddPolicy("account-auth", context => RateLimitPartition.GetFixedWindowLimiter(
        GetClientAddress(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
});

var app = builder.Build();
app.UseForwardedHeaders(forwardedHeaders);
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (!context.Request.IsHttps &&
        context.Connection.RemoteIpAddress is { } remoteAddress &&
        !System.Net.IPAddress.IsLoopback(remoteAddress))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "HTTPS is required for non-local connections." });
        return;
    }

    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/", (DownloadCatalog downloads) => Results.Content(LandingPage.Render(downloads.ListPublic()), "text/html; charset=utf-8"));
app.MapGet("/pricing", () => Results.Content(PricingPage.Render(), "text/html; charset=utf-8"));
app.MapGet("/admin", () => Results.Content(AdminPage.Render(), "text/html; charset=utf-8"));
app.MapGet("/login", () => Results.Content(LoginPage.Render(), "text/html; charset=utf-8"));
app.MapGet("/register", () => Results.Content(RegisterPage.Render(), "text/html; charset=utf-8"));
app.MapGet("/cabinet", () => Results.Content(CabinetPage.Render(), "text/html; charset=utf-8"));

// ---- Скачивание приложений PVZ: клиенты открыты всем, сервер — только администратору ----

app.MapGet("/api/downloads", (DownloadCatalog downloads) => Results.Ok(downloads.ListPublic()));

app.MapGet("/downloads/{id}", (string id, DownloadCatalog downloads) =>
{
    var item = downloads.FindPublic(id);
    if (item is null)
        return Results.NotFound();
    var stream = downloads.TryOpen(item.Id);
    return stream is null
        ? Results.NotFound()
        : Results.File(stream, "application/octet-stream", item.FileName, enableRangeProcessing: true);
});

app.MapGet("/api/admin/downloads", (
    HttpRequest httpRequest,
    TaskbookStore store,
    DownloadCatalog downloads) =>
{
    if (EnsureAdmin(httpRequest, store, out _, out _) is { } deny)
        return deny;
    return Results.Ok(downloads.ListAll());
}).RequireRateLimiting("teacher-api");

app.MapGet("/api/admin/downloads/{id}", (
    string id,
    HttpRequest httpRequest,
    TaskbookStore store,
    DownloadCatalog downloads) =>
{
    if (EnsureAdmin(httpRequest, store, out _, out _) is { } deny)
        return deny;
    var item = downloads.Find(id);
    if (item is null)
        return Results.NotFound();
    var stream = downloads.TryOpen(item.Id);
    return stream is null
        ? Results.NotFound()
        : Results.File(stream, "application/octet-stream", item.FileName, enableRangeProcessing: true);
}).RequireRateLimiting("teacher-api");

app.MapGet("/api/admin/server", (
    HttpRequest httpRequest,
    TaskbookStore store,
    ServerControl server,
    DownloadCatalog downloads) =>
{
    if (EnsureAdmin(httpRequest, store, out _, out _) is { } deny)
        return deny;
    return Results.Ok(new ServerInfoDto(
        server.Version,
        server.ProcessPath,
        dataDirectory,
        catalogDirectory,
        downloads.DirectoryPath,
        PVZ.Catalog.CatalogSeeder.TotalCollections));
}).RequireRateLimiting("teacher-api");

// Запуск произвольного .exe, загруженного в форму (поле file), с необязательными аргументами (поле args).
app.MapPost("/api/admin/exec", async (
    HttpRequest httpRequest,
    TaskbookStore store,
    ServerControl server,
    CancellationToken cancellationToken) =>
{
    if (EnsureAdmin(httpRequest, store, out _, out _) is { } deny)
        return deny;
    if (!httpRequest.HasFormContentType)
        return Results.BadRequest(new { error = "Прикрепите .exe файл." });

    var sizeFeature = httpRequest.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (sizeFeature is { IsReadOnly: false })
        sizeFeature.MaxRequestBodySize = DownloadCatalog.MaximumControlUploadBytes;

    IFormCollection form;
    try
    {
        form = await httpRequest.ReadFormAsync(cancellationToken);
    }
    catch (InvalidDataException)
    {
        return Results.BadRequest(new { error = "Не удалось прочитать файл." });
    }

    var file = form.Files.Count == 1 ? form.Files[0] : null;
    if (file is null || file.Length <= 0)
        return Results.BadRequest(new { error = "Прикрепите один непустой файл." });
    if (file.Length > DownloadCatalog.MaximumControlUploadBytes)
        return Results.BadRequest(new { error = "Файл слишком большой." });

    byte[] content;
    await using (var stream = file.OpenReadStream())
    using (var buffer = new MemoryStream())
    {
        await stream.CopyToAsync(buffer, cancellationToken);
        content = buffer.ToArray();
    }

    var (path, error) = server.SaveUpload(file.FileName, content);
    if (error is not null)
        return Results.BadRequest(new { error });
    var result = await server.RunAsync(path, form["args"].ToString(), cancellationToken);
    return Results.Ok(result);
}).RequireRateLimiting("teacher-api");

// Запуск уже лежащего на диске файла по пути (например, downloads\PVZS.exe).
app.MapPost("/api/admin/exec-path", async (
    RunPathRequest? request,
    HttpRequest httpRequest,
    TaskbookStore store,
    ServerControl server,
    CancellationToken cancellationToken) =>
{
    if (EnsureAdmin(httpRequest, store, out _, out _) is { } deny)
        return deny;
    if (request is null || string.IsNullOrWhiteSpace(request.Path))
        return Results.BadRequest(new { error = "Укажите путь к .exe (например, downloads\\PVZS.exe)." });

    var path = Path.IsPathRooted(request.Path)
        ? request.Path
        : Path.Combine(AppContext.BaseDirectory, request.Path);
    if (!File.Exists(path))
        return Results.BadRequest(new { error = $"Файл не найден: {path}" });
    return Results.Ok(await server.RunAsync(path, request.Arguments, cancellationToken));
}).RequireRateLimiting("teacher-api");

// Обновление с сайта: скачивает файл по URL (с проверкой SHA-256) и при желании запускает его.
app.MapPost("/api/admin/update", async (
    UpdateRequest? request,
    HttpRequest httpRequest,
    TaskbookStore store,
    ServerControl server,
    CancellationToken cancellationToken) =>
{
    if (EnsureAdmin(httpRequest, store, out _, out _) is { } deny)
        return deny;
    if (request is null || string.IsNullOrWhiteSpace(request.Url))
        return Results.BadRequest(new { error = "Укажите URL файла для обновления." });

    var (path, error) = await server.DownloadAsync(request.Url, request.Sha256, cancellationToken);
    if (error is not null)
        return Results.BadRequest(new { error });
    ExecResultDto? run = request.Run
        ? await server.RunAsync(path, request.Arguments, cancellationToken)
        : null;
    return Results.Ok(new { path, run });
}).RequireRateLimiting("teacher-api");

app.MapGet("/api/catalog/{subject}/{classNumber:int}", (
    string subject,
    int classNumber,
    TaskbookStore store) =>
{
    try
    {
        return Results.Ok(store.ListCatalogTaskbooks(subject, classNumber));
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});
app.MapGet("/api/catalog/{subject}/{classNumber:int}/{fileName}", (
    string subject,
    int classNumber,
    string fileName,
    HttpRequest httpRequest,
    TaskbookStore store,
    AccountService accountsService) =>
{
    var principal = GetPrincipal(httpRequest, store);
    if (principal is null)
        return Results.Json(
            new { error = "Войдите в личный кабинет, чтобы скачивать задачники из каталога." },
            statusCode: StatusCodes.Status401Unauthorized);
    try
    {
        if (!accountsService.HasCatalogAccess(principal, subject, classNumber))
            return Results.Json(
                new { error = "Нужна подписка на предмет, класс или весь каталог. Активируйте ключ в личном кабинете." },
                statusCode: StatusCodes.Status403Forbidden);
        var package = store.DownloadCatalogTaskbook(subject, classNumber, fileName);
        return package is null
            ? Results.NotFound()
            : Results.File(package, "application/octet-stream", fileName);
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("teacher-api");

app.MapPost("/api/teacher/login", async (
    LoginRequest? request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    if (request is null)
        return Results.BadRequest(new { error = "Введите пароль доступа." });
    var receipt = await store.TryLoginAsync(request.Password, cancellationToken);
    return receipt is null
        ? Results.Unauthorized()
        : Results.Ok(receipt);
}).RequireRateLimiting("teacher-login");

app.MapPost("/api/teacher/sessions", async (
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    if (!string.Equals(request.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
    if (request.ContentLength is > TaskbookStore.MaximumPackageBytes)
        return Results.BadRequest(new { error = "Размер .pvz превышает 16 МиБ." });

    var plan = ParsePlan(request.Query["plan"].ToString());
    try
    {
        var package = await ReadBoundedAsync(request.Body, cancellationToken);
        // Вход по старому паролю школы (kind=root) сохраняет обезличенную загрузку PVZT.
        var uploaded = principal.Kind == "root" && TryGetAccessToken(request, out var accessToken)
            ? await store.UploadAsync(accessToken, package, plan, cancellationToken)
            : await store.UploadAsync(principal, package, plan, cancellationToken);
        return Results.Ok(uploaded);
    }
    catch (ServerUnauthorizedException)
    {
        return Results.Unauthorized();
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("teacher-api");

app.MapGet("/api/teacher/sessions", async (
    HttpRequest request,
    TaskbookStore store,
    AccountService accountsService,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    try
    {
        // Администратор видит все учительские задачники, учитель — только свои.
        if (principal.IsAdmin || principal.Kind == "root")
        {
            var sessions = await store.ListAllSessionsAsync(cancellationToken);
            var names = accountsService.ListUsers().ToDictionary(
                user => user.Id,
                user => string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName);
            return Results.Ok(sessions
                .Select(session => session with
                {
                    OwnerName = session.OwnerId is { } ownerId && names.TryGetValue(ownerId, out var name) ? name : null,
                })
                .ToArray());
        }

        var own = await store.ListOwnedSessionsAsync(principal.UserId, cancellationToken);
        var selfName = string.IsNullOrWhiteSpace(principal.FullName) ? principal.Username : principal.FullName;
        return Results.Ok(own.Select(session => session with { OwnerName = selfName }).ToArray());
    }
    catch (ServerUnauthorizedException)
    {
        return Results.Unauthorized();
    }
}).RequireRateLimiting("teacher-api");

app.MapPost("/api/teacher/sessions/{id}/extend", async (
    string id,
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    try
    {
        var expiresAt = principal.IsAdmin && TryGetAccessToken(request, out var accessToken)
            ? await store.ExtendSessionAsync(accessToken, id, cancellationToken)
            : await store.ExtendOwnedSessionAsync(principal.UserId, id, cancellationToken);
        return expiresAt is null
            ? Results.NotFound()
            : Results.Ok(new SessionExtendReceipt(expiresAt.Value));
    }
    catch (ServerUnauthorizedException)
    {
        return Results.Unauthorized();
    }
}).RequireRateLimiting("teacher-api");

app.MapDelete("/api/teacher/sessions/{id}", async (
    string id,
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    try
    {
        var closed = principal.IsAdmin && TryGetAccessToken(request, out var accessToken)
            ? await store.CloseSessionAsync(accessToken, id, cancellationToken)
            : await store.CloseOwnerSessionAsync(principal.UserId, id, cancellationToken);
        return closed ? Results.NoContent() : Results.NotFound();
    }
    catch (ServerUnauthorizedException)
    {
        return Results.Unauthorized();
    }
}).RequireRateLimiting("teacher-api");

app.MapGet("/api/teacher/sessions/{id}/download", async (
    string id,
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    try
    {
        var package = principal.IsAdmin && TryGetAccessToken(request, out var accessToken)
            ? await store.DownloadSessionAsync(accessToken, id, cancellationToken)
            : await store.DownloadOwnerFileAsync(principal.UserId, id, cancellationToken);
        return package is null
            ? Results.NotFound()
            : Results.File(package, "application/octet-stream", "taskbook.pvz");
    }
    catch (ServerUnauthorizedException)
    {
        return Results.Unauthorized();
    }
}).RequireRateLimiting("teacher-api");

app.MapPost("/api/account/register", async (
    RegisterRequest? request,
    TaskbookStore store,
    AccountService accountsService,
    CancellationToken cancellationToken) =>
{
    if (request is null)
        return Results.BadRequest(new { error = "Заполните форму регистрации." });
    try
    {
        var result = accountsService.Register(request.Username, request.FullName, request.School, request.Email, request.Password);
        var profile = await BuildProfileAsync(accountsService, store, result.Principal, cancellationToken);
        return Results.Ok(new AccountAuthResponse(result.Receipt.AccessToken, result.Receipt.ExpiresAt, profile));
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("account-auth");

app.MapPost("/api/account/login", async (
    AccountLoginRequest? request,
    TaskbookStore store,
    AccountService accountsService,
    CancellationToken cancellationToken) =>
{
    if (request is null)
        return Results.BadRequest(new { error = "Введите имя аккаунта (или email) и пароль." });
    var result = accountsService.Login(request.Identifier, request.Password);
    if (result is null)
        return Results.Json(new { error = "Неверное имя аккаунта или пароль." }, statusCode: StatusCodes.Status401Unauthorized);
    var profile = await BuildProfileAsync(accountsService, store, result.Principal, cancellationToken);
    return Results.Ok(new AccountAuthResponse(result.Receipt.AccessToken, result.Receipt.ExpiresAt, profile));
}).RequireRateLimiting("account-auth");

app.MapPost("/api/account/logout", (
    HttpRequest request,
    TaskbookStore store,
    AccountService accountsService) =>
{
    if (!TryGetAccessToken(request, out var accessToken) || store.AuthenticateAccount(accessToken) is null)
        return Results.Unauthorized();
    accountsService.Logout(accessToken);
    return Results.NoContent();
}).RequireRateLimiting("account-api");

app.MapGet("/api/account/me", async (
    HttpRequest request,
    TaskbookStore store,
    AccountService accountsService,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    return Results.Ok(await BuildProfileAsync(accountsService, store, principal, cancellationToken));
}).RequireRateLimiting("account-api");

app.MapPut("/api/account/me", async (
    ProfileUpdateRequest? request,
    HttpRequest httpRequest,
    TaskbookStore store,
    AccountService accountsService,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(httpRequest, store);
    if (principal is null)
        return Results.Unauthorized();
    if (request is null)
        return Results.BadRequest(new { error = "Заполните ФИО и школу." });
    try
    {
        accountsService.UpdateProfile(principal, request.FullName, request.School);
        return Results.Ok(await BuildProfileAsync(accountsService, store, principal, cancellationToken));
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("account-api");

app.MapPut("/api/account/me/password", (
    PasswordChangeRequest? request,
    HttpRequest httpRequest,
    TaskbookStore store,
    AccountService accountsService) =>
{
    var principal = GetPrincipal(httpRequest, store);
    if (principal is null)
        return Results.Unauthorized();
    if (request is null)
        return Results.BadRequest(new { error = "Заполните оба поля пароля." });
    try
    {
        accountsService.ChangePassword(principal, request.CurrentPassword, request.NewPassword);
        return Results.NoContent();
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("account-auth");

app.MapPost("/api/account/keys", async (
    ActivationRequest? request,
    HttpRequest httpRequest,
    TaskbookStore store,
    AccountService accountsService,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(httpRequest, store);
    if (principal is null)
        return Results.Unauthorized();
    if (request is null || string.IsNullOrWhiteSpace(request.Code))
        return Results.BadRequest(new { error = "Введите ключ активации." });
    try
    {
        var message = accountsService.ActivateKey(principal, request.Code);
        var profile = await BuildProfileAsync(accountsService, store, principal, cancellationToken);
        return Results.Ok(new ActivationResponse(message, profile));
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("account-api");

app.MapGet("/api/account/files", async (
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    return Results.Ok(await store.ListOwnerFilesAsync(principal.UserId, cancellationToken));
}).RequireRateLimiting("account-api");

app.MapPost("/api/account/files", async (
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Ожидается форма с файлом .pvz." });

    try
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var file = form.Files.Count == 1 ? form.Files[0] : null;
        if (file is null || file.Length is <= 0 or > TaskbookStore.MaximumPackageBytes)
            return Results.BadRequest(new { error = "Прикрепите один файл .pvz размером до 16 МиБ." });
        if (!TryParseRetention(form["retention"].ToString(), out var retention))
            return Results.BadRequest(new { error = "Выберите срок хранения задачника." });

        byte[] package;
        await using (var stream = file.OpenReadStream())
            package = await ReadBoundedAsync(stream, cancellationToken);
        var receipt = await store.UploadAsync(principal, package, retention, cancellationToken);
        return Results.Ok(receipt);
    }
    catch (ServerQuotaException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (ServerUnauthorizedException)
    {
        return Results.Unauthorized();
    }
}).RequireRateLimiting("account-api");

app.MapDelete("/api/account/files/{id}", async (
    string id,
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    return await store.CloseOwnerSessionAsync(principal.UserId, id, cancellationToken)
        ? Results.NoContent()
        : Results.NotFound();
}).RequireRateLimiting("account-api");

app.MapGet("/api/account/files/{id}/download", async (
    string id,
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    var package = await store.DownloadOwnerFileAsync(principal.UserId, id, cancellationToken);
    return package is null
        ? Results.NotFound()
        : Results.File(package, "application/octet-stream", "taskbook.pvz");
}).RequireRateLimiting("account-api");

app.MapPost("/api/admin/keys", (
    AdminKeyRequest? request,
    HttpRequest httpRequest,
    TaskbookStore store,
    AccountService accountsService) =>
{
    if (EnsureAdmin(httpRequest, store, out var principal, out _) is { } deny)
        return deny;
    if (request is null)
        return Results.BadRequest(new { error = "Укажите параметры ключа." });
    try
    {
        return Results.Ok(accountsService.CreateKey(
            principal,
            request.Kind,
            request.StoragePlan,
            request.DurationDays,
            request.Subject,
            request.ClassNumber));
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("teacher-api");

app.MapGet("/api/admin/keys", (
    HttpRequest httpRequest,
    TaskbookStore store,
    AccountService accountsService) =>
{
    if (EnsureAdmin(httpRequest, store, out _, out _) is { } deny)
        return deny;
    return Results.Ok(accountsService.ListKeys());
}).RequireRateLimiting("teacher-api");

app.MapGet("/api/admin/teachers", async (
    HttpRequest httpRequest,
    TaskbookStore store,
    AccountService accountsService,
    CancellationToken cancellationToken) =>
{
    if (EnsureAdmin(httpRequest, store, out _, out _) is { } deny)
        return deny;

    var now = DateTimeOffset.UtcNow;
    var sessions = await store.ListAllSessionsAsync(cancellationToken);
    var byOwner = sessions
        .Where(session => session.OwnerId is not null)
        .ToLookup(session => session.OwnerId!.Value);
    var teachers = accountsService.ListUsers()
        .Select(user =>
        {
            var plan = AccountService.EffectivePlan(user, now);
            DateTimeOffset? planUntil = user.IsAdmin || user.StorageUntil is not { } until || until <= now ? null : until;
            return new AdminTeacherDto(
                user.Id,
                user.Username,
                user.Email,
                user.FullName,
                user.School,
                plan,
                StorageLimits.PlanDisplayName(plan),
                planUntil,
                byOwner[user.Id].ToArray());
        })
        .ToArray();
    return Results.Ok(teachers);
}).RequireRateLimiting("teacher-api");

app.MapPost("/api/taskbooks/{token}/sessions", async (
    string token,
    OnlineStudentInfo? student,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    if (student is null)
        return Results.BadRequest(new { error = "Укажите имя, фамилию и класс." });
    try
    {
        var session = await store.StartStudentSessionAsync(token, student, cancellationToken);
        return session is null ? Results.NotFound() : Results.Ok(session);
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("student-api");

app.MapPut("/api/student/sessions/{sessionId}/progress", async (
    string sessionId,
    OnlineProgressUpdate? update,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    if (update is null)
        return Results.BadRequest(new { error = "Не передан прогресс ученика." });
    return await store.ReportProgressAsync(update with { SessionId = sessionId }, cancellationToken)
        ? Results.NoContent()
        : Results.NotFound();
}).RequireRateLimiting("student-api");

// ---- Пересылка файлов: материалы от учителя и работы учеников ----

app.MapGet("/api/student/sessions/{sessionId}/files", async (
    string sessionId,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var files = await store.ListStudentFilesAsync(sessionId, cancellationToken);
    return files is null ? Results.NotFound() : Results.Ok(files);
}).RequireRateLimiting("student-api");

app.MapGet("/api/student/sessions/{sessionId}/files/{fileId}", async (
    string sessionId,
    string fileId,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var file = await store.OpenStudentFileAsync(sessionId, fileId, cancellationToken);
    return file is null
        ? Results.NotFound()
        : Results.File(file.Value.Content, "application/octet-stream", file.Value.Info.Name);
}).RequireRateLimiting("student-api");

app.MapPost("/api/student/sessions/{sessionId}/files", async (
    string sessionId,
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var (name, content, error) = await ReadExchangeUploadAsync(request, cancellationToken);
    if (error is not null)
        return error;
    try
    {
        var info = await store.AddStudentFileAsync(sessionId, name, content, cancellationToken);
        return info is null ? Results.NotFound() : Results.Ok(info);
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("student-api");

app.MapGet("/api/teacher/sessions/{id}/files", async (
    string id,
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    var files = await store.ListSessionFilesAsync(principal, id, cancellationToken);
    return files is null ? Results.NotFound() : Results.Ok(files);
}).RequireRateLimiting("teacher-api");

app.MapPost("/api/teacher/sessions/{id}/files", async (
    string id,
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    var (name, content, error) = await ReadExchangeUploadAsync(request, cancellationToken);
    if (error is not null)
        return error;
    try
    {
        var info = await store.AddSessionFileAsync(principal, id, name, content, cancellationToken);
        return info is null ? Results.NotFound() : Results.Ok(info);
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("teacher-api");

app.MapGet("/api/teacher/sessions/{id}/files/{fileId}/download", async (
    string id,
    string fileId,
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    var file = await store.OpenSessionFileAsync(principal, id, fileId, cancellationToken);
    return file is null
        ? Results.NotFound()
        : Results.File(file.Value.Content, "application/octet-stream", file.Value.Info.Name);
}).RequireRateLimiting("teacher-api");

app.MapDelete("/api/teacher/sessions/{id}/files/{fileId}", async (
    string id,
    string fileId,
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    var principal = GetPrincipal(request, store);
    if (principal is null)
        return Results.Unauthorized();
    return await store.DeleteSessionFileAsync(principal, id, fileId, cancellationToken)
        ? Results.NoContent()
        : Results.NotFound();
}).RequireRateLimiting("teacher-api");

app.MapGet("/api/taskbooks/{token}", async (
    string token,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    try
    {
        var package = await store.DownloadAsync(token, cancellationToken);
        return package is null
            ? Results.NotFound()
            : Results.File(package, "application/octet-stream", "taskbook.pvz");
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("student-api");

app.Run();

static string GetClientAddress(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static StoragePlan ParsePlan(string? raw) =>
    string.Equals(raw, "paid-month", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(raw, "PaidOneMonth", StringComparison.OrdinalIgnoreCase)
        ? StoragePlan.PaidOneMonth
        : StoragePlan.FreeTwoWeeks;

static bool TryParseRetention(string? raw, out RetentionPeriod retention)
{
    retention = default;
    if (string.IsNullOrWhiteSpace(raw) || !Enum.TryParse<RetentionPeriod>(raw.Trim(), ignoreCase: true, out var parsed))
        return false;
    // TwoWeeks — срок настольного PVZT; в кабинете доступны только новые тарифы.
    if (parsed == RetentionPeriod.TwoWeeks)
        return false;
    retention = parsed;
    return true;
}

static bool TryGetAccessToken(HttpRequest request, out string accessToken)
{
    var authorization = request.Headers.Authorization.ToString();
    const string prefix = "Bearer ";
    if (authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        accessToken = authorization[prefix.Length..].Trim();
        return accessToken.Length == 43;
    }

    accessToken = "";
    return false;
}

static AccountPrincipal? GetPrincipal(HttpRequest request, TaskbookStore store) =>
    TryGetAccessToken(request, out var accessToken) ? store.AuthenticateAccount(accessToken) : null;

static IResult? EnsureAdmin(
    HttpRequest request,
    TaskbookStore store,
    out AccountPrincipal principal,
    out string accessToken)
{
    principal = null!;
    accessToken = "";
    if (!TryGetAccessToken(request, out var token))
        return Results.Unauthorized();
    var found = store.AuthenticateAccount(token);
    if (found is null)
        return Results.Unauthorized();
    if (!found.IsAdmin)
        return Results.Json(new { error = "Доступ только для администратора." }, statusCode: StatusCodes.Status403Forbidden);
    principal = found;
    accessToken = token;
    return null;
}

static async Task<AccountProfileDto> BuildProfileAsync(
    AccountService accountsService,
    TaskbookStore store,
    AccountPrincipal principal,
    CancellationToken cancellationToken)
{
    var usage = await store.GetOwnerUsageAsync(principal.UserId, cancellationToken);
    return accountsService.GetProfile(principal, usage);
}

static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken) =>
    await ReadBoundedCoreAsync(stream, TaskbookStore.MaximumPackageBytes, "Размер .pvz превышает 16 МиБ.", cancellationToken);

static async Task<byte[]> ReadBoundedCoreAsync(Stream stream, int maximumBytes, string error, CancellationToken cancellationToken)
{
    using var buffer = new MemoryStream();
    var chunk = new byte[81920];
    int read;
    while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
    {
        if (buffer.Length + read > maximumBytes)
            throw new InvalidDataException(error);
        await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
    }

    return buffer.ToArray();
}

/// <summary>Читает multipart-форму с одним файлом; Error != null — готовый ответ-ошибка.</summary>
static async Task<(string Name, byte[] Content, IResult? Error)> ReadExchangeUploadAsync(
    HttpRequest request,
    CancellationToken cancellationToken)
{
    if (!request.HasFormContentType)
        return ("", [], Results.BadRequest(new { error = "Ожидается форма с одним файлом." }));

    IFormCollection form;
    try
    {
        form = await request.ReadFormAsync(cancellationToken);
    }
    catch (InvalidDataException)
    {
        return ("", [], Results.BadRequest(new { error = "Не удалось прочитать файл." }));
    }

    var file = form.Files.Count == 1 ? form.Files[0] : null;
    if (file is null || file.Length <= 0)
        return ("", [], Results.BadRequest(new { error = "Прикрепите один файл." }));
    if (file.Length > TaskbookStore.MaximumExchangeFileBytes)
        return ("", [], Results.BadRequest(new { error = "Файл больше 32 МиБ." }));

    byte[] content;
    try
    {
        await using var stream = file.OpenReadStream();
        content = await ReadBoundedCoreAsync(
            stream,
            TaskbookStore.MaximumExchangeFileBytes,
            "Файл больше 32 МиБ.",
            cancellationToken);
    }
    catch (InvalidDataException exception)
    {
        return ("", [], Results.BadRequest(new { error = exception.Message }));
    }

    var name = form["name"].ToString();
    if (string.IsNullOrWhiteSpace(name))
        name = file.FileName ?? "";
    return (name, content, null);
}

internal sealed record LoginRequest(string Password);
internal sealed record RegisterRequest(string Username, string FullName, string School, string Email, string Password);
internal sealed record AccountLoginRequest(string Identifier, string Password);
internal sealed record ProfileUpdateRequest(string FullName, string School);
internal sealed record PasswordChangeRequest(string CurrentPassword, string NewPassword);
internal sealed record ActivationRequest(string Code);
internal sealed record AdminKeyRequest(string Kind, string? StoragePlan, int DurationDays, string? Subject, int? ClassNumber);
internal sealed record AccountAuthResponse(string AccessToken, DateTimeOffset ExpiresAt, AccountProfileDto Profile);
internal sealed record ActivationResponse(string Message, AccountProfileDto Profile);
internal partial class Program;
