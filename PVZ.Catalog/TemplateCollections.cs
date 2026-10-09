using PVZ.Core;

namespace PVZ.Catalog;

/// <summary>Описание пустого шаблона-задачника для предмета и класса.</summary>
public sealed record TemplateCollectionSeed(int ClassNumber, string FileName, string StarterTask);

/// <summary>
/// Пустые шаблоны задачников по предметам без готовых заданий (сейчас — информатика, 6–11 классы).
/// Формат .pvz не допускает полностью пустой список задач, поэтому в шаблоне оставлена
/// одна задача-заглушка: учитель открывает файл в редакторе PVZP и заменяет её своими заданиями.
/// </summary>
public static class TemplateCollections
{
    public static IReadOnlyList<TemplateCollectionSeed> BuildInformatics()
    {
        const string subject = TaskbookCatalogSubjects.Informatics;
        var seeds = new List<TemplateCollectionSeed>();
        for (var classNumber = TaskbookCatalogSubjects.MinimumClassNumber(subject);
             classNumber <= 11;
             classNumber++)
        {
            seeds.Add(new TemplateCollectionSeed(
                classNumber,
                $"informatics-{classNumber}-template.pvz",
                $"Шаблон по информатике для {classNumber} класса. Откройте его в редакторе PVZP и добавьте задания. "
                + "Ниже — пример: программа должна вывести «Hello, world!»."));
        }

        return seeds;
    }

    public static TaskbookDocument CreateDocument(string subject, TemplateCollectionSeed seed, string teacherPassword)
    {
        var (hash, salt) = PasswordUtil.CreateHash(teacherPassword);
        return new TaskbookDocument
        {
            Subject = subject,
            ClassNumber = seed.ClassNumber,
            TeacherPasswordHash = hash,
            TeacherPasswordSalt = salt,
            SequentialUnlock = true,
            Tasks = new List<TaskbookTaskDto>
            {
                new()
                {
                    Kind = TaskbookTaskKind.CodeTests,
                    Description = seed.StarterTask,
                    BonusPointsForAllTests = 10,
                    Tests = new List<TestCaseDto>
                    {
                        new() { Input = "", Output = "Hello, world!", ShowToStudent = true, Points = 25 },
                    },
                },
            },
        };
    }
}
