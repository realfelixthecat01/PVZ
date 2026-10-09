namespace PVZ.Core;

/// <summary>
/// Персональный адрес сервера PVZS, сохранённый пользователем через UI
/// (поле в форме входа ученика или диалог учителя).
/// Приоритет адреса: переменная PVZ_SERVER_URL → это хранилище → DefaultServerUrl.
/// </summary>
public static class ServerAddressStore
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PVZ",
        "server-address.txt");

    public static string? Load()
    {
        try
        {
            return File.Exists(FilePath) ? File.ReadAllText(FilePath).Trim() : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static void Save(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return;
        }

        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(FilePath, address.Trim());
    }
}
