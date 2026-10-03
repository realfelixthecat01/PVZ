using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PVZ.Core;

public enum AppTheme
{
    Light,
    Dark,
}

public sealed class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Light;
    public string AccentColor { get; set; } = "#2563A5";
    public string MusicFilePath { get; set; } = SettingsStore.DefaultMusicPath;
    public bool MusicEnabled { get; set; }
    public int MusicVolume { get; set; } = 35;
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PVZ",
        "settings.json");

    public static string DefaultMusicPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PVZ",
        "Music",
        "background.mp3");

    public static AppSettings Load(string? path = null)
    {
        var settingsPath = path ?? SettingsPath;
        if (!File.Exists(settingsPath))
            return new AppSettings();

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath), JsonOptions)
                ?? throw new InvalidDataException("Файл настроек пуст.");

            if (!Enum.IsDefined(settings.Theme))
                throw new InvalidDataException("В файле настроек указана неизвестная тема.");
            if (!IsHexColor(settings.AccentColor))
                throw new InvalidDataException("В файле настроек указан некорректный цвет акцента.");

            settings.MusicVolume = Math.Clamp(settings.MusicVolume, 0, 100);
            if (string.IsNullOrWhiteSpace(settings.MusicFilePath))
                settings.MusicFilePath = DefaultMusicPath;

            return settings;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Не удалось прочитать файл настроек «{settingsPath}».", ex);
        }
    }

    public static void Save(AppSettings settings, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!Enum.IsDefined(settings.Theme))
            throw new InvalidDataException("Выбрана неизвестная тема оформления.");
        if (!IsHexColor(settings.AccentColor))
            throw new InvalidDataException("Выбран некорректный цвет акцента.");

        settings.MusicVolume = Math.Clamp(settings.MusicVolume, 0, 100);
        if (string.IsNullOrWhiteSpace(settings.MusicFilePath))
            settings.MusicFilePath = DefaultMusicPath;

        var settingsPath = path ?? SettingsPath;
        var directory = Path.GetDirectoryName(settingsPath)
            ?? throw new InvalidOperationException("Не удалось определить каталог настроек.");
        Directory.CreateDirectory(directory);
        var temporaryPath = settingsPath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, settingsPath, overwrite: true);
    }

    private static bool IsHexColor(string? value) =>
        value is { Length: 7 } &&
        value[0] == '#' &&
        value.AsSpan(1).ToString().All(Uri.IsHexDigit);
}

public static class BackgroundMusicPlayer
{
    private const string Alias = "pvzBackgroundMusic";
    private static bool _opened;

    public static bool TryPlay(string filePath, int volume, out string? errorMessage)
    {
        errorMessage = null;
        if (!OperatingSystem.IsWindows())
        {
            errorMessage = "Фоновая музыка доступна только в Windows.";
            return false;
        }

        CloseCurrent();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            errorMessage = $"Музыкальный файл не найден: {filePath}";
            return false;
        }

        if (filePath.IndexOfAny(['"', '\r', '\n']) >= 0)
        {
            errorMessage = "Путь к музыкальному файлу содержит недопустимые символы.";
            return false;
        }

        try
        {
            var escapedPath = Path.GetFullPath(filePath).Replace("\"", "\"\"");
            if (!Send($"open \"{escapedPath}\" type mpegvideo alias {Alias}", out errorMessage))
                return false;

            _opened = true;
            if (!TrySetVolume(volume, out errorMessage))
            {
                CloseCurrent();
                return false;
            }

            if (Send($"play {Alias} repeat", out errorMessage))
                return true;

            CloseCurrent();
            return false;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            CloseCurrent();
            errorMessage = $"Не удалось запустить фоновую музыку: {ex.Message}";
            return false;
        }
    }

    public static bool TrySetVolume(int volume, out string? errorMessage)
    {
        errorMessage = null;
        if (!_opened)
            return true;

        try
        {
            return Send($"setaudio {Alias} volume to {Math.Clamp(volume, 0, 100) * 10}", out errorMessage);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            errorMessage = $"Не удалось изменить громкость фоновой музыки: {ex.Message}";
            return false;
        }
    }

    public static void Stop()
    {
        CloseCurrent();
    }

    private static void CloseCurrent()
    {
        if (!_opened)
            return;

        try
        {
            _ = mciSendString($"close {Alias}", null, 0, IntPtr.Zero);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // The device may be unavailable during shutdown.
        }

        _opened = false;
    }

    private static bool Send(string command, out string? errorMessage)
    {
        var result = mciSendString(command, null, 0, IntPtr.Zero);
        if (result == 0)
        {
            errorMessage = null;
            return true;
        }

        var buffer = new System.Text.StringBuilder(256);
        errorMessage = mciGetErrorString(result, buffer, buffer.Capacity)
            ? buffer.ToString()
            : $"Код ошибки Windows MCI: {result}.";
        return false;
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int mciSendString(string command, System.Text.StringBuilder? returnValue, int returnLength, IntPtr callback);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool mciGetErrorString(int errorCode, System.Text.StringBuilder errorText, int errorTextSize);
}
