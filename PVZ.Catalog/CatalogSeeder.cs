// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using PVZ.Core;

namespace PVZ.Catalog;

/// <summary>
/// Раскладывает стандартные задачники по предметам и классам.
/// Используется консольным PVZ.CatalogSeed.
/// </summary>
public static class CatalogSeeder
{
    /// <summary>Сколько сборников всего лежит в каталоге после наполнения.</summary>
    public static int TotalCollections =>
        EnglishCollections.Build().Count
        + MathCollections.Build().Count
        + InformaticsCollections.Build().Count;

    /// <summary>
    /// Наполняет каталог стандартными сборниками. Существующие файлы не трогаются,
    /// если <paramref name="overwrite"/> не задан.
    /// </summary>
    public static int Ensure(string outputRoot, string teacherPassword, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(outputRoot))
            throw new ArgumentException("Не задана папка каталога.", nameof(outputRoot));

        var created = 0;
        created += SeedSubject(TaskbookCatalogSubjects.English, EnglishCollections.Build(), outputRoot, teacherPassword, overwrite);
        created += SeedSubject(TaskbookCatalogSubjects.Math, MathCollections.Build(), outputRoot, teacherPassword, overwrite);
        created += SeedSubject(TaskbookCatalogSubjects.Informatics, InformaticsCollections.Build(), outputRoot, teacherPassword, overwrite);
        return created;
    }

    private static int SeedSubject(
        string subject,
        IReadOnlyList<FillBlankCollectionSeed> seeds,
        string outputRoot,
        string teacherPassword,
        bool overwrite)
    {
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
