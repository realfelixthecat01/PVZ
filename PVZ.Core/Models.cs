namespace PVZ.Core;

[Flags]
public enum TaskbookCompilerFlags
{
    None = 0,
    Python = 1,
    PascalAbc = 2,
    All = Python | PascalAbc,
}

public sealed class TestCaseDto
{
    public string Input { get; set; } = "";
    public string Output { get; set; } = "";
    public bool ShowToStudent { get; set; }
    public int Points { get; set; } = 25;
}

public sealed class TaskbookTaskDto
{
    public string Description { get; set; } = "";
    public int BonusPointsForAllTests { get; set; } = 10;
    public List<TestCaseDto> Tests { get; set; } = new();
}

public sealed class TaskbookDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string TeacherPasswordHash { get; set; } = "";
    public string TeacherPasswordSalt { get; set; } = "";
    public bool SequentialUnlock { get; set; }
    public bool TopMost { get; set; }
    public bool FullScreen { get; set; }
    public bool KillExplorer { get; set; }
    public TaskbookCompilerFlags AllowedCompilers { get; set; } = TaskbookCompilerFlags.All;
    public List<TaskbookTaskDto> Tasks { get; set; } = new();
}
