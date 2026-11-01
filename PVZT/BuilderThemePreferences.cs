// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using System.Text.Json;

namespace PVZ;

internal enum BuilderThemeMode
{
    Light,
    Dark,
    Custom,
}

internal sealed record BuilderThemeSettings(BuilderThemeMode Mode, int AccentArgb)
{
    public static BuilderThemeSettings Default { get; } =
        new(BuilderThemeMode.Dark, Color.FromArgb(99, 102, 241).ToArgb());
}

internal static class BuilderThemePreferences
{
    private static readonly string SettingsPath =
        Path.Combine(Application.UserAppDataPath, "theme.json");

    public static BuilderThemeSettings Load(out string? warning)
    {
        warning = null;
        if (!File.Exists(SettingsPath))
            return BuilderThemeSettings.Default;

        try
        {
            var settings = JsonSerializer.Deserialize<BuilderThemeSettings>(File.ReadAllText(SettingsPath));
            if (settings is null ||
                !Enum.IsDefined(settings.Mode) ||
                Color.FromArgb(settings.AccentArgb).A != byte.MaxValue)
            {
                warning = "Сохранённая цветовая схема повреждена. Используется тёмная тема.";
                return BuilderThemeSettings.Default;
            }

            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            warning = $"Не удалось прочитать настройки темы: {ex.Message}";
            return BuilderThemeSettings.Default;
        }
    }

    public static bool TrySave(BuilderThemeSettings settings, out string? error)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }
}
