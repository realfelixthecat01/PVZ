using System.Text.Json;

namespace PVZ.Core;

public static class TaskbookIO
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string Serialize(TaskbookDocument document) =>
        JsonSerializer.Serialize(document, Options);

    public static TaskbookDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<TaskbookDocument>(json, Options)
        ?? throw new InvalidDataException("Пустой или неверный задачник.");

    public static TaskbookDocument LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return Deserialize(json);
    }

    public static void SaveToFile(string path, TaskbookDocument document) =>
        File.WriteAllText(path, Serialize(document));
}
