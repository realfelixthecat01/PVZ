using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PVZ.Core;

namespace PVZS;

/// <summary>
/// Аккаунты учителей поверх встроенной SQLite: пароль-логин, веб-регистрация,
/// токены, подписки и ключи активации. Корневая учётка — старый пароль доступа,
/// поэтому настольный PVZT и страница /admin продолжают работать без изменений.
/// </summary>
internal sealed class AccountService
{
    public const string RootEmail = "admin@pvz.local";
    public const string RootUsername = "admin";
    public const string RootFullName = "Администратор";
    public static readonly TimeSpan WebTokenLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan RootTokenLifetime = TaskbookStore.AccessTokenLifetime;
    private const string KeyAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int KeyBodyLength = 12;
    private const int MaximumKeyAttempts = 20;

    private readonly PvzDatabase _database;
    private readonly (string Hash, string Salt) _dummyPassword;

    public AccountService(PvzDatabase database)
    {
        _database = database;
        _dummyPassword = PvzDatabase.HashPassword(Guid.NewGuid().ToString("N"));
        EnsureRootAccount();
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static bool IsValidEmail(string email) =>
        email.Length is >= 5 and <= 190 &&
        email.Count(character => character == '@') == 1 &&
        !email.Any(char.IsWhiteSpace) &&
        email.IndexOf('@') is var at && at > 0 && at < email.Length - 3 &&
        email[(at + 1)..].Contains('.', StringComparison.Ordinal);

    /// <summary>Имя аккаунта: логин для входа вместо email. Латинские буквы, цифры, . _ -</summary>
    public static string NormalizeUsername(string? username)
    {
        var normalized = (username ?? "").Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 32 ||
            normalized.Any(character => character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-')))
            throw new InvalidDataException("Имя аккаунта — от 3 до 32 символов: латинские буквы, цифры, точка, дефис или подчёркивание.");
        return normalized;
    }

    public TeacherLoginReceipt? LoginRoot(string? password)
    {
        var root = _database.FindUserByEmail(RootEmail);
        if (root is null || string.IsNullOrEmpty(password) || password.Length > 256)
            return null;
        if (!PvzDatabase.VerifyPassword(password, root.PasswordHash, root.PasswordSalt))
            return null;
        return IssueToken(root.Id, "root", RootTokenLifetime);
    }

    /// <summary>Вход по email или по имени аккаунта — что учитель ввёл, то и ищем.</summary>
    public AccountAuthResult? Login(string? identifier, string? password)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrEmpty(password) || password.Length > 256)
            return null;
        var trimmed = identifier.Trim();
        var user = trimmed.Contains('@', StringComparison.Ordinal)
            ? _database.FindUserByEmail(NormalizeEmail(trimmed))
            : _database.FindUserByUsername(trimmed);
        if (user is null)
        {
            // Сверка с фиктивным хэшем выравнивает время ответа: иначе по задержке
            // можно было бы понять, какие адреса зарегистрированы.
            _ = PvzDatabase.VerifyPassword(password, _dummyPassword.Hash, _dummyPassword.Salt);
            return null;
        }

        if (!PvzDatabase.VerifyPassword(password, user.PasswordHash, user.PasswordSalt))
            return null;
        var receipt = IssueToken(user.Id, "web", WebTokenLifetime);
        return new AccountAuthResult(receipt, ToPrincipal(user, "web"));
    }

    public AccountAuthResult Register(string? username, string? fullName, string? school, string? email, string? password)
    {
        var normalizedUsername = NormalizeUsername(username);
        var normalizedName = RequireText(fullName, 120, "Введите ФИО.");
        var normalizedSchool = RequireText(school, 160, "Введите школу или гимназию.");
        var normalizedEmail = NormalizeEmail(RequireText(email, 190, "Введите email."));
        if (!IsValidEmail(normalizedEmail))
            throw new InvalidDataException("Укажите корректный email, например teacher@school.by.");
        if (string.IsNullOrEmpty(password) || password.Length is < 6 or > 128)
            throw new InvalidDataException("Пароль должен быть от 6 до 128 символов.");
        if (_database.FindUserByUsername(normalizedUsername) is not null)
            throw new InvalidDataException("Это имя аккаунта уже занято. Выберите другое.");

        var (hash, salt) = PvzDatabase.HashPassword(password);
        long userId;
        try
        {
            userId = _database.CreateUser(
                normalizedUsername,
                normalizedEmail,
                hash,
                salt,
                normalizedName,
                normalizedSchool,
                isAdmin: false,
                StorageLimits.Free,
                storageUntil: null,
                DateTimeOffset.UtcNow);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            throw new InvalidDataException("Аккаунт с таким email или именем уже зарегистрирован. Войдите в него.", exception);
        }

        var user = _database.FindUserById(userId)!;
        var receipt = IssueToken(user.Id, "web", WebTokenLifetime);
        return new AccountAuthResult(receipt, ToPrincipal(user, "web"));
    }

    public AccountPrincipal? Authenticate(string? token)
    {
        if (string.IsNullOrEmpty(token) || token.Length != 43 ||
            token.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
            return null;
        var row = _database.FindToken(HashToken(token), DateTimeOffset.UtcNow);
        if (row is null)
            return null;
        var user = _database.FindUserById(row.UserId);
        return user is null ? null : ToPrincipal(user, row.Kind);
    }

    public void Logout(string token) => _database.DeleteToken(HashToken(token));

    public void ChangePassword(AccountPrincipal principal, string? currentPassword, string? newPassword)
    {
        var user = _database.FindUserById(principal.UserId)
            ?? throw new ServerUnauthorizedException("Аккаунт не найден. Войдите снова.");
        if (string.IsNullOrEmpty(currentPassword) ||
            !PvzDatabase.VerifyPassword(currentPassword, user.PasswordHash, user.PasswordSalt))
            throw new InvalidDataException("Текущий пароль указан неверно.");
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length is < 6 or > 128)
            throw new InvalidDataException("Новый пароль должен быть от 6 до 128 символов.");
        var (hash, salt) = PvzDatabase.HashPassword(newPassword);
        _database.UpdateUserPassword(user.Id, hash, salt);
    }

    public AccountProfileDto GetProfile(AccountPrincipal principal, IReadOnlyDictionary<RetentionPeriod, int> usage)
    {
        var user = _database.FindUserById(principal.UserId)
            ?? throw new ServerUnauthorizedException("Аккаунт не найден. Войдите снова.");
        var now = DateTimeOffset.UtcNow;
        var plan = EffectivePlan(user, now);
        DateTimeOffset? planUntil = user.IsAdmin || user.StorageUntil is not { } until || until <= now ? null : until;
        var subscriptions = _database.ListSubscriptions(user.Id, now)
            .Select(row => new AccountSubscriptionDto(row.Kind, row.Subject, row.ClassNumber, row.ExpiresAt))
            .ToArray();
        var storage = StorageLimits.RetentionsFor(plan)
            .Select(retention => new StorageBucketDto(
                retention,
                usage.TryGetValue(retention, out var used) ? used : 0,
                user.IsAdmin ? StorageLimits.Unlimited : StorageLimits.LimitFor(plan, retention)))
            .ToArray();

        return new AccountProfileDto(
            user.Id,
            user.Username,
            user.Email,
            user.FullName,
            user.School,
            user.IsAdmin,
            plan,
            StorageLimits.PlanDisplayName(plan),
            planUntil,
            subscriptions,
            storage);
    }

    public void UpdateProfile(AccountPrincipal principal, string? fullName, string? school)
    {
        var normalizedName = RequireText(fullName, 120, "Введите ФИО.");
        var normalizedSchool = RequireText(school, 160, "Введите школу или гимназию.");
        _database.UpdateUserProfile(principal.UserId, normalizedName, normalizedSchool);
    }

    public string ActivateKey(AccountPrincipal principal, string? code)
    {
        var normalized = NormalizeKeyCode(code);
        var key = _database.FindKey(normalized)
            ?? throw new InvalidDataException("Ключ активации не найден. Проверьте код.");
        if (key.UsedBy is not null)
            throw new InvalidDataException("Этот ключ уже был активирован.");
        var now = DateTimeOffset.UtcNow;
        if (!_database.MarkKeyUsed(normalized, principal.UserId, now))
            throw new InvalidDataException("Этот ключ уже был активирован.");
        return ApplyKey(principal, key, now);
    }

    public bool HasCatalogAccess(AccountPrincipal principal, string subject, int classNumber)
    {
        var user = _database.FindUserById(principal.UserId);
        if (user is null)
            return false;
        if (user.IsAdmin)
            return true;
        var now = DateTimeOffset.UtcNow;
        foreach (var subscription in _database.ListSubscriptions(user.Id, now))
        {
            if (string.Equals(subscription.Kind, "all", StringComparison.Ordinal))
                return true;
            if (!string.Equals(subscription.Subject, subject, StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(subscription.Kind, "subject", StringComparison.Ordinal))
                return true;
            if (string.Equals(subscription.Kind, "class", StringComparison.Ordinal) &&
                subscription.ClassNumber == classNumber)
                return true;
        }

        return false;
    }

    public void EnsureCanStore(AccountPrincipal principal, RetentionPeriod retention, IReadOnlyDictionary<RetentionPeriod, int> usage)
    {
        var user = _database.FindUserById(principal.UserId)
            ?? throw new ServerUnauthorizedException("Аккаунт не найден. Войдите снова.");
        if (user.IsAdmin)
            return;
        var plan = EffectivePlan(user, DateTimeOffset.UtcNow);
        var limit = StorageLimits.LimitFor(plan, retention);
        if (limit == 0)
            throw new ServerQuotaException(
                $"Срок хранения «{RetentionPeriods.DisplayName(retention)}» недоступен на тарифе «{StorageLimits.PlanDisplayName(plan)}». " +
                "Активируйте подходящий ключ в личном кабинете.");
        if (limit == StorageLimits.Unlimited)
            return;
        var used = usage.TryGetValue(retention, out var count) ? count : 0;
        if (used >= limit)
            throw new ServerQuotaException(
                $"На тарифе «{StorageLimits.PlanDisplayName(plan)}» можно хранить {limit} задачников со сроком «{RetentionPeriods.ShortLabel(retention)}». " +
                "Удалите ненужный задачник или активируйте тариф выше.");
    }

    public ActivationKeyDto CreateKey(
        AccountPrincipal principal,
        string? kind,
        string? storagePlan,
        int durationDays,
        string? subject,
        int? classNumber)
    {
        var normalizedKind = (kind ?? "").Trim().ToLowerInvariant();
        if (normalizedKind is not ("storage" or "subject" or "class" or "all"))
            throw new InvalidDataException("Тип ключа должен быть storage, subject, class или all.");
        if (durationDays is < 1 or > 3650)
            throw new InvalidDataException("Срок действия ключа — от 1 до 3650 дней.");

        string? normalizedPlan = null;
        string? normalizedSubject = null;
        int? normalizedClass = null;
        switch (normalizedKind)
        {
            case "storage":
                normalizedPlan = (storagePlan ?? "").Trim().ToLowerInvariant();
                if (normalizedPlan is not (StorageLimits.Pro or StorageLimits.ProPlus))
                    throw new InvalidDataException("Тариф ключа хранения — pro или pro_plus.");
                break;
            case "subject":
                normalizedSubject = NormalizeSubject(subject);
                break;
            case "class":
                normalizedSubject = NormalizeSubject(subject);
                if (classNumber is not { } value || !TaskbookCatalogSubjects.IsValidClass(normalizedSubject, value))
                    throw new InvalidDataException("Для выбранного предмета этот класс недоступен.");
                normalizedClass = classNumber;
                break;
            case "all":
                break;
        }

        var now = DateTimeOffset.UtcNow;
        for (var attempt = 0; attempt < MaximumKeyAttempts; attempt++)
        {
            var code = GenerateKeyCode();
            if (_database.TryCreateKey(code, normalizedKind, normalizedPlan, durationDays, normalizedSubject, normalizedClass, now, principal.UserId))
                return new ActivationKeyDto(code, normalizedKind, normalizedPlan, durationDays, normalizedSubject, normalizedClass, now, false, null);
        }

        throw new InvalidDataException("Не удалось создать уникальный ключ. Попробуйте ещё раз.");
    }

    public IReadOnlyList<ActivationKeyDto> ListKeys() =>
        _database.ListKeys(200)
            .Select(row => new ActivationKeyDto(
                row.Code,
                row.Kind,
                row.StoragePlan,
                row.DurationDays,
                row.Subject,
                row.ClassNumber,
                row.CreatedAt,
                row.UsedBy is not null,
                row.UsedAt))
            .ToArray();

    public IReadOnlyList<PvzDatabase.UserRow> ListUsers() => _database.ListUsers();

    public void PurgeExpiredTokens() => _database.PurgeExpiredTokens(DateTimeOffset.UtcNow);

    public static string NormalizeKeyCode(string? code)
    {
        var raw = (code ?? "").Trim().ToUpperInvariant().Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
        if (raw.StartsWith("PVZ", StringComparison.Ordinal))
            raw = raw[3..];
        if (raw.Length != KeyBodyLength || raw.Any(character => !KeyAlphabet.Contains(character)))
            throw new InvalidDataException("Ключ активации выглядит как PVZ-XXXX-XXXX-XXXX.");
        return $"PVZ-{raw[..4]}-{raw[4..8]}-{raw[8..]}";
    }

    public static string EffectivePlan(PvzDatabase.UserRow user, DateTimeOffset now) =>
        user.IsAdmin
            ? StorageLimits.ProPlus
            : user.StorageUntil is { } until && until > now && user.StoragePlan is StorageLimits.Pro or StorageLimits.ProPlus
                ? user.StoragePlan
                : StorageLimits.Free;

    private string ApplyKey(AccountPrincipal principal, PvzDatabase.KeyRow key, DateTimeOffset now)
    {
        var duration = TimeSpan.FromDays(key.DurationDays);
        switch (key.Kind)
        {
            case "storage":
            {
                var user = _database.FindUserById(principal.UserId)!;
                var target = key.StoragePlan ?? StorageLimits.Pro;
                var newUntil = user.StoragePlan == target && user.StorageUntil is { } existing && existing > now
                    ? existing + duration
                    : now + duration;
                _database.UpdateUserPlan(user.Id, target, newUntil);
                return $"Тариф «{StorageLimits.PlanDisplayName(target)}» активирован до {newUntil:dd.MM.yyyy}.";
            }
            case "subject":
            {
                var subject = key.Subject ?? TaskbookCatalogSubjects.Informatics;
                var until = now + duration;
                _database.InsertSubscription(principal.UserId, "subject", subject, null, now, until);
                return $"Подписка на предмет «{DisplaySubject(subject)}» (все классы) активирована до {until:dd.MM.yyyy}.";
            }
            case "all":
            {
                var until = now + duration;
                _database.InsertSubscription(principal.UserId, "all", "", null, now, until);
                return $"Подписка на все предметы активирована до {until:dd.MM.yyyy}.";
            }
            default:
            {
                var subject = key.Subject ?? TaskbookCatalogSubjects.Informatics;
                var classNumber = key.ClassNumber ?? TaskbookCatalogSubjects.MinimumClassNumber(subject);
                var until = now + duration;
                _database.InsertSubscription(principal.UserId, "class", subject, classNumber, now, until);
                return $"Подписка на «{DisplaySubject(subject)}», {classNumber} класс активирована до {until:dd.MM.yyyy}.";
            }
        }
    }

    private void EnsureRootAccount()
    {
        if (_database.FindUserByEmail(RootEmail) is not null)
            return;
        var (hash, salt) = PvzDatabase.HashPassword(TaskbookStore.DefaultAccessPassword);
        try
        {
            _database.CreateUser(
                RootUsername,
                RootEmail,
                hash,
                salt,
                RootFullName,
                "",
                isAdmin: true,
                StorageLimits.Free,
                storageUntil: null,
                DateTimeOffset.UtcNow);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19)
        {
            // Учётку создал параллельный запуск — она уже есть.
        }
    }

    private TeacherLoginReceipt IssueToken(long userId, string kind, TimeSpan lifetime)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now + lifetime;
        _database.InsertToken(HashToken(token), userId, kind, now, expiresAt);
        return new TeacherLoginReceipt(token, expiresAt);
    }

    private static AccountPrincipal ToPrincipal(PvzDatabase.UserRow user, string kind) =>
        new(user.Id, user.Username, user.Email, user.FullName, user.IsAdmin, kind);

    private static string RequireText(string? value, int maximumLength, string error)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
            throw new InvalidDataException(error);
        return normalized;
    }

    private static string NormalizeSubject(string? subject)
    {
        var normalized = TaskbookCatalogSubjects.All
            .FirstOrDefault(item => string.Equals(item.Id, subject, StringComparison.OrdinalIgnoreCase))?.Id;
        return normalized ?? throw new InvalidDataException("Неизвестный предмет.");
    }

    private static string DisplaySubject(string subject) =>
        TaskbookCatalogSubjects.All.FirstOrDefault(item => item.Id == subject)?.DisplayName ?? subject;

    private static string GenerateKeyCode()
    {
        var random = RandomNumberGenerator.GetBytes(KeyBodyLength);
        var body = string.Create(KeyBodyLength, random, (chars, bytes) =>
        {
            for (var index = 0; index < chars.Length; index++)
                chars[index] = KeyAlphabet[bytes[index] & 31];
        });
        return $"PVZ-{body[..4]}-{body[4..8]}-{body[8..]}";
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

internal sealed record AccountAuthResult(TeacherLoginReceipt Receipt, AccountPrincipal Principal);
