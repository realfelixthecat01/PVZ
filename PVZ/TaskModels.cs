namespace PVZ
{
    internal sealed class TestCase
    {
        public string Input { get; set; } = "";
        public string Output { get; set; } = "";
        public bool ShowToStudent { get; set; }
        public int Points { get; set; } = 25;
    }

    internal sealed class TaskItem
    {
        public string Description { get; set; } = "";
        public int BonusPointsForAllTests { get; set; } = 10;
        public List<TestCase> Tests { get; } = new();
    }
}
