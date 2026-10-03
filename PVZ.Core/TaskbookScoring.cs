namespace PVZ.Core;

public static class TaskbookScoring
{
    public static int GetAllTestsPointsMax(TaskbookTaskDto task) =>
        task.Tests.Sum(t => t.Points);

    public static int CalculatePercentageScore(int passedTests, int totalTests)
    {
        if (totalTests <= 0)
            return 0;

        var safePassedTests = Math.Clamp(passedTests, 0, totalTests);
        return (int)Math.Round(safePassedTests * 100d / totalTests, MidpointRounding.AwayFromZero);
    }

    public static int GetTaskMaxScore(TaskbookTaskDto task)
    {
        if (task.Tests.Count == 0)
            return 0;

        if (task.ScoringMode == TaskScoringMode.Percentage)
            return 100;

        return GetAllTestsPointsMax(task) + task.BonusPointsForAllTests;
    }

    public static int GetDocumentMaxScore(TaskbookDocument document) =>
        document.Tasks.Sum(GetTaskMaxScore);
}
