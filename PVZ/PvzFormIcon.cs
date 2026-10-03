namespace PVZ;

internal static class PvzFormIcon
{
    private static Icon? _cached;

    public static void TryApply(Form form)
    {
        if (form.Icon is not null)
            return;

        try
        {
            form.Icon = _cached ??= Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                ?? throw new InvalidOperationException();
        }
        catch (InvalidOperationException)
        {
            // optional window icon
        }
    }
}
