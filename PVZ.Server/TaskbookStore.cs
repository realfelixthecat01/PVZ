using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PVZ.Core;

namespace PVZ.Server;

public sealed record TeacherRegistration(string TeacherId, string ApiKey);
public sealed record UploadedTaskbook(string Token, DateTimeOffset UploadedAt, DateTimeOffset ExpiresAt);
public sealed record TaskbookTokenSummary(string Id, string TokenHint, DateTimeOffset UploadedAt, DateTimeOffset ExpiresAt, bool IsActive);
public sealed record StudentSessionSummary(
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

public sealed class TaskbookStore
{
    public const int MaximumPackageBytes = 16 * 1024 * 1024;
    private const string KeyAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _dataDirectory;
    private readonly string _packageDirectory;
    private readonly string _statePath;
    private readonly TimeSpan _tokenLifetime;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private StoreState? _state;

    public TaskbookStore(string dataDirectory, int tokenLifetimeDays = 90)
        : this(dataDirectory, ValidateTokenLifetimeDays(tokenLifetimeDays))
    {
    }

    public TaskbookStore(string dataDirectory, TimeSpan tokenLifetime)
    {
        if (tokenLifetime < TimeSpan.FromSeconds(1) || tokenLifetime > TimeSpan.FromDays(3650))
            throw new ArgumentOutOfRangeException(nameof(tokenLifetime));
        _dataDirectory = Path.GetFullPath(dataDirectory);
        _packageDirectory = Path.Combine(_dataDirectory, "packages");
        _statePath = Path.Combine(_dataDirectory, "store.json");
        _tokenLifetime = tokenLifetime;
        Directory.CreateDirectory(_packageDirectory);
    }

    public async Task<TeacherRegistration> RegisterTeacherAsync(string displayName, CancellationToken cancellationToken = default)
    {
        displayName = NormalizeRequired(displayName, 80, "Укажите имя учителя.");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            string apiKey;
            string keyHash;
            do
            {
                apiKey = GenerateShortCode(8);
                keyHash = Hash(apiKey);
            }
            while (state.Teachers.Any(teacher => FixedTimeEqualsHex(teacher.ApiKeyHash, keyHash)));

            var teacherId = Guid.NewGuid().ToString("N");
            state.Teachers.Add(new TeacherRecord(teacherId, displayName, keyHash, DateTimeOffset.UtcNow));
            await SaveStateAsync(state, cancellationToken);
            _state = state;
            return new TeacherRegistration(teacherId, apiKey);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> IsRegisteredTeacherAsync(string teacherApiKey, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            return FindTeacher(state, teacherApiKey) is not null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<UploadedTaskbook> UploadAsync(string teacherApiKey, byte[] package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.Length == 0 || package.Length > MaximumPackageBytes)
            throw new InvalidDataException("Размер .pvz файла недопустим.");

        if (!await IsRegisteredTeacherAsync(teacherApiKey, cancellationToken))
            throw new UnauthorizedAccessException("Неверный ключ учителя.");

        var temporaryPath = Path.Combine(_dataDirectory, $".upload-{Guid.NewGuid():N}.pvz");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, package, cancellationToken);
            TaskbookDocument document;
            try
            {
                document = TaskbookPackageIO.Load(temporaryPath);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Файл не содержит корректный задачник PVZ.", exception);
            }

            await _gate.WaitAsync(cancellationToken);
            try
            {
                var state = await LoadStateAsync(cancellationToken);
                var teacher = FindTeacher(state, teacherApiKey);
                if (teacher is null)
                    throw new UnauthorizedAccessException("Неверный ключ учителя.");
                DeleteInactivePackages(state);

                var token = GenerateShortCode(6);
                var tokenHash = Hash(token);
                while (state.Taskbooks.Any(book => FixedTimeEqualsHex(book.TokenHash, tokenHash)))
                {
                    token = GenerateShortCode(6);
                    tokenHash = Hash(token);
                }

                var uploadedAt = DateTimeOffset.UtcNow;
                var expiresAt = uploadedAt + _tokenLifetime;
                var packageFile = $"{Guid.NewGuid():N}.pvz";
                var targetPath = Path.Combine(_packageDirectory, packageFile);
                File.Move(temporaryPath, targetPath);
                state.Taskbooks.Add(new TaskbookRecord(
                    Guid.NewGuid().ToString("N"),
                    teacher.TeacherId,
                    tokenHash,
                    token[..2],
                    packageFile,
                    uploadedAt,
                    expiresAt,
                    null,
                    document.Tasks.Count,
                    TaskbookScoring.GetDocumentMaxScore(document)));
                try
                {
                    await SaveStateAsync(state, cancellationToken);
                }
                catch
                {
                    File.Delete(targetPath);
                    throw;
                }

                _state = state;
                return new UploadedTaskbook(token, uploadedAt, expiresAt);
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public async Task<byte[]?> DownloadAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = Hash(NormalizeToken(token));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            var book = state.Taskbooks.FirstOrDefault(item => FixedTimeEqualsHex(item.TokenHash, tokenHash));
            if (!IsActive(book))
            {
                if (book is not null)
                    DeletePackage(book);
                return null;
            }

            var path = SafePackagePath(book!.PackageFile);
            return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OnlineStudentSession?> StartStudentSessionAsync(
        string token,
        OnlineStudentInfo student,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(student);
        var firstName = NormalizeRequired(student.FirstName, 60, "Введите имя.");
        var lastName = NormalizeRequired(student.LastName, 60, "Введите фамилию.");
        var className = NormalizeRequired(student.ClassName, 30, "Введите класс.");
        var tokenHash = Hash(NormalizeToken(token));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            var book = state.Taskbooks.FirstOrDefault(item => FixedTimeEqualsHex(item.TokenHash, tokenHash));
            if (!IsActive(book))
                return null;

            var now = DateTimeOffset.UtcNow;
            var sessionId = Base64Url(RandomNumberGenerator.GetBytes(32));
            state.Sessions.Add(new StudentSessionRecord(
                sessionId,
                book!.TaskbookId,
                firstName,
                lastName,
                className,
                now,
                now,
                Enumerable.Repeat(0, book.TotalTasks).ToArray(),
                new bool[book.TotalTasks]));
            await SaveStateAsync(state, cancellationToken);
            _state = state;
            return new OnlineStudentSession(sessionId, now);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> ReportProgressAsync(OnlineProgressUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (string.IsNullOrWhiteSpace(update.SessionId) || update.Tasks is null || update.Tasks.Count > 1000)
            return false;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            var session = state.Sessions.FirstOrDefault(item =>
                CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(item.SessionId),
                    Encoding.UTF8.GetBytes(update.SessionId)));
            if (session is null)
                return false;
            var book = state.Taskbooks.FirstOrDefault(item => item.TaskbookId == session.TaskbookId);
            if (!IsActive(book) || update.Tasks.Count != book!.TotalTasks)
                return false;

            var packagePath = SafePackagePath(book.PackageFile);
            if (!File.Exists(packagePath))
                return false;
            var document = TaskbookPackageIO.Load(packagePath);
            if (document.Tasks.Count != update.Tasks.Count)
                return false;

            for (var index = 0; index < update.Tasks.Count; index++)
            {
                var task = update.Tasks[index];
                if (task is null || task.EarnedPoints < 0 ||
                    task.EarnedPoints > TaskbookScoring.GetTaskMaxScore(document.Tasks[index]))
                    return false;
            }

            session.EarnedPoints = update.Tasks.Select(item => item.EarnedPoints).ToArray();
            session.CompletedTasks = update.Tasks.Select(item => item.Completed).ToArray();
            session.LastActivityAt = DateTimeOffset.UtcNow;
            await SaveStateAsync(state, cancellationToken);
            _state = state;
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<TaskbookTokenSummary>> ListTaskbooksAsync(string teacherApiKey, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            var teacher = FindTeacher(state, teacherApiKey);
            if (teacher is null)
                throw new UnauthorizedAccessException("Неверный ключ учителя.");
            DeleteInactivePackages(state);
            var now = DateTimeOffset.UtcNow;
            return state.Taskbooks
                .Where(book => book.TeacherId == teacher.TeacherId)
                .OrderByDescending(book => book.UploadedAt)
                .Select(book => new TaskbookTokenSummary(
                    book.TaskbookId,
                    $"{book.TokenHint}****",
                    book.UploadedAt,
                    book.ExpiresAt,
                    IsActive(book, now)))
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<StudentSessionSummary>> ListStudentSessionsAsync(string teacherApiKey, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            var teacher = FindTeacher(state, teacherApiKey);
            if (teacher is null)
                throw new UnauthorizedAccessException("Неверный ключ учителя.");
            var books = state.Taskbooks.Where(book => book.TeacherId == teacher.TeacherId).ToDictionary(book => book.TaskbookId);
            var now = DateTimeOffset.UtcNow;
            return state.Sessions
                .Where(session => books.ContainsKey(session.TaskbookId))
                .OrderByDescending(session => session.LastActivityAt)
                .Select(session =>
                {
                    var book = books[session.TaskbookId];
                    var lastActivity = session.LastActivityAt;
                    var elapsed = (long)Math.Max(0, (lastActivity - session.StartedAt).TotalSeconds);
                    return new StudentSessionSummary(
                        session.FirstName,
                        session.LastName,
                        session.ClassName,
                        session.StartedAt,
                        lastActivity,
                        elapsed,
                        session.EarnedPoints.Sum(),
                        book.MaximumPoints,
                        session.CompletedTasks.Count(completed => completed),
                        book.TotalTasks,
                        now - lastActivity <= TimeSpan.FromSeconds(30));
                })
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RevokeAsync(string teacherApiKey, string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            var teacher = FindTeacher(state, teacherApiKey);
            if (teacher is null)
                throw new UnauthorizedAccessException("Неверный ключ учителя.");
            var index = state.Taskbooks.FindIndex(book => book.TaskbookId == id && book.TeacherId == teacher.TeacherId);
            if (index < 0 || state.Taskbooks[index].RevokedAt is not null)
                return false;
            state.Taskbooks[index] = state.Taskbooks[index] with { RevokedAt = DateTimeOffset.UtcNow };
            await SaveStateAsync(state, cancellationToken);
            _state = state;
            DeletePackage(state.Taskbooks[index]);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private TeacherRecord? FindTeacher(StoreState state, string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey) || apiKey.Length != 8 ||
            apiKey.Any(character => !KeyAlphabet.Contains(char.ToUpperInvariant(character))))
            return null;
        var hash = Hash(apiKey.ToUpperInvariant());
        return state.Teachers.FirstOrDefault(teacher => FixedTimeEqualsHex(teacher.ApiKeyHash, hash));
    }

    private void DeleteInactivePackages(StoreState state)
    {
        foreach (var book in state.Taskbooks.Where(book => !IsActive(book)))
            DeletePackage(book);
    }

    private bool IsActive(TaskbookRecord? book, DateTimeOffset? now = null) =>
        book is not null && book.RevokedAt is null && book.ExpiresAt > (now ?? DateTimeOffset.UtcNow);

    private void DeletePackage(TaskbookRecord book)
    {
        var packagePath = SafePackagePath(book.PackageFile);
        if (File.Exists(packagePath))
            File.Delete(packagePath);
    }

    private string SafePackagePath(string packageFile)
    {
        if (!string.Equals(packageFile, Path.GetFileName(packageFile), StringComparison.Ordinal) ||
            !packageFile.EndsWith(".pvz", StringComparison.Ordinal) ||
            packageFile.Length != 36)
            throw new InvalidDataException("В хранилище обнаружена недопустимая запись файла.");
        return Path.Combine(_packageDirectory, packageFile);
    }

    private async Task<StoreState> LoadStateAsync(CancellationToken cancellationToken)
    {
        if (_state is not null)
            return _state;
        if (!File.Exists(_statePath))
            return _state = new StoreState();
        await using var input = File.OpenRead(_statePath);
        _state = await JsonSerializer.DeserializeAsync<StoreState>(input, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Файл хранилища пуст или повреждён.");
        return _state;
    }

    private async Task SaveStateAsync(StoreState state, CancellationToken cancellationToken)
    {
        var temporaryPath = Path.Combine(_dataDirectory, $".store-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = File.Create(temporaryPath))
                await JsonSerializer.SerializeAsync(output, state, JsonOptions, cancellationToken);
            File.Move(temporaryPath, _statePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string NormalizeToken(string token)
    {
        if (token is null || token.Length != 6 || token.Any(character => !KeyAlphabet.Contains(char.ToUpperInvariant(character))))
            throw new InvalidDataException("Токен должен состоять из 6 символов.");
        return token.ToUpperInvariant();
    }

    private static string NormalizeRequired(string value, int maximumLength, string error)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
            throw new InvalidDataException(error);
        return normalized;
    }

    private static string GenerateShortCode(int length)
    {
        var random = RandomNumberGenerator.GetBytes(length);
        return string.Create(length, random, (chars, bytes) =>
        {
            for (var index = 0; index < chars.Length; index++)
                chars[index] = KeyAlphabet[bytes[index] & 31];
        });
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedTimeEqualsHex(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static TimeSpan ValidateTokenLifetimeDays(int tokenLifetimeDays)
    {
        if (tokenLifetimeDays is < 1 or > 3650)
            throw new ArgumentOutOfRangeException(nameof(tokenLifetimeDays));
        return TimeSpan.FromDays(tokenLifetimeDays);
    }

    private sealed class StoreState
    {
        public List<TeacherRecord> Teachers { get; set; } = new();
        public List<TaskbookRecord> Taskbooks { get; set; } = new();
        public List<StudentSessionRecord> Sessions { get; set; } = new();
    }

    private sealed record TeacherRecord(string TeacherId, string DisplayName, string ApiKeyHash, DateTimeOffset RegisteredAt);

    private sealed record TaskbookRecord(
        string TaskbookId,
        string TeacherId,
        string TokenHash,
        string TokenHint,
        string PackageFile,
        DateTimeOffset UploadedAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? RevokedAt,
        int TotalTasks,
        int MaximumPoints);

    private sealed class StudentSessionRecord(
        string sessionId,
        string taskbookId,
        string firstName,
        string lastName,
        string className,
        DateTimeOffset startedAt,
        DateTimeOffset lastActivityAt,
        int[] earnedPoints,
        bool[] completedTasks)
    {
        public string SessionId { get; } = sessionId;
        public string TaskbookId { get; } = taskbookId;
        public string FirstName { get; } = firstName;
        public string LastName { get; } = lastName;
        public string ClassName { get; } = className;
        public DateTimeOffset StartedAt { get; } = startedAt;
        public DateTimeOffset LastActivityAt { get; set; } = lastActivityAt;
        public int[] EarnedPoints { get; set; } = earnedPoints;
        public bool[] CompletedTasks { get; set; } = completedTasks;
    }
}
