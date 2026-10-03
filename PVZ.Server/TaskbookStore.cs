using System.Security.Cryptography;
using System.Text.Json;
using PVZ.Core;

namespace PVZ.Server;

public sealed record TaskbookTokenSummary(
    string Id,
    string TokenHint,
    DateTimeOffset UploadedAt,
    DateTimeOffset ExpiresAt,
    bool IsActive);

public sealed record UploadedTaskbook(string Token, DateTimeOffset UploadedAt, DateTimeOffset ExpiresAt);

public sealed class TaskbookStore
{
    public const int MaximumPackageBytes = 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _dataDirectory;
    private readonly string _packageDirectory;
    private readonly string _manifestPath;
    private readonly TimeSpan _tokenLifetime;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<TokenRecord>? _records;

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
        _manifestPath = Path.Combine(_dataDirectory, "tokens.json");
        _tokenLifetime = tokenLifetime;
        Directory.CreateDirectory(_packageDirectory);
    }

    public async Task<UploadedTaskbook> UploadAsync(byte[] package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.Length == 0 || package.Length > MaximumPackageBytes)
            throw new InvalidDataException("Размер .pvz файла недопустим.");

        var temporaryPath = Path.Combine(_dataDirectory, $".upload-{Guid.NewGuid():N}.pvz");
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, package, cancellationToken);
            try
            {
                _ = TaskbookPackageIO.Load(temporaryPath);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Файл не содержит корректный задачник PVZ.", exception);
            }

            await _gate.WaitAsync(cancellationToken);
            try
            {
                var records = await LoadRecordsAsync(cancellationToken);
                DeleteInactivePackages(records);
                var token = Base64Url(RandomNumberGenerator.GetBytes(32));
                var tokenHash = HashToken(token);
                var uploadedAt = DateTimeOffset.UtcNow;
                var expiresAt = uploadedAt + _tokenLifetime;
                var packageFile = $"{tokenHash}.pvz";
                var targetPath = Path.Combine(_packageDirectory, packageFile);
                File.Move(temporaryPath, targetPath);
                records.Add(new TokenRecord(tokenHash, token[..8], packageFile, uploadedAt, expiresAt, null));
                try
                {
                    await SaveRecordsAsync(records, cancellationToken);
                }
                catch
                {
                    File.Delete(targetPath);
                    throw;
                }

                _records = records;
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
        var tokenHash = HashToken(token);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var records = await LoadRecordsAsync(cancellationToken);
            var record = records.FirstOrDefault(item =>
                CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(item.TokenHash),
                    Convert.FromHexString(tokenHash)));
            if (record is null)
                return null;

            var packagePath = SafePackagePath(record.PackageFile);
            if (record.RevokedAt is not null || record.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                if (File.Exists(packagePath))
                    File.Delete(packagePath);
                return null;
            }
            if (!File.Exists(packagePath))
                return null;

            return await File.ReadAllBytesAsync(packagePath, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<TaskbookTokenSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var records = await LoadRecordsAsync(cancellationToken);
            DeleteInactivePackages(records);
            var now = DateTimeOffset.UtcNow;
            return records
                .OrderByDescending(item => item.UploadedAt)
                .Select(item => new TaskbookTokenSummary(
                    item.TokenHash[..16],
                    $"{item.TokenHint}...",
                    item.UploadedAt,
                    item.ExpiresAt,
                    item.RevokedAt is null && item.ExpiresAt > now))
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RevokeAsync(string id, CancellationToken cancellationToken = default)
    {
        if (id.Length != 16 || id.Any(character => !Uri.IsHexDigit(character)))
            return false;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var records = await LoadRecordsAsync(cancellationToken);
            var index = records.FindIndex(item => item.TokenHash.StartsWith(id, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || records[index].RevokedAt is not null)
                return false;

            records[index] = records[index] with { RevokedAt = DateTimeOffset.UtcNow };
            await SaveRecordsAsync(records, cancellationToken);
            _records = records;
            var packagePath = SafePackagePath(records[index].PackageFile);
            if (File.Exists(packagePath))
                File.Delete(packagePath);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private string SafePackagePath(string packageFile)
    {
        if (!string.Equals(packageFile, Path.GetFileName(packageFile), StringComparison.Ordinal) ||
            !packageFile.EndsWith(".pvz", StringComparison.Ordinal) ||
            packageFile.Length != 68)
            throw new InvalidDataException("В хранилище обнаружена недопустимая запись файла.");
        return Path.Combine(_packageDirectory, packageFile);
    }

    private void DeleteInactivePackages(IEnumerable<TokenRecord> records)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var record in records)
        {
            if (record.RevokedAt is null && record.ExpiresAt > now)
                continue;
            var packagePath = SafePackagePath(record.PackageFile);
            if (File.Exists(packagePath))
                File.Delete(packagePath);
        }
    }

    private async Task<List<TokenRecord>> LoadRecordsAsync(CancellationToken cancellationToken)
    {
        if (_records is not null)
            return _records;

        if (!File.Exists(_manifestPath))
            return _records = new List<TokenRecord>();

        await using var input = File.OpenRead(_manifestPath);
        _records = await JsonSerializer.DeserializeAsync<List<TokenRecord>>(input, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Файл базы токенов пуст или повреждён.");
        foreach (var record in _records)
            _ = SafePackagePath(record.PackageFile);
        return _records;
    }

    private async Task SaveRecordsAsync(List<TokenRecord> records, CancellationToken cancellationToken)
    {
        var temporaryPath = Path.Combine(_dataDirectory, $".tokens-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = File.Create(temporaryPath))
                await JsonSerializer.SerializeAsync(output, records, JsonOptions, cancellationToken);
            File.Move(temporaryPath, _manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static TimeSpan ValidateTokenLifetimeDays(int tokenLifetimeDays)
    {
        if (tokenLifetimeDays is < 1 or > 3650)
            throw new ArgumentOutOfRangeException(nameof(tokenLifetimeDays));
        return TimeSpan.FromDays(tokenLifetimeDays);
    }

    private sealed record TokenRecord(
        string TokenHash,
        string TokenHint,
        string PackageFile,
        DateTimeOffset UploadedAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? RevokedAt);
}
