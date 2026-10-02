namespace PVZ.Core;

public static class TaskbookScoring
{
    public static int GetAllTestsPointsMax(TaskbookTaskDto task) =>
        task.Tests.Sum(t => t.Points);

    public static int GetTaskMaxScore(TaskbookTaskDto task)
    {
        if (task.Tests.Count == 0)
            return 0;

        return GetAllTestsPointsMax(task) + task.BonusPointsForAllTests;
    }

    public static int GetDocumentMaxScore(TaskbookDocument document) =>
        document.Tasks.Sum(GetTaskMaxScore);
}
