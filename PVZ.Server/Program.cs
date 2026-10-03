using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using PVZ.Core;
using PVZ.Server;

var builder = WebApplication.CreateBuilder(args);
var dataDirectory = Environment.GetEnvironmentVariable("PVZ_SERVER_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDirectory))
    dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PVZ.Server");
var tokenLifetime = ResolveTokenLifetime();

builder.WebHost.UseUrls(
    Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://0.0.0.0:5080");
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
builder.Services.AddSingleton(new TaskbookStore(dataDirectory, tokenLifetime));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("registration", context => RateLimitPartition.GetFixedWindowLimiter(
        GetClientAddress(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    options.AddPolicy("teacher-api", context => RateLimitPartition.GetFixedWindowLimiter(
        GetClientAddress(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 90,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
    options.AddPolicy("student-api", context => RateLimitPartition.GetFixedWindowLimiter(
        GetClientAddress(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
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
app.MapGet("/", () => Results.Redirect("/admin"));
app.MapGet("/admin", () => Results.Content(AdminPage.Content, "text/html; charset=utf-8"));

app.MapPost("/api/teachers/register", async (
    TeacherRegistrationRequest? request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    if (request is null)
        return Results.BadRequest(new { error = "Укажите имя учителя." });
    try
    {
        return Results.Ok(await store.RegisterTeacherAsync(request.DisplayName, cancellationToken));
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("registration");

app.MapPost("/api/teacher/taskbooks", async (
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    if (!TryGetTeacherKey(request, out var teacherKey))
        return Results.Unauthorized();
    if (!await store.IsRegisteredTeacherAsync(teacherKey, cancellationToken))
        return Results.Unauthorized();
    if (!string.Equals(request.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
    if (request.ContentLength is > TaskbookStore.MaximumPackageBytes)
        return Results.BadRequest(new { error = "Размер .pvz превышает 16 МиБ." });

    try
    {
        var package = await ReadBoundedAsync(request.Body, cancellationToken);
        var uploaded = await store.UploadAsync(teacherKey, package, cancellationToken);
        return Results.Ok(uploaded);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).RequireRateLimiting("teacher-api");

app.MapGet("/api/teacher/taskbooks", async (
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    if (!TryGetTeacherKey(request, out var teacherKey))
        return Results.Unauthorized();
    try
    {
        return Results.Ok(await store.ListTaskbooksAsync(teacherKey, cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
}).RequireRateLimiting("teacher-api");

app.MapGet("/api/teacher/sessions", async (
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    if (!TryGetTeacherKey(request, out var teacherKey))
        return Results.Unauthorized();
    try
    {
        return Results.Ok(await store.ListStudentSessionsAsync(teacherKey, cancellationToken));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
}).RequireRateLimiting("teacher-api");

app.MapDelete("/api/teacher/taskbooks/{id}", async (
    string id,
    HttpRequest request,
    TaskbookStore store,
    CancellationToken cancellationToken) =>
{
    if (!TryGetTeacherKey(request, out var teacherKey))
        return Results.Unauthorized();
    try
    {
        return await store.RevokeAsync(teacherKey, id, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound();
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Unauthorized();
    }
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

static TimeSpan ResolveTokenLifetime()
{
    const int defaultHours = 48;
    var hoursSetting = Environment.GetEnvironmentVariable("PVZ_TOKEN_LIFETIME_HOURS");
    if (!string.IsNullOrWhiteSpace(hoursSetting))
    {
        if (!int.TryParse(hoursSetting, out var hours) || hours is < 1 or > 87600)
            throw new InvalidOperationException("PVZ_TOKEN_LIFETIME_HOURS must be an integer from 1 through 87600.");
        return TimeSpan.FromHours(hours);
    }

    var daysSetting = Environment.GetEnvironmentVariable("PVZ_TOKEN_LIFETIME_DAYS");
    if (!string.IsNullOrWhiteSpace(daysSetting))
    {
        if (!int.TryParse(daysSetting, out var days) || days is < 1 or > 3650)
            throw new InvalidOperationException("PVZ_TOKEN_LIFETIME_DAYS must be an integer from 1 through 3650.");
        return TimeSpan.FromDays(days);
    }

    return TimeSpan.FromHours(defaultHours);
}

static string GetClientAddress(HttpContext context) =>
    context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

static bool TryGetTeacherKey(HttpRequest request, out string apiKey)
{
    var authorization = request.Headers.Authorization.ToString();
    const string prefix = "Bearer ";
    if (authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        apiKey = authorization[prefix.Length..].Trim();
        return apiKey.Length == 8;
    }

    apiKey = "";
    return false;
}

static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
{
    using var buffer = new MemoryStream();
    var chunk = new byte[81920];
    int read;
    while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
    {
        if (buffer.Length + read > TaskbookStore.MaximumPackageBytes)
            throw new InvalidDataException("Размер .pvz превышает 16 МиБ.");
        await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
    }

    return buffer.ToArray();
}

internal sealed record TeacherRegistrationRequest(string DisplayName);
internal partial class Program;
