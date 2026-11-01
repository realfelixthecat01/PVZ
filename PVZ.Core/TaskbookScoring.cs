// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

namespace PVZ.Core;

public static class TaskbookScoring
{
    public static bool IsFillTask(TaskbookTaskDto task) =>
        task.Kind == TaskbookTaskKind.FillInBlanks;

    public static int GetAllTestsPointsMax(TaskbookTaskDto task) => task.Kind switch
    {
        // У выбора в максимум идут только верные варианты: отмеченный зря всегда отнимает баллы.
        TaskbookTaskKind.Choice => task.Options.Where(o => o.Correct).Sum(o => o.Points),
        TaskbookTaskKind.Matching => task.Pairs.Sum(p => p.Points),
        // Короткий ответ хранит единственный ответ в том же списке Blanks, что и пропуски.
        TaskbookTaskKind.FillInBlanks or TaskbookTaskKind.ShortAnswer => task.Blanks.Sum(b => b.Points),
        _ => task.Tests.Sum(t => t.Points),
    };

    public static int GetTaskMaxScore(TaskbookTaskDto task)
    {
        // Пустое наполнение задания даёт максимум 0, иначе сумма баллов плюс бонус за все.
        int count = task.Kind switch
        {
            TaskbookTaskKind.FillInBlanks or TaskbookTaskKind.ShortAnswer => task.Blanks.Count,
            TaskbookTaskKind.Choice => task.Options.Count,
            TaskbookTaskKind.Matching => task.Pairs.Count,
            _ => task.Tests.Count,
        };
        return count == 0 ? 0 : GetAllTestsPointsMax(task) + task.BonusPointsForAllTests;
    }

    public static int GetDocumentMaxScore(TaskbookDocument document) =>
        document.Tasks.Sum(GetTaskMaxScore);
}
