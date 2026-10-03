using System.Reflection;
using System.Text;

namespace PVZ.Core;

public static class EmbeddedTaskbook
{
    public const string ResourceName = "taskbook.json";

    public static bool TryLoadAppendedForRunningApp(out TaskbookDocument? document)
    {
        document = null;
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            return false;

        return TaskbookPayload.TryReadAppended(exePath, out document) && document != null;
    }

    public static TaskbookDocument LoadForRunningApp()
    {
        if (TryLoadAppendedForRunningApp(out var appended) && appended != null)
            return appended;

        return LoadFromAssembly(Assembly.GetExecutingAssembly());
    }

    public static TaskbookDocument LoadFromAssembly(Assembly assembly)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(ResourceName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null)
            throw new InvalidOperationException("В программе не найден встроенный задачник (taskbook.json).");

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new InvalidOperationException("Не удалось прочитать встроенный задачник.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return TaskbookIO.Deserialize(reader.ReadToEnd());
    }
}
