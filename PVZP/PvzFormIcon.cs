// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

namespace PVZ.Player;

public static class PvzFormIcon
{
    private static Icon? _cached;
    private static readonly object LoadLock = new();

    public static void TryApply(Form form)
    {
        var icon = GetOrLoad();
        if (icon is null)
            return;

        try
        {
            form.Icon = (Icon)icon.Clone();
        }
        catch (ArgumentException)
        {
            // optional window icon
        }
    }

    private static Icon? GetOrLoad()
    {
        if (_cached is not null)
            return _cached;

        lock (LoadLock)
        {
            if (_cached is not null)
                return _cached;

            var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                _cached = new Icon(iconPath);
                return _cached;
            }

            try
            {
                _cached = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch (ArgumentException)
            {
                _cached = null;
            }

            return _cached;
        }
    }
}
