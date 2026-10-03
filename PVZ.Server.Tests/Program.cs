using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net.Http.Json;
using PVZ.Core;
using PVZ.Server;

var repositoryRoot = FindRepositoryRoot();
var serverAssembly = new[]
{
    Environment.GetEnvironmentVariable("PVZ_SERVER_TEST_ASSEMBLY"),
    Path.Combine(AppContext.BaseDirectory, "PVZ.Server.dll"),
    Path.Combine(repositoryRoot, "PVZ.Server", "verify-output", "Debug", "net10.0", "PVZ.Server.dll"),
    Path.Combine(repositoryRoot, "PVZ.Server", "bin", "Debug", "net10.0", "PVZ.Server.dll"),
}.FirstOrDefault(path => path is not null && File.Exists(path))
    ?? throw new FileNotFoundException("Build PVZ.Server before running integration tests.");

var portListener = new TcpListener(IPAddress.Loopback, 0);
portListener.Start();
var port = ((IPEndPoint)portListener.LocalEndpoint).Port;
portListener.Stop();
var serverUrl = $"http://127.0.0.1:{port}";
var dataDirectory = Path.Combine(Path.GetTempPath(), $"pvz-server-test-{Guid.NewGuid():N}");
var expiryDirectory = Path.Combine(Path.GetTempPath(), $"pvz-expiry-test-{Guid.NewGuid():N}");
using var client = new HttpClient { BaseAddress = new Uri(serverUrl), Timeout = TimeSpan.FromSeconds(5) };
Process? server = null;

try
{
    var document = new TaskbookDocument
    {
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
    server = StartServer(serverAssembly, serverUrl, dataDirectory, tokenLifetimeDays: 2);
    await WaitForServerAsync(client, server);

    using var noRedirectClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
    {
        BaseAddress = new Uri(serverUrl),
    };
    using (var root = await noRedirectClient.GetAsync("/", HttpCompletionOption.ResponseHeadersRead))
        Require(root.StatusCode == HttpStatusCode.Redirect && root.Headers.Location?.OriginalString == "/admin",
            "Root redirects to admin dashboard");

    using (var dashboard = await client.GetAsync("/admin"))
    {
        Require(dashboard.IsSuccessStatusCode, "Teacher dashboard route");
        var html = await dashboard.Content.ReadAsStringAsync();
        Require(html.Contains("Создать аккаунт учителя", StringComparison.Ordinal), "Dashboard supports teacher registration");
        Require(html.Contains("Прохождения учеников", StringComparison.Ordinal), "Dashboard displays student progress");
        Require(html.Contains("каждые 3 секунды", StringComparison.Ordinal), "Dashboard refreshes live progress");
        Require(html.Contains("Введите корректный личный API-ключ учителя", StringComparison.Ordinal), "Dashboard explains invalid teacher key");
        Require(html.Contains("response.status === 401 || response.status === 403", StringComparison.Ordinal),
            "Dashboard handles empty unauthorized responses before parsing JSON");
    }

    var teacher = await TaskbookOnlineClient.RegisterTeacherAsync("Учитель Один", serverUrl);
    var otherTeacher = await TaskbookOnlineClient.RegisterTeacherAsync("Учитель Два", serverUrl);
    Require(teacher.ApiKey.Length == 8 && teacher.ApiKey.All(char.IsAsciiLetterOrDigit), "Teacher key has eight characters");
    Require(otherTeacher.ApiKey.Length == 8 && teacher.ApiKey != otherTeacher.ApiKey, "Teacher registrations receive distinct keys");

    using (var invalidRegistration = await client.PostAsJsonAsync("/api/teachers/register", new { displayName = " " }))
        Require(invalidRegistration.StatusCode == HttpStatusCode.BadRequest, "Reject invalid teacher registration");

    using (var unauthorized = await client.GetAsync("/api/teacher/taskbooks"))
        Require(unauthorized.StatusCode == HttpStatusCode.Unauthorized, "Reject teacher list without key");

    using (var invalidTeacherKey = new HttpRequestMessage(HttpMethod.Get, "/api/teacher/taskbooks"))
    {
        invalidTeacherKey.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "AAAAAAAA");
        using var response = await client.SendAsync(invalidTeacherKey);
        Require(response.StatusCode == HttpStatusCode.Unauthorized, "Reject an unregistered teacher key");
    }

    using (var unauthorized = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/taskbooks"))
    {
        unauthorized.Content = new ByteArrayContent([1, 2, 3]);
        unauthorized.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var response = await client.SendAsync(unauthorized);
        Require(response.StatusCode == HttpStatusCode.Unauthorized, "Reject upload without teacher key");
    }

    using (var invalidRequest = new HttpRequestMessage(HttpMethod.Post, "/api/teacher/taskbooks"))
    {
        invalidRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacher.ApiKey);
        invalidRequest.Content = new ByteArrayContent([1, 2, 3]);
        invalidRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var invalidResponse = await client.SendAsync(invalidRequest);
        Require(invalidResponse.StatusCode == HttpStatusCode.BadRequest, "Reject invalid package upload");
    }

    using (var invalidStudentToken = await client.GetAsync("/api/taskbooks/TOO-LONG"))
        Require(invalidStudentToken.StatusCode == HttpStatusCode.BadRequest, "Reject malformed student token");

    var packagePath = Path.Combine(Path.GetTempPath(), $"pvz-server-test-{Guid.NewGuid():N}.pvz");
    try
    {
        TaskbookPackageIO.Save(packagePath, document);
        var upload = await TaskbookOnlineClient.UploadAsync(packagePath, serverUrl, teacher.ApiKey);
        Require(upload.Token.Length == 6, "Student token has six characters");
        Require(upload.Token.All(character => "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".Contains(character)), "Token uses unambiguous alphabet");

        var ownBooks = await TaskbookOnlineClient.ListTeacherTaskbooksAsync(teacher.ApiKey, serverUrl);
        var otherBooks = await TaskbookOnlineClient.ListTeacherTaskbooksAsync(otherTeacher.ApiKey, serverUrl);
        Require(ownBooks.Count == 1 && ownBooks[0].TokenHint.StartsWith(upload.Token[..2], StringComparison.Ordinal),
            "Teacher sees own taskbook");
        Require(otherBooks.Count == 0, "Teacher cannot see another teacher's taskbooks");

        var session = await TaskbookOnlineClient.StartStudentSessionAsync(
            upload.Token,
            new OnlineStudentInfo("Анна", "Иванова", "7А"),
            serverUrl);
        var downloaded = await TaskbookOnlineClient.DownloadAsync(upload.Token, serverUrl);
        Require(downloaded.Tasks[0].Description == document.Tasks[0].Description, "Student downloads and validates the PVZ package");

        await TaskbookOnlineClient.ReportProgressAsync(
            new OnlineProgressUpdate(session.SessionId, [new StudentTaskProgressDto(30, true)]),
            serverUrl);
        var sessions = await TaskbookOnlineClient.ListStudentProgressAsync(teacher.ApiKey, serverUrl);
        Require(sessions.Count == 1, "Teacher sees own student sessions");
        Require(sessions[0].FirstName == "Анна" && sessions[0].LastName == "Иванова" && sessions[0].ClassName == "7А",
            "Student identity is recorded");
        Require(sessions[0].EarnedPoints == 30 && sessions[0].MaximumPoints == 30 &&
            sessions[0].CompletedTasks == 1 && sessions[0].TotalTasks == 1 && sessions[0].IsOnline,
            "Live student score and progress are recorded");
        Require((await TaskbookOnlineClient.ListStudentProgressAsync(otherTeacher.ApiKey, serverUrl)).Count == 0,
            "Teacher cannot see another teacher's student sessions");

        var teacherBookId = ownBooks.Single().Id;
        using var foreignRevoke = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/taskbooks/{teacherBookId}");
        foreignRevoke.Headers.Authorization = new AuthenticationHeaderValue("Bearer", otherTeacher.ApiKey);
        using (var forbidden = await client.SendAsync(foreignRevoke))
            Require(forbidden.StatusCode == HttpStatusCode.NotFound, "Teacher cannot revoke another teacher's taskbook");

        server.Kill(entireProcessTree: true);
        await server.WaitForExitAsync();
        server.Dispose();
        server = StartServer(serverAssembly, serverUrl, dataDirectory, tokenLifetimeDays: 2);
        await WaitForServerAsync(client, server);
        var persisted = await TaskbookOnlineClient.DownloadAsync(upload.Token, serverUrl);
        var persistedSessions = await TaskbookOnlineClient.ListStudentProgressAsync(teacher.ApiKey, serverUrl);
        Require(persisted.Tasks[0].Description == document.Tasks[0].Description, "Taskbook persists across server restart");
        Require(persistedSessions.Count == 1 && persistedSessions[0].EarnedPoints == 30, "Student progress persists across server restart");

        using var revokeRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/teacher/taskbooks/{teacherBookId}");
        revokeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", teacher.ApiKey);
        using (var revoke = await client.SendAsync(revokeRequest))
            Require(revoke.StatusCode == HttpStatusCode.NoContent, "Teacher revokes owned taskbook");
        using (var revoked = await client.GetAsync($"/api/taskbooks/{Uri.EscapeDataString(upload.Token)}"))
            Require(revoked.StatusCode == HttpStatusCode.NotFound, "Revoked token cannot download");
    }
    finally
    {
        if (File.Exists(packagePath))
            File.Delete(packagePath);
    }

    var expiryStore = new TaskbookStore(expiryDirectory, TimeSpan.FromSeconds(1));
    var expiryTeacher = await expiryStore.RegisterTeacherAsync("Временный учитель");
    var expiryPackage = Path.Combine(Path.GetTempPath(), $"pvz-expiry-package-{Guid.NewGuid():N}.pvz");
    try
    {
        TaskbookPackageIO.Save(expiryPackage, document);
        var expiryUpload = await expiryStore.UploadAsync(
            expiryTeacher.ApiKey,
            await File.ReadAllBytesAsync(expiryPackage));
        Require(expiryUpload.Token.Length == 6, "Store token length for expiry lifecycle");
        await Task.Delay(TimeSpan.FromMilliseconds(1200));
        Require(await expiryStore.DownloadAsync(expiryUpload.Token) is null, "Expired token is rejected");
        var expiredList = await expiryStore.ListTaskbooksAsync(expiryTeacher.ApiKey);
        Require(expiredList.Count == 1 && !expiredList[0].IsActive, "Expiry shown in owner taskbook listing");
    }
    finally
    {
        if (File.Exists(expiryPackage))
            File.Delete(expiryPackage);
    }

    Console.WriteLine("PASS: redirect, teacher registration and key scope, package validation/upload/download, six-character tokens, student identity/live progress, persistence, owner-only revocation, and expiry");
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

static Process StartServer(string assembly, string url, string dataDirectory, int tokenLifetimeDays)
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
    start.Environment["PVZ_TOKEN_LIFETIME_DAYS"] = tokenLifetimeDays.ToString();
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
