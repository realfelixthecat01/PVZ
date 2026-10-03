using PVZ.Core;

var checks = 0;
var failures = new List<string>();

void Check(bool condition, string description)
{
    checks++;
    if (condition)
    {
        Console.WriteLine($"PASS {description}");
        return;
    }

    failures.Add(description);
    Console.Error.WriteLine($"FAIL {description}");
}

Check(TaskbookScoring.CalculatePercentageScore(0, 0) == 0, "an empty test set receives no score");
Check(TaskbookScoring.CalculatePercentageScore(1, 2) == 50, "one of two tests receives 50 points");
Check(TaskbookScoring.CalculatePercentageScore(1, 3) == 33, "one of three tests rounds to 33");
Check(TaskbookScoring.CalculatePercentageScore(2, 3) == 67, "two of three tests round to 67");
Check(TaskbookScoring.CalculatePercentageScore(1, 8) == 13, "half-point percentages round away from zero");
Check(TaskbookScoring.CalculatePercentageScore(8, 3) == 100, "passed test count is clamped to the total");

var percentageTask = new TaskbookTaskDto
{
    ScoringMode = TaskScoringMode.Percentage,
    BonusPointsForAllTests = 1000,
    Tests = [new TestCaseDto { Points = 500 }, new TestCaseDto { Points = 500 }],
};
Check(TaskbookScoring.GetTaskMaxScore(percentageTask) == 100, "percentage score ignores legacy weights and bonuses");
percentageTask.Tests.Clear();
Check(TaskbookScoring.GetTaskMaxScore(percentageTask) == 0, "tasks without tests have a zero maximum");

var legacyDocument = TaskbookIO.Deserialize(
    """{"tasks":[{"bonusPointsForAllTests":10,"tests":[{"input":"","output":"","points":25}]}]}""");
Check(legacyDocument.Tasks[0].ScoringMode == TaskScoringMode.WeightedPoints, "old taskbooks keep weighted scoring by default");
Check(TaskbookScoring.GetTaskMaxScore(legacyDocument.Tasks[0]) == 35, "legacy test points and all-tests bonus remain unchanged");

var tempDirectory = Path.Combine(Path.GetTempPath(), "pvz-settings-tests", Guid.NewGuid().ToString("N"));
var settingsPath = Path.Combine(tempDirectory, "settings.json");
try
{
    var settings = new AppSettings
    {
        Theme = AppTheme.Dark,
        AccentColor = "#B04AFF",
        MusicFilePath = Path.Combine(tempDirectory, "track.mp3"),
        MusicEnabled = true,
        MusicVolume = 72,
    };
    SettingsStore.Save(settings, settingsPath);
    var loadedSettings = SettingsStore.Load(settingsPath);
    Check(
        loadedSettings.Theme == AppTheme.Dark &&
        loadedSettings.AccentColor == "#B04AFF" &&
        loadedSettings.MusicFilePath == settings.MusicFilePath &&
        loadedSettings.MusicEnabled &&
        loadedSettings.MusicVolume == 72,
        "theme, accent, music path and volume survive a settings round trip");

    settings.MusicVolume = 200;
    SettingsStore.Save(settings, settingsPath);
    Check(SettingsStore.Load(settingsPath).MusicVolume == 100, "saved music volume is clamped to the slider range");

    var invalidPath = Path.Combine(tempDirectory, "invalid.json");
    await File.WriteAllTextAsync(invalidPath, "{");
    var invalidSettingsRejected = false;
    try
    {
        _ = SettingsStore.Load(invalidPath);
    }
    catch (InvalidDataException)
    {
        invalidSettingsRejected = true;
    }
    Check(invalidSettingsRejected, "malformed settings fail explicitly");
}
finally
{
    if (Directory.Exists(tempDirectory))
        Directory.Delete(tempDirectory, recursive: true);
}

var runtimes = SolutionRunner.DetectRuntimes();
Check(runtimes.Select(runtime => runtime.Language).Distinct().Count() == 5, "all five language choices are represented");
foreach (var runtime in runtimes)
    Console.WriteLine($"Runtime: {runtime}");
var oneC = runtimes.Single(runtime => runtime.Language == SolutionLanguage.OneC);
Check(!oneC.IsAvailable && oneC.Status.Contains("не поддерживается", StringComparison.OrdinalIgnoreCase), "1C is marked unsupported");
var oneCRun = await SolutionRunner.RunAllAsync(oneC, "", [""]);
Check(!oneCRun.RuntimeAvailable && oneCRun.Tests.Count == 0, "unsupported 1C cannot report a passing test");

var missingMusicPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp3");
var musicStarted = BackgroundMusicPlayer.TryPlay(missingMusicPath, 50, out var musicError);
Check(!musicStarted && musicError?.Contains("не найден", StringComparison.OrdinalIgnoreCase) == true, "a missing MP3 reports its missing-file path");

var samplePrograms = new Dictionary<SolutionLanguage, string>
{
    [SolutionLanguage.Python] = "a, b = map(int, input().split()); print(a + b)",
    [SolutionLanguage.Cpp] = """
        #include <iostream>
        int main() { int a, b; std::cin >> a >> b; std::cout << a + b << '\n'; }
        """,
    [SolutionLanguage.CSharp] = """
        using System;
        var values = Console.ReadLine()!.Split(' ');
        Console.WriteLine(int.Parse(values[0]) + int.Parse(values[1]));
        """,
    [SolutionLanguage.Pascal] = """
        var a, b: Integer;
        begin
          read(a, b);
          writeln(a + b);
        end.
        """,
};
foreach (var runtime in runtimes.Where(runtime => runtime.IsAvailable && samplePrograms.ContainsKey(runtime.Language)))
{
    var batch = await SolutionRunner.RunAllAsync(runtime, samplePrograms[runtime.Language], ["2 3", "-4 7"]);
    Check(batch.RuntimeAvailable, $"{runtime.DisplayName} runtime prepares source code");
    Check(
        batch.Tests.Count == 2 &&
        batch.Tests[0].Success &&
        PythonRunner.NormalizeOutput(batch.Tests[0].StandardOutput) == "5" &&
        batch.Tests[1].Success &&
        PythonRunner.NormalizeOutput(batch.Tests[1].StandardOutput) == "3",
        $"{runtime.DisplayName} executes multiple stdin/stdout cases correctly");
}

var csharpRuntime = runtimes.Single(runtime => runtime.Language == SolutionLanguage.CSharp);
if (csharpRuntime.IsAvailable)
{
    var invalidBatch = await SolutionRunner.RunAllAsync(csharpRuntime, "public class {", [""]);
    Check(
        invalidBatch.RuntimeAvailable &&
        invalidBatch.ErrorMessage?.Contains("скомпилировать", StringComparison.OrdinalIgnoreCase) == true &&
        invalidBatch.Tests.All(test => !test.Success),
        "a C# compilation error fails all cases instead of receiving a pass");
}
else
{
    Console.WriteLine($"SKIP C# execution integration: {csharpRuntime.Status}");
}

Console.WriteLine($"{checks - failures.Count}/{checks} checks passed.");
if (failures.Count > 0)
    Environment.ExitCode = 1;
