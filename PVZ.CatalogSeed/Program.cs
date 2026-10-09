using PVZ.Catalog;
using PVZ.Core;

namespace PVZ.CatalogSeed;

public static class Program
{
    // Совпадает с паролем преподавателя по умолчанию на сервере (TaskbookStore.DefaultAccessPassword).
    private const string TeacherPassword = "ezfridgembxd";

    public static int Main(string[] args)
    {
        var outputRoot = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : Path.Combine(TaskbookUserFolders.RootPath, "zadachniki");

        Console.WriteLine($"Папка каталога: {outputRoot}");

        // Консольный режим всегда перезаписывает сборники, чтобы обновить их содержимое.
        foreach (var subject in new[] { TaskbookCatalogSubjects.English, TaskbookCatalogSubjects.Math })
        {
            var displayName = TaskbookCatalogSubjects.All
                .First(item => item.Id == subject).DisplayName;
            Console.WriteLine();
            Console.WriteLine($"{displayName}:");
            var seeds = subject == TaskbookCatalogSubjects.Math
                ? MathCollections.Build()
                : EnglishCollections.Build();
            foreach (var seed in seeds)
                Console.WriteLine($"  {seed.ClassNumber} класс · {seed.Title} → {seed.FileName}");
        }

        Console.WriteLine();
        Console.WriteLine("Информатика (пустые шаблоны):");
        foreach (var seed in TemplateCollections.BuildInformatics())
            Console.WriteLine($"  {seed.ClassNumber} класс → {seed.FileName}");

        var created = CatalogSeeder.Ensure(outputRoot, TeacherPassword, overwrite: true);
        Console.WriteLine();
        Console.WriteLine($"Готово: {created} сборников в {outputRoot}.");
        return 0;
    }
}
