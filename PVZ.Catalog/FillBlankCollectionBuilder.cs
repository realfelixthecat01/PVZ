using PVZ.Core;

namespace PVZ.Catalog;

public sealed record FillBlankTaskSeed(string Text, string[] Answers);

public sealed record FillBlankCollectionSeed(int ClassNumber, string FileName, string Title, FillBlankTaskSeed[] Tasks);

/// <summary>
/// Собирает .pvz-задачник со заданиями на пропуски («_») для указанного предмета.
/// Количество ответов обязано совпадать с количеством «_»; иначе сборка падает.
/// </summary>
public static class FillBlankCollectionBuilder
{
    public static TaskbookDocument CreateDocument(string subject, FillBlankCollectionSeed seed, string teacherPassword)
    {
        var (hash, salt) = PasswordUtil.CreateHash(teacherPassword);
        var document = new TaskbookDocument
        {
            Subject = subject,
            ClassNumber = seed.ClassNumber,
            TeacherPasswordHash = hash,
            TeacherPasswordSalt = salt,
            SequentialUnlock = true,
            Tasks = new List<TaskbookTaskDto>(),
        };

        foreach (var taskSeed in seed.Tasks)
        {
            var blankCount = FillBlankRules.CountBlanks(taskSeed.Text);
            if (blankCount != taskSeed.Answers.Length)
            {
                throw new InvalidDataException(
                    $"Сборник «{seed.FileName}»: в тексте {blankCount} пропусков, а ответов {taskSeed.Answers.Length}.");
            }

            var task = new TaskbookTaskDto
            {
                Kind = TaskbookTaskKind.FillInBlanks,
                Description = taskSeed.Text,
                BonusPointsForAllTests = 10,
            };

            foreach (var rawAnswer in taskSeed.Answers)
            {
                var (answer, extraVariants) = FillBlankRules.SplitAnswerVariants(rawAnswer);
                task.Blanks.Add(new FillBlankDto
                {
                    Answer = answer,
                    AcceptedAnswers = extraVariants.ToList(),
                    Points = 10,
                    ShowToStudent = true,
                });
            }

            document.Tasks.Add(task);
        }

        return document;
    }
}
