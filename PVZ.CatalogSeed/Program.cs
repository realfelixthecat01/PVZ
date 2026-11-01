// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using PVZ.Catalog;
using PVZ.Core;

namespace PVZ.CatalogSeed;

public static class Program
{
    public static int Main(string[] args)
    {
        // Этот пароль попадает в каждый сборник каталога: по нему редактор учителя открывает задачник на правку.
        var teacherPassword = Environment.GetEnvironmentVariable("PVZ_CATALOG_PASSWORD");
        if (string.IsNullOrWhiteSpace(teacherPassword))
        {
            Console.WriteLine("Задайте PVZ_CATALOG_PASSWORD: этим паролем закрываются сборники каталога от правки.");
            return 1;
        }

        var outputRoot = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
            ? args[0]
            : Path.Combine(TaskbookUserFolders.RootPath, "zadachniki");

        Console.WriteLine($"Папка каталога: {outputRoot}");

        // Консольный режим всегда перезаписывает сборники, чтобы обновить их содержимое.
        foreach (var subject in new[]
                 {
                     TaskbookCatalogSubjects.Math,
                     TaskbookCatalogSubjects.English,
                     TaskbookCatalogSubjects.Informatics,
                 })
        {
            var displayName = TaskbookCatalogSubjects.All
                .First(item => item.Id == subject).DisplayName;
            var seeds = subject switch
            {
                TaskbookCatalogSubjects.Math => MathCollections.Build(),
                TaskbookCatalogSubjects.English => EnglishCollections.Build(),
                _ => InformaticsCollections.Build(),
            };
            Console.WriteLine();
            Console.WriteLine($"{displayName}:");
            foreach (var seed in seeds)
                Console.WriteLine($"  {seed.ClassNumber} класс · {seed.Title} → {seed.FileName}");
        }

        var created = CatalogSeeder.Ensure(outputRoot, teacherPassword, overwrite: true);
        Console.WriteLine();
        Console.WriteLine($"Готово: {created} сборников в {outputRoot}.");
        return 0;
    }
}
