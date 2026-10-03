using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.HttpOverrides;
using PVZ.Server;

var builder = WebApplication.CreateBuilder(args);
var adminKey = Environment.GetEnvironmentVariable("PVZ_SERVER_ADMIN_KEY");
if (string.IsNullOrWhiteSpace(adminKey) || Encoding.UTF8.GetByteCount(adminKey) < 32)
    throw new InvalidOperationException(
        "Set PVZ_SERVER_ADMIN_KEY to a unique secret of at least 32 UTF-8 bytes before starting PVZ.Server.");

var dataDirectory = Environment.GetEnvironmentVariable("PVZ_SERVER_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDirectory))
    dataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PVZ.Server");
var tokenLifetimeSetting = Environment.GetEnvironmentVariable("PVZ_TOKEN_LIFETIME_DAYS");
var tokenLifetimeDays = 90;
if (tokenLifetimeSetting is not null &&
    (!int.TryParse(tokenLifetimeSetting, out tokenLifetimeDays) || tokenLifetimeDays is < 1 or > 3650))
    throw new InvalidOperationException("PVZ_TOKEN_LIFETIME_DAYS must be an integer from 1 through 3650.");

builder.WebHost.UseUrls(
    Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://127.0.0.1:5080");
builder.Services.AddSingleton(new TaskbookStore(dataDirectory, tokenLifetimeDays));

var app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});
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

app.MapPost("/api/admin/taskbooks", async (HttpRequest request, TaskbookStore store, CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(request, adminKey))
        return Results.Unauthorized();
    if (!string.Equals(request.ContentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
    if (request.ContentLength is > TaskbookStore.MaximumPackageBytes)
        return Results.BadRequest(new { error = "Package exceeds the 16 MiB limit." });

    try
    {
        var bytes = await ReadBoundedAsync(request.Body, cancellationToken);
        var uploaded = await store.UploadAsync(bytes, cancellationToken);
        return Results.Ok(uploaded);
    }
    catch (InvalidDataException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapGet("/api/admin/taskbooks", async (HttpRequest request, TaskbookStore store, CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(request, adminKey))
        return Results.Unauthorized();
    return Results.Ok(await store.ListAsync(cancellationToken));
});

app.MapDelete("/api/admin/taskbooks/{id}", async (string id, HttpRequest request, TaskbookStore store, CancellationToken cancellationToken) =>
{
    if (!IsAuthorized(request, adminKey))
        return Results.Unauthorized();
    return await store.RevokeAsync(id, cancellationToken)
        ? Results.NoContent()
        : Results.NotFound();
});

app.MapGet("/api/taskbooks/{token}", async (string token, TaskbookStore store, CancellationToken cancellationToken) =>
{
    if (token.Length != 43 || token.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        return Results.NotFound();

    var package = await store.DownloadAsync(token, cancellationToken);
    return package is null
        ? Results.NotFound()
        : Results.File(package, "application/octet-stream", "taskbook.pvz");
});

app.Run();

static bool IsAuthorized(HttpRequest request, string expectedKey)
{
    var authorization = request.Headers.Authorization.ToString();
    const string prefix = "Bearer ";
    if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        return false;

    var providedKey = authorization[prefix.Length..].Trim();
    var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey));
    var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
    return CryptographicOperations.FixedTimeEquals(expectedHash, providedHash);
}

static async Task<byte[]> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
{
    using var buffer = new MemoryStream();
    var chunk = new byte[81920];
    int read;
    while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
    {
        if (buffer.Length + read > TaskbookStore.MaximumPackageBytes)
            throw new InvalidDataException("Package exceeds the 16 MiB limit.");
        await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
    }

    return buffer.ToArray();
}

internal partial class Program;
