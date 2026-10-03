using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PVZ.Core;
using PVZ.Server;

var repositoryRoot = FindRepositoryRoot();
var serverAssembly = new[]
{
    Path.Combine(repositoryRoot, "PVZ.Server", "verify-output", "Debug", "net10.0", "PVZ.Server.dll"),
    Path.Combine(repositoryRoot, "PVZ.Server", "bin", "Debug", "net10.0", "PVZ.Server.dll"),
}.FirstOrDefault(File.Exists)
    ?? throw new FileNotFoundException("Build PVZ.Server before running integration tests.");

var portListener = new TcpListener(IPAddress.Loopback, 0);
portListener.Start();
var port = ((IPEndPoint)portListener.LocalEndpoint).Port;
portListener.Stop();
var serverUrl = $"http://127.0.0.1:{port}";
var dataDirectory = Path.Combine(Path.GetTempPath(), $"pvz-server-test-{Guid.NewGuid():N}");
var adminKey = Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
using var client = new HttpClient { BaseAddress = new Uri(serverUrl), Timeout = TimeSpan.FromSeconds(3) };
Process? server = null;
var expiryDirectory = Path.Combine(Path.GetTempPath(), $"pvz-expiry-test-{Guid.NewGuid():N}");

try
{
    var document = new TaskbookDocument
    {
        Tasks =
        [
            new TaskbookTaskDto
            {
                Description = "Persisted test task",
                Tests = [new TestCaseDto { Input = "a", Output = "b" }],
            },
        ],
    };
    server = StartServer(serverAssembly, serverUrl, dataDirectory, adminKey);
    await WaitForServerAsync(client, server);
    using var noRedirectClient = new HttpClient(
        new HttpClientHandler { AllowAutoRedirect = false })
    {
        BaseAddress = new Uri(serverUrl),
    };
    using (var root = await noRedirectClient.GetAsync("/", HttpCompletionOption.ResponseHeadersRead))
        Require(root.StatusCode == HttpStatusCode.Redirect && root.Headers.Location?.OriginalString == "/admin", "Root redirects to admin dashboard");

    using (var dashboard = await client.GetAsync("/admin"))
    {
        Require(dashboard.IsSuccessStatusCode, "Admin dashboard route");
        Require((await dashboard.Content.ReadAsStringAsync()).Contains("PVZ Server Admin", StringComparison.Ordinal), "Admin dashboard content");
    }

    using (var unauthorized = await client.PostAsync("/api/admin/taskbooks", new ByteArrayContent([])))
        Require(unauthorized.StatusCode == HttpStatusCode.Unauthorized, "Reject upload without admin key");

    using (var invalidRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/taskbooks"))
    {
        invalidRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminKey);
        invalidRequest.Content = new ByteArrayContent([1, 2, 3]);
        invalidRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var invalidResponse = await client.SendAsync(invalidRequest);
        Require(invalidResponse.StatusCode == HttpStatusCode.BadRequest, "Reject invalid package upload");
    }

    var upload = await TaskbookPublisher.UploadOnlineAsync(document, serverUrl, adminKey);
    Require(upload.Success && upload.AccessToken is not null, upload.ErrorMessage ?? "Authorized client upload");
    var token = upload.AccessToken!;
    Require(token.Length == 43, "Token format");
    var downloaded = await TaskbookOnlineClient.DownloadAsync(token, serverUrl);
    Require(downloaded.Tasks[0].Description == document.Tasks[0].Description, "Student client download and package validation");

    var tokenId = await GetTokenIdAsync(client, adminKey, token);
    server.Kill(entireProcessTree: true);
    await server.WaitForExitAsync();
    server.Dispose();
    server = StartServer(serverAssembly, serverUrl, dataDirectory, adminKey);
    await WaitForServerAsync(client, server);
    var persisted = await TaskbookOnlineClient.DownloadAsync(token, serverUrl);
    Require(persisted.Tasks[0].Description == document.Tasks[0].Description, "Token and package persist across restart");

    using var revokeRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/taskbooks/{tokenId}");
    revokeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminKey);
    using (var revoke = await client.SendAsync(revokeRequest))
        Require(revoke.StatusCode == HttpStatusCode.NoContent, "Admin token revocation");
    using (var revoked = await client.GetAsync($"/api/taskbooks/{Uri.EscapeDataString(token)}"))
        Require(revoked.StatusCode == HttpStatusCode.NotFound, "Revoked token rejected");

    var listed = await GetAdminListAsync(client, adminKey);
    Require(listed.Length == 1 && !listed[0].IsActive, "Revocation shown in admin dashboard API");
    Require(!listed[0].TokenHint.Contains(token, StringComparison.Ordinal), "Store/list does not expose full token");

    var samplePackage = Path.Combine(Path.GetTempPath(), $"pvz-expiry-package-{Guid.NewGuid():N}.pvz");
    try
    {
        TaskbookPackageIO.Save(samplePackage, document);
        var store = new TaskbookStore(expiryDirectory, TimeSpan.FromSeconds(1));
        var expiryUpload = await store.UploadAsync(await File.ReadAllBytesAsync(samplePackage));
        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        Require(await store.DownloadAsync(expiryUpload.Token) is null, "Expired token rejected");
        var expiredList = await store.ListAsync();
        Require(expiredList.Count == 1 && !expiredList[0].IsActive, "Expiry shown in token lifecycle listing");
    }
    finally
    {
        if (File.Exists(samplePackage))
            File.Delete(samplePackage);
    }

    Console.WriteLine("PASS: server startup, admin auth, invalid upload rejection, upload/download, persistence, dashboard, revocation, and expiry");
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

static Process StartServer(string assembly, string url, string dataDirectory, string adminKey)
{
    var start = new ProcessStartInfo("dotnet", $"\"{assembly}\"")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
    };
    start.Environment["ASPNETCORE_URLS"] = url;
    start.Environment["PVZ_SERVER_ADMIN_KEY"] = adminKey;
    start.Environment["PVZ_SERVER_DATA_DIR"] = dataDirectory;
    start.Environment["PVZ_TOKEN_LIFETIME_DAYS"] = "2";
    var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start PVZ.Server.");
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}

static async Task WaitForServerAsync(HttpClient client, Process process)
{
    for (var attempt = 0; attempt < 80; attempt++)
    {
        if (process.HasExited)
            throw new InvalidOperationException($"PVZ.Server exited during startup with code {process.ExitCode}.");
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

    throw new TimeoutException("PVZ.Server did not become ready.");
}

static async Task<string> GetTokenIdAsync(HttpClient client, string adminKey, string token)
{
    var list = await GetAdminListAsync(client, adminKey);
    var hint = token[..8];
    var record = list.SingleOrDefault(item => item.TokenHint == $"{hint}...")
        ?? throw new InvalidOperationException("Uploaded token missing from admin list.");
    return record.Id;
}

static async Task<TokenSummary[]> GetAdminListAsync(HttpClient client, string adminKey)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/taskbooks");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminKey);
    using var response = await client.SendAsync(request);
    Require(response.IsSuccessStatusCode, "Authorized admin token listing");
    return await response.Content.ReadFromJsonAsync<TokenSummary[]>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidOperationException("Admin list returned no data.");
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

sealed record TokenSummary(string Id, string TokenHint, DateTimeOffset UploadedAt, DateTimeOffset ExpiresAt, bool IsActive);
