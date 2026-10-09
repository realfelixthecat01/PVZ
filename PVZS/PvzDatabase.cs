using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace PVZS;

/// <summary>
/// Встроенная база SQLite рядом с .exe: аккаунты, токены, подписки и ключи активации.
/// Задачники остаются папками на диске — база хранит только учётные данные.
/// </summary>
internal sealed class PvzDatabase : IDisposable
{
    private const int Pbkdf2Iterations = 100_000;
    private const int Pbkdf2Bytes = 32;
    private const int SaltBytes = 16;

    private readonly object _gate = new();
    private readonly SqliteConnection _connection;

    public PvzDatabase(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        }.ToString();
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        Execute("PRAGMA journal_mode=WAL;");
        Execute("PRAGMA busy_timeout=5000;");
        CreateSchema();
        EnsureUsernames();
    }

    public static (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, Pbkdf2Bytes);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool VerifyPassword(string password, string passwordHash, string passwordSalt)
    {
        try
        {
            var salt = Convert.FromBase64String(passwordSalt);
            var expected = Convert.FromBase64String(passwordHash);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public long CreateUser(
        string username,
        string email,
        string passwordHash,
        string passwordSalt,
        string fullName,
        string school,
        bool isAdmin,
        string storagePlan,
        DateTimeOffset? storageUntil,
        DateTimeOffset createdAt)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO users (username, email, password_hash, password_salt, full_name, school, is_admin, storage_plan, storage_until, created_at)
                VALUES ($username, $email, $hash, $salt, $fullName, $school, $isAdmin, $plan, $until, $createdAt);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$username", username);
            command.Parameters.AddWithValue("$email", email);
            command.Parameters.AddWithValue("$hash", passwordHash);
            command.Parameters.AddWithValue("$salt", passwordSalt);
            command.Parameters.AddWithValue("$fullName", fullName);
            command.Parameters.AddWithValue("$school", school);
            command.Parameters.AddWithValue("$isAdmin", isAdmin ? 1 : 0);
            command.Parameters.AddWithValue("$plan", storagePlan);
            command.Parameters.AddWithValue("$until", storageUntil is { } until ? until.ToUnixTimeSeconds() : DBNull.Value);
            command.Parameters.AddWithValue("$createdAt", createdAt.ToUnixTimeSeconds());
            return (long)(command.ExecuteScalar() ?? 0L);
        }
    }

    public UserRow? FindUserByEmail(string email) =>
        QueryOne(
            "SELECT id, username, email, password_hash, password_salt, full_name, school, is_admin, storage_plan, storage_until, created_at FROM users WHERE email = $email;",
            command => command.Parameters.AddWithValue("$email", email),
            ReadUser);

    public UserRow? FindUserByUsername(string username) =>
        QueryOne(
            "SELECT id, username, email, password_hash, password_salt, full_name, school, is_admin, storage_plan, storage_until, created_at FROM users WHERE username = $username COLLATE NOCASE;",
            command => command.Parameters.AddWithValue("$username", username),
            ReadUser);

    public UserRow? FindUserById(long id) =>
        QueryOne(
            "SELECT id, username, email, password_hash, password_salt, full_name, school, is_admin, storage_plan, storage_until, created_at FROM users WHERE id = $id;",
            command => command.Parameters.AddWithValue("$id", id),
            ReadUser);

    public IReadOnlyList<UserRow> ListUsers() =>
        QueryMany(
            "SELECT id, username, email, password_hash, password_salt, full_name, school, is_admin, storage_plan, storage_until, created_at FROM users ORDER BY id;",
            null,
            ReadUser);

    public void UpdateUserProfile(long id, string fullName, string school) =>
        Execute(
            "UPDATE users SET full_name = $fullName, school = $school WHERE id = $id;",
            command =>
            {
                command.Parameters.AddWithValue("$fullName", fullName);
                command.Parameters.AddWithValue("$school", school);
                command.Parameters.AddWithValue("$id", id);
            });

    public void UpdateUserPassword(long id, string passwordHash, string passwordSalt) =>
        Execute(
            "UPDATE users SET password_hash = $hash, password_salt = $salt WHERE id = $id;",
            command =>
            {
                command.Parameters.AddWithValue("$hash", passwordHash);
                command.Parameters.AddWithValue("$salt", passwordSalt);
                command.Parameters.AddWithValue("$id", id);
            });

    public void UpdateUserPlan(long id, string storagePlan, DateTimeOffset? storageUntil) =>
        Execute(
            "UPDATE users SET storage_plan = $plan, storage_until = $until WHERE id = $id;",
            command =>
            {
                command.Parameters.AddWithValue("$plan", storagePlan);
                command.Parameters.AddWithValue("$until", storageUntil is { } until ? until.ToUnixTimeSeconds() : DBNull.Value);
                command.Parameters.AddWithValue("$id", id);
            });

    public void InsertToken(string tokenHash, long userId, string kind, DateTimeOffset createdAt, DateTimeOffset expiresAt) =>
        Execute(
            "INSERT INTO auth_tokens (token_hash, user_id, kind, created_at, expires_at) VALUES ($hash, $userId, $kind, $createdAt, $expiresAt);",
            command =>
            {
                command.Parameters.AddWithValue("$hash", tokenHash);
                command.Parameters.AddWithValue("$userId", userId);
                command.Parameters.AddWithValue("$kind", kind);
                command.Parameters.AddWithValue("$createdAt", createdAt.ToUnixTimeSeconds());
                command.Parameters.AddWithValue("$expiresAt", expiresAt.ToUnixTimeSeconds());
            });

    public TokenRow? FindToken(string tokenHash, DateTimeOffset now) =>
        QueryOne(
            "SELECT user_id, kind, expires_at FROM auth_tokens WHERE token_hash = $hash AND expires_at > $now;",
            command =>
            {
                command.Parameters.AddWithValue("$hash", tokenHash);
                command.Parameters.AddWithValue("$now", now.ToUnixTimeSeconds());
            },
            reader => new TokenRow(
                reader.GetInt64(0),
                reader.GetString(1),
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(2))));

    public void DeleteToken(string tokenHash) =>
        Execute(
            "DELETE FROM auth_tokens WHERE token_hash = $hash;",
            command => command.Parameters.AddWithValue("$hash", tokenHash));

    public void PurgeExpiredTokens(DateTimeOffset now) =>
        Execute(
            "DELETE FROM auth_tokens WHERE expires_at <= $now;",
            command => command.Parameters.AddWithValue("$now", now.ToUnixTimeSeconds()));

    public long InsertSubscription(long userId, string kind, string subject, int? classNumber, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO subscriptions (user_id, kind, subject, class_number, created_at, expires_at)
                VALUES ($userId, $kind, $subject, $classNumber, $createdAt, $expiresAt);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$subject", subject);
            command.Parameters.AddWithValue("$classNumber", classNumber is { } value ? value : DBNull.Value);
            command.Parameters.AddWithValue("$createdAt", createdAt.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("$expiresAt", expiresAt.ToUnixTimeSeconds());
            return (long)(command.ExecuteScalar() ?? 0L);
        }
    }

    public IReadOnlyList<SubscriptionRow> ListSubscriptions(long userId, DateTimeOffset now) =>
        QueryMany(
            "SELECT kind, subject, class_number, created_at, expires_at FROM subscriptions WHERE user_id = $userId AND expires_at > $now ORDER BY expires_at;",
            command =>
            {
                command.Parameters.AddWithValue("$userId", userId);
                command.Parameters.AddWithValue("$now", now.ToUnixTimeSeconds());
            },
            reader => new SubscriptionRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetInt32(2),
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(3)),
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(4))));

    /// <summary>Вставляет ключ; false — код уже существует.</summary>
    public bool TryCreateKey(
        string code,
        string kind,
        string? storagePlan,
        int durationDays,
        string? subject,
        int? classNumber,
        DateTimeOffset createdAt,
        long? createdBy)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT OR IGNORE INTO activation_keys (code, kind, storage_plan, duration_days, subject, class_number, created_at, created_by)
                VALUES ($code, $kind, $plan, $days, $subject, $classNumber, $createdAt, $createdBy);
                """;
            command.Parameters.AddWithValue("$code", code);
            command.Parameters.AddWithValue("$kind", kind);
            command.Parameters.AddWithValue("$plan", storagePlan is null ? DBNull.Value : storagePlan);
            command.Parameters.AddWithValue("$days", durationDays);
            command.Parameters.AddWithValue("$subject", subject is null ? DBNull.Value : subject);
            command.Parameters.AddWithValue("$classNumber", classNumber is { } value ? value : DBNull.Value);
            command.Parameters.AddWithValue("$createdAt", createdAt.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("$createdBy", createdBy is { } value2 ? value2 : DBNull.Value);
            return command.ExecuteNonQuery() > 0;
        }
    }

    public KeyRow? FindKey(string code) =>
        QueryOne(
            "SELECT code, kind, storage_plan, duration_days, subject, class_number, created_at, used_by, used_at FROM activation_keys WHERE code = $code;",
            command => command.Parameters.AddWithValue("$code", code),
            reader => new KeyRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(6)),
                reader.IsDBNull(7) ? null : reader.GetInt64(7),
                reader.IsDBNull(8) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(8))));

    /// <summary>Помечает ключ использованным; false — ключ уже активирован кем-то другим.</summary>
    public bool MarkKeyUsed(string code, long userId, DateTimeOffset usedAt)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText =
                "UPDATE activation_keys SET used_by = $userId, used_at = $usedAt WHERE code = $code AND used_by IS NULL;";
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$usedAt", usedAt.ToUnixTimeSeconds());
            command.Parameters.AddWithValue("$code", code);
            return command.ExecuteNonQuery() > 0;
        }
    }

    public IReadOnlyList<KeyRow> ListKeys(int limit) =>
        QueryMany(
            "SELECT code, kind, storage_plan, duration_days, subject, class_number, created_at, used_by, used_at FROM activation_keys ORDER BY created_at DESC LIMIT $limit;",
            command => command.Parameters.AddWithValue("$limit", limit),
            reader => new KeyRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(6)),
                reader.IsDBNull(7) ? null : reader.GetInt64(7),
                reader.IsDBNull(8) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(8))));

    private void CreateSchema() =>
        Execute("""
            CREATE TABLE IF NOT EXISTS users (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              username TEXT NOT NULL DEFAULT '',
              email TEXT NOT NULL UNIQUE COLLATE NOCASE,
              password_hash TEXT NOT NULL,
              password_salt TEXT NOT NULL,
              full_name TEXT NOT NULL DEFAULT '',
              school TEXT NOT NULL DEFAULT '',
              is_admin INTEGER NOT NULL DEFAULT 0,
              storage_plan TEXT NOT NULL DEFAULT 'free',
              storage_until INTEGER NULL,
              created_at INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS auth_tokens (
              token_hash TEXT PRIMARY KEY,
              user_id INTEGER NOT NULL,
              kind TEXT NOT NULL,
              created_at INTEGER NOT NULL,
              expires_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_auth_tokens_user ON auth_tokens(user_id);
            CREATE TABLE IF NOT EXISTS subscriptions (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              user_id INTEGER NOT NULL,
              kind TEXT NOT NULL,
              subject TEXT NOT NULL,
              class_number INTEGER NULL,
              created_at INTEGER NOT NULL,
              expires_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_subscriptions_user ON subscriptions(user_id);
            CREATE TABLE IF NOT EXISTS activation_keys (
              code TEXT PRIMARY KEY COLLATE NOCASE,
              kind TEXT NOT NULL,
              storage_plan TEXT NULL,
              duration_days INTEGER NOT NULL,
              subject TEXT NULL,
              class_number INTEGER NULL,
              created_at INTEGER NOT NULL,
              created_by INTEGER NULL,
              used_by INTEGER NULL,
              used_at INTEGER NULL
            );
            """);

    /// <summary>Догоняет базу старых версий: колонка username, её заполнение и уникальный индекс.</summary>
    private void EnsureUsernames()
    {
        var hasUsername = QueryMany(
            "PRAGMA table_info(users);",
            null,
            reader => reader.GetString(1)).Any(name => string.Equals(name, "username", StringComparison.OrdinalIgnoreCase));
        if (!hasUsername)
            Execute("ALTER TABLE users ADD COLUMN username TEXT NOT NULL DEFAULT '';");

        var users = QueryMany(
            "SELECT id, email, username FROM users ORDER BY id;",
            null,
            reader => (Id: reader.GetInt64(0), Email: reader.GetString(1), Username: reader.GetString(2)));
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in users)
        {
            var username = user.Username.Trim();
            if (username.Length == 0)
                username = SuggestUsername(user.Email, taken);
            else
                taken.Add(username);
            if (!string.Equals(username, user.Username, StringComparison.Ordinal))
                Execute(
                    "UPDATE users SET username = $username WHERE id = $id;",
                    command =>
                    {
                        command.Parameters.AddWithValue("$username", username);
                        command.Parameters.AddWithValue("$id", user.Id);
                    });
        }

        Execute("CREATE UNIQUE INDEX IF NOT EXISTS idx_users_username ON users(username COLLATE NOCASE);");
    }

    private static string SuggestUsername(string email, HashSet<string> taken)
    {
        var localPart = email.Split('@')[0].ToLowerInvariant();
        var sanitized = new string(localPart
            .Where(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-')
            .ToArray()).Trim('.', '_', '-');
        if (string.Equals(email, "admin@pvz.local", StringComparison.OrdinalIgnoreCase))
            sanitized = "admin";
        if (sanitized.Length < 3)
            sanitized = "teacher";
        if (sanitized.Length > 32)
            sanitized = sanitized[..32];
        var candidate = sanitized;
        var suffix = 2;
        while (taken.Contains(candidate))
        {
            var basis = sanitized.Length > 28 ? sanitized[..28] : sanitized;
            candidate = basis + suffix;
            suffix++;
        }

        taken.Add(candidate);
        return candidate;
    }

    private void Execute(string sql, Action<SqliteCommand>? bind = null)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            bind?.Invoke(command);
            command.ExecuteNonQuery();
        }
    }

    private T? QueryOne<T>(string sql, Action<SqliteCommand>? bind, Func<SqliteDataReader, T> read)
        where T : class
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            bind?.Invoke(command);
            using var reader = command.ExecuteReader();
            return reader.Read() ? read(reader) : null;
        }
    }

    private IReadOnlyList<T> QueryMany<T>(string sql, Action<SqliteCommand>? bind, Func<SqliteDataReader, T> read)
    {
        lock (_gate)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            bind?.Invoke(command);
            using var reader = command.ExecuteReader();
            var items = new List<T>();
            while (reader.Read())
                items.Add(read(reader));
            return items;
        }
    }

    private static UserRow ReadUser(SqliteDataReader reader) =>
        new(
            reader.GetInt64(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetInt64(7) != 0,
            reader.GetString(8),
            reader.IsDBNull(9) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(9)),
            DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(10)));

    public void Dispose() => _connection.Dispose();

    public sealed record UserRow(
        long Id,
        string Username,
        string Email,
        string PasswordHash,
        string PasswordSalt,
        string FullName,
        string School,
        bool IsAdmin,
        string StoragePlan,
        DateTimeOffset? StorageUntil,
        DateTimeOffset CreatedAt);

    public sealed record TokenRow(long UserId, string Kind, DateTimeOffset ExpiresAt);

    public sealed record SubscriptionRow(string Kind, string Subject, int? ClassNumber, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

    public sealed record KeyRow(
        string Code,
        string Kind,
        string? StoragePlan,
        int DurationDays,
        string? Subject,
        int? ClassNumber,
        DateTimeOffset CreatedAt,
        long? UsedBy,
        DateTimeOffset? UsedAt);
}
