using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PVZ.Core;

namespace PVZS;

public sealed class TaskbookStore
{
    public const string DefaultAccessPassword = "ezfridgembxd";
    public const int MaximumPackageBytes = 16 * 1024 * 1024;
    public const int MaximumExchangeFileBytes = 32 * 1024 * 1024;
    public const int MaximumSessionExchangeBytes = 512 * 1024 * 1024;
    public static readonly TimeSpan DefaultSessionLifetime = TimeSpan.FromDays(14);
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromHours(12);
    private const string KeyAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _sessionsRoot;
    private readonly string _catalogDirectory;
    private readonly TimeSpan _sessionLifetime;
    private readonly AccountService? _accounts;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, SessionFile> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AccessTokenEntry> _accessTokens = new(StringComparer.Ordinal);
    private bool _sessionsLoaded;

    public TaskbookStore(
        string sessionsDirectory,
        TimeSpan? sessionLifetime = null,
        string? catalogDirectory = null)
        : this(sessionsDirectory, sessionLifetime, catalogDirectory, accounts: null)
    {
    }

    internal TaskbookStore(
        string sessionsDirectory,
        TimeSpan? sessionLifetime,
        string? catalogDirectory,
        AccountService? accounts)
    {
        var lifetime = sessionLifetime ?? DefaultSessionLifetime;
        if (lifetime < TimeSpan.FromSeconds(1) || lifetime > TimeSpan.FromDays(3650))
            throw new ArgumentOutOfRangeException(nameof(sessionLifetime));
        _sessionsRoot = Path.GetFullPath(sessionsDirectory);
        _catalogDirectory = Path.GetFullPath(catalogDirectory ??
            Path.Combine(AppContext.BaseDirectory, "zadachniki"));
        _sessionLifetime = lifetime;
        _accounts = accounts;
        Directory.CreateDirectory(_sessionsRoot);
    }

    public IReadOnlyList<TaskbookCatalogItemDto> ListCatalogTaskbooks(string subject, int classNumber)
    {
        var directory = GetCatalogClassDirectory(subject, classNumber);
        if (!Directory.Exists(directory))
            return Array.Empty<TaskbookCatalogItemDto>();

        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(Path.GetExtension(path), ".pvz", StringComparison.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path))
            .Where(file => file.Length is > 0 and <= MaximumPackageBytes)
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Select(file => new TaskbookCatalogItemDto(file.Name, file.Length))
            .ToArray();
    }

    public byte[]? DownloadCatalogTaskbook(string subject, int classNumber, string fileName)
    {
        var directory = GetCatalogClassDirectory(subject, classNumber);
        if (string.IsNullOrWhiteSpace(fileName) ||
            !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(fileName), ".pvz", StringComparison.OrdinalIgnoreCase))
            return null;

        var path = Path.GetFullPath(Path.Combine(directory, fileName));
        if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(path))
            return null;

        var file = new FileInfo(path);
        if (file.Length is <= 0 or > MaximumPackageBytes)
            return null;

        var package = File.ReadAllBytes(path);
        if (package.Length is <= 0 or > MaximumPackageBytes)
            return null;
        _ = TaskbookPackageIO.Load(path);
        return package;
    }

    public async Task<TeacherLoginReceipt?> TryLoginAsync(string? password, CancellationToken cancellationToken = default)
    {
        if (_accounts is not null)
            return _accounts.LoginRoot(password);
        if (string.IsNullOrEmpty(password) || password.Length > 256)
            return null;

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(password),
            Encoding.UTF8.GetBytes(DefaultAccessPassword));
        if (!matches)
            return null;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            PurgeExpiredAccessTokens();
            var accessToken = Base64Url(RandomNumberGenerator.GetBytes(32));
            var now = DateTimeOffset.UtcNow;
            _accessTokens[Hash(accessToken)] = new AccessTokenEntry(now + AccessTokenLifetime);
            return new TeacherLoginReceipt(accessToken, now + AccessTokenLifetime);
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool IsAccessValid(string? accessToken)
    {
        if (_accounts is not null)
            return _accounts.Authenticate(accessToken) is not null;
        if (string.IsNullOrEmpty(accessToken) || accessToken.Length != 43 ||
            accessToken.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
            return false;

        _gate.Wait();
        try
        {
            PurgeExpiredAccessTokens();
            return _accessTokens.ContainsKey(Hash(accessToken));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<TeacherSessionDto>> ListSessionsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RequireAccess(accessToken);
            await LoadSessionsAsync(cancellationToken);
            await PurgeExpiredSessionsAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            // Личные файлы кабинета в списке настольного PVZT не показываем:
            // ими управляет только владелец через /api/account/files.
            return _sessions.Values
                .Where(session => session.OwnerUserId is null && session.ExpiresAt > now)
                .OrderByDescending(session => session.UploadedAt)
                .Select(ToDto)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Список только своих задачников — для учителя в панели /admin.</summary>
    public async Task<IReadOnlyList<TeacherSessionDto>> ListOwnedSessionsAsync(long userId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            await PurgeExpiredSessionsAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            return _sessions.Values
                .Where(session => session.OwnerUserId == userId && session.ExpiresAt > now)
                .OrderByDescending(session => session.UploadedAt)
                .Select(ToDto)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Все активные задачники всех учителей — только для администратора.</summary>
    public async Task<IReadOnlyList<TeacherSessionDto>> ListAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            await PurgeExpiredSessionsAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            return _sessions.Values
                .Where(session => session.ExpiresAt > now)
                .OrderByDescending(session => session.UploadedAt)
                .Select(ToDto)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TeacherSessionUploadReceipt> UploadAsync(string accessToken, byte[] package, StoragePlan plan = StoragePlan.FreeTwoWeeks, CancellationToken cancellationToken = default) =>
        await StorePackageAsync(
            package,
            StoragePlans.ToRetention(plan),
            ownerUserId: null,
            legacyPlan: plan,
            principal: null,
            enforceQuota: false,
            accessToken,
            cancellationToken);

    public async Task<TeacherSessionUploadReceipt> UploadAsync(AccountPrincipal principal, byte[] package, RetentionPeriod retention, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return await StorePackageAsync(
            package,
            retention,
            ownerUserId: principal.UserId,
            legacyPlan: null,
            principal,
            enforceQuota: true,
            accessToken: null,
            cancellationToken);
    }

    /// <summary>Загрузка настольного PVZT: файл принадлежит аккаунту учителя, квота хранения кабинета не проверяется.</summary>
    public async Task<TeacherSessionUploadReceipt> UploadAsync(AccountPrincipal principal, byte[] package, StoragePlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return await StorePackageAsync(
            package,
            StoragePlans.ToRetention(plan),
            ownerUserId: principal.UserId,
            legacyPlan: plan,
            principal,
            enforceQuota: false,
            accessToken: null,
            cancellationToken);
    }

    private async Task<TeacherSessionUploadReceipt> StorePackageAsync(
        byte[] package,
        RetentionPeriod retention,
        long? ownerUserId,
        StoragePlan? legacyPlan,
        AccountPrincipal? principal,
        bool enforceQuota,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.Length == 0 || package.Length > MaximumPackageBytes)
            throw new InvalidDataException("Размер .pvz файла недопустим.");

        var temporaryPath = Path.Combine(_sessionsRoot, $".upload-{Guid.NewGuid():N}.pvz");
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
                if (principal is not null)
                {
                    if (_accounts is null)
                        throw new ServerUnauthorizedException("Кабинет недоступен: сервер запущен без базы аккаунтов.");
                    await LoadSessionsAsync(cancellationToken);
                    await PurgeExpiredSessionsAsync(cancellationToken);
                    if (enforceQuota)
                        _accounts.EnsureCanStore(principal, retention, CountUsage(principal.UserId));
                }
                else
                {
                    RequireAccess(accessToken);
                    await LoadSessionsAsync(cancellationToken);
                    await PurgeExpiredSessionsAsync(cancellationToken);
                }

                var token = GenerateShortCode(6);
                var tokenHash = Hash(token);
                while (_sessions.Values.Any(session => FixedTimeEqualsHex(session.TokenHash, tokenHash)))
                {
                    token = GenerateShortCode(6);
                    tokenHash = Hash(token);
                }

                var sessionId = Guid.NewGuid().ToString("N");
                var sessionDirectory = Path.Combine(_sessionsRoot, sessionId);
                Directory.CreateDirectory(sessionDirectory);
                var packagePath = Path.Combine(sessionDirectory, "taskbook.pvz");
                File.Move(temporaryPath, packagePath);

                var now = DateTimeOffset.UtcNow;
                var effectiveLifetime = ClampLifetime(RetentionPeriods.Lifetime(retention));
                var session = new SessionFile
                {
                    Id = sessionId,
                    TokenHash = tokenHash,
                    TokenHint = $"{token[..2]}****",
                    UploadedAt = now,
                    ExpiresAt = now + effectiveLifetime,
                    TotalTasks = document.Tasks.Count,
                    MaximumPoints = TaskbookScoring.GetDocumentMaxScore(document),
                    Plan = legacyPlan ?? StoragePlan.FreeTwoWeeks,
                    Retention = retention,
                    OwnerUserId = ownerUserId,
                };
                try
                {
                    await SaveSessionAsync(session, cancellationToken);
                }
                catch
                {
                    Directory.Delete(sessionDirectory, recursive: true);
                    throw;
                }

                _sessions[sessionId] = session;
                return new TeacherSessionUploadReceipt(sessionId, token, session.ExpiresAt, retention);
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

    public async Task<DateTimeOffset?> ExtendSessionAsync(string accessToken, string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RequireAccess(accessToken);
            await LoadSessionsAsync(cancellationToken);
            var session = FindActiveSession(id);
            if (session is null)
                return null;

            session.ExpiresAt = DateTimeOffset.UtcNow + ClampLifetime(RetentionPeriods.Lifetime(SessionRetention(session)));
            await SaveSessionAsync(session, cancellationToken);
            return session.ExpiresAt;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Продление своего задачника учителем; null — файла нет или он чужой.</summary>
    public async Task<DateTimeOffset?> ExtendOwnedSessionAsync(long userId, string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            var session = FindActiveSession(id);
            if (session is null || session.OwnerUserId != userId)
                return null;

            session.ExpiresAt = DateTimeOffset.UtcNow + ClampLifetime(RetentionPeriods.Lifetime(SessionRetention(session)));
            await SaveSessionAsync(session, cancellationToken);
            return session.ExpiresAt;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> CloseSessionAsync(string accessToken, string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RequireAccess(accessToken);
            await LoadSessionsAsync(cancellationToken);
            var session = FindActiveSession(id);
            if (session is null)
                return false;

            _sessions.Remove(session.Id);
            DeleteSessionDirectory(session);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> CloseOwnerSessionAsync(long userId, string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            var session = FindActiveSession(id);
            if (session is null || session.OwnerUserId != userId)
                return false;

            _sessions.Remove(session.Id);
            DeleteSessionDirectory(session);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<CabinetFileDto>> ListOwnerFilesAsync(long userId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            await PurgeExpiredSessionsAsync(cancellationToken);

            var now = DateTimeOffset.UtcNow;
            return _sessions.Values
                .Where(session => session.OwnerUserId == userId && session.ExpiresAt > now)
                .OrderByDescending(session => session.UploadedAt)
                .Select(session => new CabinetFileDto(
                    session.Id,
                    session.TokenHint,
                    session.UploadedAt,
                    session.ExpiresAt,
                    SessionRetention(session),
                    session.TotalTasks,
                    session.MaximumPoints,
                    session.Students.Count))
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyDictionary<RetentionPeriod, int>> GetOwnerUsageAsync(long userId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            await PurgeExpiredSessionsAsync(cancellationToken);
            return CountUsage(userId);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Проверка токена для веб-эндпоинтов; null — токен отсутствует или истёк.</summary>
    public AccountPrincipal? AuthenticateAccount(string? accessToken) => _accounts?.Authenticate(accessToken);

    public async Task<byte[]?> DownloadSessionAsync(string accessToken, string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            RequireAccess(accessToken);
            await LoadSessionsAsync(cancellationToken);
            var session = FindActiveSession(id);
            if (session is null)
                return null;

            var packagePath = Path.Combine(Path.Combine(_sessionsRoot, session.Id), "taskbook.pvz");
            return File.Exists(packagePath) ? await File.ReadAllBytesAsync(packagePath, cancellationToken) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<byte[]?> DownloadOwnerFileAsync(long userId, string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            var session = FindActiveSession(id);
            if (session is null || session.OwnerUserId != userId)
                return null;

            var packagePath = Path.Combine(Path.Combine(_sessionsRoot, session.Id), "taskbook.pvz");
            return File.Exists(packagePath) ? await File.ReadAllBytesAsync(packagePath, cancellationToken) : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<byte[]?> DownloadAsync(string token, CancellationToken cancellationToken = default)
    {
        var tokenHash = Hash(NormalizeToken(token));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            var session = FindActiveSessionByTokenHash(tokenHash);
            if (session is null)
                return null;

            var packagePath = Path.Combine(Path.Combine(_sessionsRoot, session.Id), "taskbook.pvz");
            return File.Exists(packagePath) ? await File.ReadAllBytesAsync(packagePath, cancellationToken) : null;
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
            await LoadSessionsAsync(cancellationToken);
            var session = FindActiveSessionByTokenHash(tokenHash);
            if (session is null)
                return null;

            var now = DateTimeOffset.UtcNow;
            var sessionId = Base64Url(RandomNumberGenerator.GetBytes(32));
            session.Students.Add(new StudentRecord
            {
                SessionId = sessionId,
                FirstName = firstName,
                LastName = lastName,
                ClassName = className,
                StartedAt = now,
                LastActivityAt = now,
                EarnedPoints = Enumerable.Repeat(0, session.TotalTasks).ToArray(),
                CompletedTasks = new bool[session.TotalTasks],
            });
            await SaveSessionAsync(session, cancellationToken);
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
            await LoadSessionsAsync(cancellationToken);
            var pair = FindStudent(update.SessionId);
            if (pair is null || pair.Session.ExpiresAt <= DateTimeOffset.UtcNow)
                return false;
            var session = pair.Session;
            var student = pair.Student;
            if (update.Tasks.Count != session.TotalTasks)
                return false;

            var packagePath = Path.Combine(Path.Combine(_sessionsRoot, session.Id), "taskbook.pvz");
            if (!File.Exists(packagePath))
                return false;
            TaskbookDocument document;
            try
            {
                document = TaskbookPackageIO.Load(packagePath);
            }
            catch (JsonException)
            {
                return false;
            }
            if (document.Tasks.Count != update.Tasks.Count)
                return false;

            for (var index = 0; index < update.Tasks.Count; index++)
            {
                var task = update.Tasks[index];
                if (task is null || task.EarnedPoints < 0 ||
                    task.EarnedPoints > TaskbookScoring.GetTaskMaxScore(document.Tasks[index]))
                    return false;
            }

            student.EarnedPoints = update.Tasks.Select(item => item.EarnedPoints).ToArray();
            student.CompletedTasks = update.Tasks.Select(item => item.Completed).ToArray();
            student.LastActivityAt = DateTimeOffset.UtcNow;
            await SaveSessionAsync(session, cancellationToken);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ---- Пересылка файлов: учитель рассылает материалы, ученики присылают работы ----

    /// <summary>Материал от учителя всем ученикам задачника; null — задачника нет или он чужой.</summary>
    public async Task<ExchangeFileDto?> AddSessionFileAsync(
        AccountPrincipal principal,
        string sessionId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        ValidateExchangeContent(content);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            var session = FindActiveSession(sessionId);
            if (session is null || !CanManageExchangeFiles(principal, session))
                return null;
            return await AddExchangeFileAsync(
                session, ExchangeFileKinds.Material, fileName, content, studentSessionId: null, studentName: null, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Все файлы задачника глазами учителя; null — задачника нет или он чужой.</summary>
    public async Task<IReadOnlyList<ExchangeFileDto>?> ListSessionFilesAsync(
        AccountPrincipal principal,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            var session = FindActiveSession(sessionId);
            if (session is null || !CanManageExchangeFiles(principal, session))
                return null;
            return ToExchangeDtos(session.Files);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Скачивание файла задачника учителем (работа ученика или свой материал).</summary>
    public async Task<(ExchangeFileDto Info, byte[] Content)?> OpenSessionFileAsync(
        AccountPrincipal principal,
        string sessionId,
        string fileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            var session = FindActiveSession(sessionId);
            if (session is null || !CanManageExchangeFiles(principal, session))
                return null;
            return await OpenExchangeFileAsync(session, fileId, allowStudentWorks: true, studentSessionId: null, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteSessionFileAsync(
        AccountPrincipal principal,
        string sessionId,
        string fileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            var session = FindActiveSession(sessionId);
            if (session is null || !CanManageExchangeFiles(principal, session))
                return false;
            return await DeleteExchangeFileAsync(session, fileId, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Работа ученика; null — сессии ученика нет, задачник истёк или переполнен лимит файлов.</summary>
    public async Task<ExchangeFileDto?> AddStudentFileAsync(
        string studentSessionId,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken = default)
    {
        ValidateExchangeContent(content);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            var pair = FindStudent(studentSessionId);
            if (pair is null || pair.Session.ExpiresAt <= DateTimeOffset.UtcNow)
                return null;
            var student = pair.Student;
            var studentName = $"{student.FirstName} {student.LastName} ({student.ClassName})";
            return await AddExchangeFileAsync(
                pair.Session, ExchangeFileKinds.Work, fileName, content, studentSessionId, studentName, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Материалы задачника и только свои работы — для ученика; null — сессии ученика нет.</summary>
    public async Task<StudentExchangeFilesDto?> ListStudentFilesAsync(
        string studentSessionId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            var pair = FindStudent(studentSessionId);
            if (pair is null || pair.Session.ExpiresAt <= DateTimeOffset.UtcNow)
                return null;

            var materials = pair.Session.Files
                .Where(record => record.Kind == ExchangeFileKinds.Material);
            var works = pair.Session.Files
                .Where(record => record.Kind == ExchangeFileKinds.Work &&
                    string.Equals(record.StudentSessionId, studentSessionId, StringComparison.Ordinal));
            return new StudentExchangeFilesDto(ToExchangeDtos(materials), ToExchangeDtos(works));
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Ученик скачивает материал или свою работу; чужая работа недоступна.</summary>
    public async Task<(ExchangeFileDto Info, byte[] Content)?> OpenStudentFileAsync(
        string studentSessionId,
        string fileId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadSessionsAsync(cancellationToken);
            var pair = FindStudent(studentSessionId);
            if (pair is null || pair.Session.ExpiresAt <= DateTimeOffset.UtcNow)
                return null;
            return await OpenExchangeFileAsync(
                pair.Session, fileId, allowStudentWorks: false, studentSessionId, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PurgeExpiredAsync(CancellationToken cancellationToken = default)
    {
        _accounts?.PurgeExpiredTokens();
        await _gate.WaitAsync(cancellationToken);
        try
        {
            PurgeExpiredAccessTokens();
            await LoadSessionsAsync(cancellationToken);
            await PurgeExpiredSessionsAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private TeacherSessionDto ToDto(SessionFile session) =>
        new(
            session.Id,
            session.TokenHint,
            session.UploadedAt,
            session.ExpiresAt,
            session.TotalTasks,
            session.MaximumPoints,
            SessionRetention(session),
            session.Students
                .OrderByDescending(student => student.LastActivityAt)
                .Select(student =>
                {
                    var lastActivity = student.LastActivityAt;
                    return new SessionStudentDto(
                        student.FirstName,
                        student.LastName,
                        student.ClassName,
                        student.StartedAt,
                        lastActivity,
                        (long)Math.Max(0, (lastActivity - student.StartedAt).TotalSeconds),
                        student.EarnedPoints.Sum(),
                        session.MaximumPoints,
                        student.CompletedTasks.Count(completed => completed),
                        session.TotalTasks,
                        DateTimeOffset.UtcNow - lastActivity <= TimeSpan.FromSeconds(30),
                        student.EarnedPoints
                            .Select((earnedPoints, index) =>
                                new StudentTaskProgressDto(earnedPoints, student.CompletedTasks[index]))
                            .ToArray());
                })
                .ToArray(),
            session.OwnerUserId);

    private SessionDtoPair? FindStudent(string studentSessionId)
    {
        foreach (var session in _sessions.Values)
        {
            var student = session.Students.FirstOrDefault(item =>
                CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(item.SessionId),
                    Encoding.UTF8.GetBytes(studentSessionId)));
            if (student is not null)
                return new SessionDtoPair(session, student);
        }

        return null;
    }

    private SessionFile? FindActiveSession(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || !SessionId.IsValid(id))
            return null;
        return _sessions.TryGetValue(id, out var session) && session.ExpiresAt > DateTimeOffset.UtcNow
            ? session
            : null;
    }

    private SessionFile? FindActiveSessionByTokenHash(string tokenHash)
    {
        var session = _sessions.Values.FirstOrDefault(item => FixedTimeEqualsHex(item.TokenHash, tokenHash));
        return session is not null && session.ExpiresAt > DateTimeOffset.UtcNow ? session : null;
    }

    /// <summary>Считается под захваченным _gate: сколько активных задачников владельца в каждой корзине хранения.</summary>
    private Dictionary<RetentionPeriod, int> CountUsage(long userId)
    {
        var usage = new Dictionary<RetentionPeriod, int>();
        var now = DateTimeOffset.UtcNow;
        foreach (var session in _sessions.Values)
        {
            if (session.OwnerUserId != userId || session.ExpiresAt <= now)
                continue;
            var retention = SessionRetention(session);
            usage[retention] = usage.TryGetValue(retention, out var count) ? count + 1 : 1;
        }

        return usage;
    }

    /// <summary>Писать файлы задачника может владелец, администратор и вход по общему паролю (root).</summary>
    private static bool CanManageExchangeFiles(AccountPrincipal principal, SessionFile session) =>
        principal.IsAdmin ||
        string.Equals(principal.Kind, "root", StringComparison.Ordinal) ||
        session.OwnerUserId == principal.UserId;

    private static void ValidateExchangeContent(byte[] content)
    {
        if (content is null || content.Length == 0 || content.Length > MaximumExchangeFileBytes)
            throw new InvalidDataException("Размер файла должен быть от 1 байта до 32 МиБ.");
    }

    private async Task<ExchangeFileDto> AddExchangeFileAsync(
        SessionFile session,
        string kind,
        string fileName,
        byte[] content,
        string? studentSessionId,
        string? studentName,
        CancellationToken cancellationToken)
    {
        if (session.Files.Sum(record => record.SizeBytes) + content.Length > MaximumSessionExchangeBytes)
            throw new InvalidDataException("Лимит файлов задачника — 512 МиБ. Удалите лишние файлы и повторите.");

        var fileId = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(_sessionsRoot, session.Id, "files");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileId);
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        try
        {
            var record = new ExchangeFileRecord
            {
                Id = fileId,
                Name = SanitizeFileName(fileName),
                Kind = kind,
                StudentSessionId = studentSessionId,
                StudentName = studentName,
                SizeBytes = content.Length,
                UploadedAt = DateTimeOffset.UtcNow,
            };
            session.Files.Add(record);
            try
            {
                await SaveSessionAsync(session, cancellationToken);
            }
            catch
            {
                session.Files.Remove(record);
                throw;
            }

            return ToExchangeDto(record);
        }
        catch
        {
            if (File.Exists(path))
                File.Delete(path);
            throw;
        }
    }

    private async Task<(ExchangeFileDto Info, byte[] Content)?> OpenExchangeFileAsync(
        SessionFile session,
        string fileId,
        bool allowStudentWorks,
        string? studentSessionId,
        CancellationToken cancellationToken)
    {
        if (!SessionId.IsValid(fileId))
            return null;
        var record = session.Files.FirstOrDefault(item => string.Equals(item.Id, fileId, StringComparison.Ordinal));
        if (record is null)
            return null;
        if (!allowStudentWorks && record.Kind == ExchangeFileKinds.Work &&
            !string.Equals(record.StudentSessionId, studentSessionId, StringComparison.Ordinal))
            return null;

        var path = Path.Combine(_sessionsRoot, session.Id, "files", fileId);
        if (!File.Exists(path))
            return null;
        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        return content.Length == record.SizeBytes ? (ToExchangeDto(record), content) : null;
    }

    private async Task<bool> DeleteExchangeFileAsync(SessionFile session, string fileId, CancellationToken cancellationToken)
    {
        if (!SessionId.IsValid(fileId))
            return false;
        var record = session.Files.FirstOrDefault(item => string.Equals(item.Id, fileId, StringComparison.Ordinal));
        if (record is null)
            return false;

        session.Files.Remove(record);
        await SaveSessionAsync(session, cancellationToken);
        var path = Path.Combine(_sessionsRoot, session.Id, "files", fileId);
        if (File.Exists(path))
            File.Delete(path);
        return true;
    }

    private static IReadOnlyList<ExchangeFileDto> ToExchangeDtos(IEnumerable<ExchangeFileRecord> records) =>
        records.OrderByDescending(record => record.UploadedAt).Select(ToExchangeDto).ToArray();

    private static ExchangeFileDto ToExchangeDto(ExchangeFileRecord record) =>
        new(record.Id, record.Name, record.Kind, record.SizeBytes, record.StudentName, record.UploadedAt);

    private static string SanitizeFileName(string? rawName)
    {
        var name = Path.GetFileName((rawName ?? "").Trim());
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name
            .Where(character => character >= ' ' && !invalid.Contains(character))
            .ToArray()).Trim().TrimEnd('.');
        if (cleaned.Length == 0)
            cleaned = "file";
        if (cleaned[0] == '.')
            cleaned = "_" + cleaned[1..];
        return cleaned.Length > 120 ? cleaned[..120] : cleaned;
    }

    private static RetentionPeriod SessionRetention(SessionFile session) =>
        session.Retention ?? StoragePlans.ToRetention(session.Plan);

    private void RequireAccess(string? accessToken)
    {
        if (_accounts is not null)
        {
            if (_accounts.Authenticate(accessToken) is null)
                throw new ServerUnauthorizedException("Сессия доступа истекла. Войдите снова.");
            return;
        }

        if (string.IsNullOrEmpty(accessToken) || accessToken.Length != 43 ||
            accessToken.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')) ||
            !_accessTokens.TryGetValue(Hash(accessToken), out var entry) ||
            entry.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ServerUnauthorizedException("Сессия доступа истекла. Войдите снова.");
        PurgeExpiredAccessTokens();
    }

    private void PurgeExpiredAccessTokens()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (hash, entry) in _accessTokens.Where(item => item.Value.ExpiresAt <= now).ToArray())
            _accessTokens.Remove(hash);
    }

    private async Task LoadSessionsAsync(CancellationToken cancellationToken)
    {
        if (_sessionsLoaded)
            return;

        foreach (var directory in Directory.EnumerateDirectories(_sessionsRoot))
        {
            var id = Path.GetFileName(directory);
            if (!SessionId.IsValid(id))
                continue;
            var sessionPath = Path.Combine(directory, "session.json");
            if (!File.Exists(sessionPath))
                continue;
            try
            {
                await using var input = File.OpenRead(sessionPath);
                var session = await JsonSerializer.DeserializeAsync<SessionFile>(input, JsonOptions, cancellationToken)
                    ?? throw new InvalidDataException("Пустой файл сессии.");
                if (SessionId.IsValid(session.Id))
                    _sessions[session.Id] = session;
            }
            catch (Exception exception) when (exception is IOException or JsonException)
            {
                // повреждённая сессия не должна валить сервер
            }
        }

        _sessionsLoaded = true;
    }

    private async Task PurgeExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var session in _sessions.Values.Where(item => item.ExpiresAt <= now).ToArray())
        {
            _sessions.Remove(session.Id);
            DeleteSessionDirectory(session);
        }
        await Task.CompletedTask;
    }

    private void DeleteSessionDirectory(SessionFile session)
    {
        var directory = Path.Combine(_sessionsRoot, session.Id);
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private async Task SaveSessionAsync(SessionFile session, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_sessionsRoot, session.Id);
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".session-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = File.Create(temporaryPath))
                await JsonSerializer.SerializeAsync(output, session, JsonOptions, cancellationToken);
            File.Move(temporaryPath, Path.Combine(directory, "session.json"), overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private string GetCatalogClassDirectory(string subject, int classNumber)
    {
        var normalizedSubject = TaskbookCatalogSubjects.All
            .FirstOrDefault(item => string.Equals(item.Id, subject, StringComparison.OrdinalIgnoreCase))?.Id;
        if (normalizedSubject is null)
            throw new InvalidDataException("Неизвестный предмет каталога.");
        if (!TaskbookCatalogSubjects.IsValidClass(normalizedSubject, classNumber))
            throw new InvalidDataException("Для выбранного предмета этот класс недоступен.");

        return Path.Combine(_catalogDirectory, normalizedSubject, classNumber.ToString());
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

    private TimeSpan ClampLifetime(TimeSpan lifetime) =>
        _sessionLifetime < DefaultSessionLifetime ? _sessionLifetime : lifetime;

    private static bool FixedTimeEqualsHex(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed record AccessTokenEntry(DateTimeOffset ExpiresAt);

    private sealed record SessionDtoPair(SessionFile Session, StudentRecord Student);

    private static class SessionId
    {
        public static bool IsValid(string? value) =>
            value is { Length: 32 } && value.All(char.IsAsciiHexDigit);
    }

    private sealed class SessionFile
    {
        public string Id { get; set; } = "";
        public string TokenHash { get; set; } = "";
        public string TokenHint { get; set; } = "";
        public DateTimeOffset UploadedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        public int TotalTasks { get; set; }
        public int MaximumPoints { get; set; }

        // Plan — исторический тариф PVZT; Retention — новый срок хранения.
        // Старые session.json без Retention читаются через StoragePlans.ToRetention(Plan).
        public StoragePlan Plan { get; set; } = StoragePlan.FreeTwoWeeks;
        public RetentionPeriod? Retention { get; set; }

        /// <summary>Владелец из кабинета; null — загрузка настольного PVZT по паролю.</summary>
        public long? OwnerUserId { get; set; }

        public List<StudentRecord> Students { get; set; } = new();

        /// <summary>Файлы пересылки: материалы от учителя и работы учеников (папка files/).</summary>
        public List<ExchangeFileRecord> Files { get; set; } = new();
    }

    private sealed class ExchangeFileRecord
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Kind { get; set; } = ExchangeFileKinds.Material;

        /// <summary>Сессия ученика-автора; у материалов null.</summary>
        public string? StudentSessionId { get; set; }
        public string? StudentName { get; set; }
        public long SizeBytes { get; set; }
        public DateTimeOffset UploadedAt { get; set; }
    }

    private sealed class StudentRecord
    {
        public string SessionId { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset LastActivityAt { get; set; }
        public int[] EarnedPoints { get; set; } = [];
        public bool[] CompletedTasks { get; set; } = [];
    }
}
