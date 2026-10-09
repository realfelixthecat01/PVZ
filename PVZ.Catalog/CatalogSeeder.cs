using PVZ.Core;

namespace PVZ.Catalog;

/// <summary>
/// Раскладывает стандартные задачники по предметам и классам.
/// Используется и консольным PVZ.CatalogSeed, и сервером PVZS (автонаполнение каталога),
/// и установщиком лаунчера.
/// </summary>
public static class CatalogSeeder
{
    /// <summary>Сколько сборников всего лежит в каталоге после наполнения.</summary>
    public static int TotalCollections =>
        EnglishCollections.Build().Count
        + MathCollections.Build().Count
        + TemplateCollections.BuildInformatics().Count;

    /// <summary>
    /// Наполняет каталог стандартными сборниками. Существующие файлы не трогаются,
    /// если <paramref name="overwrite"/> не задан.
    /// </summary>
    public static int Ensure(string outputRoot, string teacherPassword, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(outputRoot))
            throw new ArgumentException("Не задана папка каталога.", nameof(outputRoot));

        var created = 0;
        created += SeedSubject(TaskbookCatalogSubjects.English, outputRoot, teacherPassword, overwrite);
        created += SeedSubject(TaskbookCatalogSubjects.Math, outputRoot, teacherPassword, overwrite);
        created += SeedTemplates(outputRoot, teacherPassword, overwrite);
        return created;
    }

    private static int SeedTemplates(string outputRoot, string teacherPassword, bool overwrite)
    {
        const string subject = TaskbookCatalogSubjects.Informatics;
        var created = 0;
        foreach (var seed in TemplateCollections.BuildInformatics())
        {
            var directory = Path.Combine(outputRoot, subject, seed.ClassNumber.ToString());
            var path = Path.Combine(directory, seed.FileName);
            if (!overwrite && File.Exists(path))
                continue;

            var document = TemplateCollections.CreateDocument(subject, seed, teacherPassword);
            Directory.CreateDirectory(directory);
            TaskbookPackageIO.Save(path, document);
            created++;
        }

        return created;
    }

    private static int SeedSubject(string subject, string outputRoot, string teacherPassword, bool overwrite)
    {
        var seeds = subject == TaskbookCatalogSubjects.Math
            ? MathCollections.Build()
            : EnglishCollections.Build();

        var created = 0;
        foreach (var seed in seeds)
        {
            var directory = Path.Combine(outputRoot, subject, seed.ClassNumber.ToString());
            var path = Path.Combine(directory, seed.FileName);
            if (!overwrite && File.Exists(path))
                continue;

            var document = FillBlankCollectionBuilder.CreateDocument(subject, seed, teacherPassword);
            Directory.CreateDirectory(directory);
            TaskbookPackageIO.Save(path, document);
            created++;
        }

        return created;
    }
}
