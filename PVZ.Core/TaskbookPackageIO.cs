using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace PVZ.Core;

public static class TaskbookPackageIO
{
    private static ReadOnlySpan<byte> Magic => "PVZPKG1"u8;
    private const int ChecksumLength = 32;
    private const int HeaderLength = 7 + sizeof(int) + ChecksumLength;
    private const int MaximumPackageBytes = 16 * 1024 * 1024;
    private const int MaximumDocumentBytes = 64 * 1024 * 1024;

    public static void Save(string path, TaskbookDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(document);

        var json = Encoding.UTF8.GetBytes(TaskbookIO.Serialize(document));
        if (json.Length == 0 || json.Length > MaximumDocumentBytes)
            throw new InvalidDataException("Размер задачника не поддерживается.");

        using var compressed = new MemoryStream();
        using (var brotli = new BrotliStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
            brotli.Write(json);
        if (HeaderLength + compressed.Length > MaximumPackageBytes)
            throw new InvalidDataException("Размер сжатого задачника превышает допустимый предел.");

        using var package = new MemoryStream(HeaderLength + (int)compressed.Length);
        package.Write(Magic);
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, json.Length);
        package.Write(lengthBytes);
        package.Write(SHA256.HashData(json));
        compressed.Position = 0;
        compressed.CopyTo(package);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(tempPath, package.ToArray());
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public static TaskbookDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var packageLength = new FileInfo(path).Length;
        if (packageLength <= HeaderLength || packageLength > MaximumPackageBytes)
            throw new InvalidDataException("Размер файла задачника недопустим.");
        var package = File.ReadAllBytes(path);
        if (!package.AsSpan(0, Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Файл не является задачником PVZ или имеет неподдерживаемую версию.");

        var documentLength = BinaryPrimitives.ReadInt32LittleEndian(package.AsSpan(Magic.Length, sizeof(int)));
        if (documentLength <= 0 || documentLength > MaximumDocumentBytes)
            throw new InvalidDataException("В файле указан недопустимый размер задачника.");

        var expectedHash = package.AsSpan(Magic.Length + sizeof(int), ChecksumLength);
        using var input = new MemoryStream(package, HeaderLength, package.Length - HeaderLength, writable: false);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var json = new MemoryStream(documentLength);
        var buffer = new byte[81920];
        int read;
        try
        {
            while ((read = brotli.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (json.Length + read > MaximumDocumentBytes || json.Length + read > documentLength)
                    throw new InvalidDataException("Содержимое задачника превышает допустимый размер.");
                json.Write(buffer, 0, read);
            }
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidDataException("Файл задачника повреждён или содержит неверные сжатые данные.", ex);
        }

        if (json.Length != documentLength)
            throw new InvalidDataException("Файл задачника повреждён или обрезан.");

        var jsonBytes = json.ToArray();
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, SHA256.HashData(jsonBytes)))
            throw new InvalidDataException("Контрольная сумма задачника не совпадает.");

        var document = TaskbookIO.Deserialize(Encoding.UTF8.GetString(jsonBytes));
        if (document.SchemaVersion != 1 || document.Tasks.Count == 0)
            throw new InvalidDataException("В файле нет задач или версия формата не поддерживается.");

        return document;
    }
}
