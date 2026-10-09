namespace PVZ.Core;

public static class TaskbookScoring
{
    public static bool IsFillTask(TaskbookTaskDto task) =>
        task.Kind == TaskbookTaskKind.FillInBlanks;

    public static int GetAllTestsPointsMax(TaskbookTaskDto task)
    {
        if (IsFillTask(task))
            return task.Blanks.Sum(blank => blank.Points);

        return task.Tests.Sum(test => test.Points);
    }

    public static int GetTaskMaxScore(TaskbookTaskDto task)
    {
        if (IsFillTask(task))
            return task.Blanks.Count == 0 ? 0 : GetAllTestsPointsMax(task) + task.BonusPointsForAllTests;

        if (task.Tests.Count == 0)
            return 0;

        return GetAllTestsPointsMax(task) + task.BonusPointsForAllTests;
    }

    public static int GetDocumentMaxScore(TaskbookDocument document) =>
        document.Tasks.Sum(GetTaskMaxScore);
}
