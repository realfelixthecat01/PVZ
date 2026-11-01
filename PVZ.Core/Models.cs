// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

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

/// <summary>
/// Тип задания. CodeTests: обычная задача с кодом и тестами.
/// FillInBlanks: текст с пропусками «_», ученик вписывает слова, приложение проверяет.
/// Choice: вопрос с вариантами, ученик отмечает верные, неверная отметка снимает баллы.
/// Matching: пары, ученик подбирает правую часть к каждой левой.
/// ShortAnswer: вопрос, на который отвечают словом или числом в одно поле.
/// </summary>
public enum TaskbookTaskKind
{
    CodeTests = 0,
    FillInBlanks = 1,
    Choice = 2,
    Matching = 3,
    ShortAnswer = 4,
}

/// <summary>Один вариант в задании с выбором. Баллы дают верные отмеченные варианты, отмеченный зря отнимает столько же.</summary>
public sealed class ChoiceOptionDto
{
    public string Text { get; set; } = "";
    public bool Correct { get; set; }
    public int Points { get; set; } = 25;
}

/// <summary>Одна пара в задании на соответствие. Порядок пар в списке и есть правильное соотнесение.</summary>
public sealed class MatchPairDto
{
    public string Left { get; set; } = "";
    public string Right { get; set; } = "";
    public int Points { get; set; } = 25;
}

/// <summary>
/// Один ответ для проверки: пропуск «_» в тексте задания или единственный ответ
/// короткого задания. BlankPattern считает каждый непрерывный
/// ряд подчёркиваний одним пропуском, поэтому порядок Blanks совпадает с порядком «_» в тексте.
/// </summary>
public sealed class FillBlankDto
{
    public string Answer { get; set; } = "";
    public List<string> AcceptedAnswers { get; set; } = new();
    public int Points { get; set; } = 25;
    public bool ShowToStudent { get; set; } = true;
}

public sealed class TaskbookTaskDto
{
    public string Description { get; set; } = "";
    public int BonusPointsForAllTests { get; set; } = 10;
    public List<TestCaseDto> Tests { get; set; } = new();

    // Enum по умолчанию 0 = CodeTests, поэтому старые задачники открываются без изменений.
    public TaskbookTaskKind Kind { get; set; } = TaskbookTaskKind.CodeTests;

    public List<FillBlankDto> Blanks { get; set; } = new();

    public List<ChoiceOptionDto> Options { get; set; } = new();

    public List<MatchPairDto> Pairs { get; set; } = new();
}

public sealed class TaskbookDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string Subject { get; set; } = TaskbookCatalogSubjects.Informatics;
    public int ClassNumber { get; set; } = 6;
    public string TeacherPasswordHash { get; set; } = "";
    public string TeacherPasswordSalt { get; set; } = "";
    public bool SequentialUnlock { get; set; }
    public bool TopMost { get; set; }
    public bool FullScreen { get; set; }
    public bool KillExplorer { get; set; }
    public TaskbookCompilerFlags AllowedCompilers { get; set; } = TaskbookCompilerFlags.All;
    public List<TaskbookTaskDto> Tasks { get; set; } = new();
}

public static class TaskbookCatalogSubjects
{
    public const string Informatics = "informatics";
    public const string English = "english";
    public const string Math = "math";

    public static IReadOnlyList<TaskbookCatalogSubject> All { get; } =
    [
        new(Informatics, "Информатика"),
        new(English, "Английский язык"),
        new(Math, "Математика"),
    ];

    public static bool IsValid(string? subject) =>
        All.Any(item => string.Equals(item.Id, subject, StringComparison.OrdinalIgnoreCase));

    public static int MinimumClassNumber(string subject) => subject.ToLowerInvariant() switch
    {
        Informatics => 6,
        English => 2,
        Math => 1,
        _ => throw new ArgumentException("Неизвестный предмет.", nameof(subject)),
    };

    public static bool IsValidClass(string subject, int classNumber) =>
        IsValid(subject) && classNumber >= MinimumClassNumber(subject) && classNumber <= 11;
}

public sealed record TaskbookCatalogSubject(string Id, string DisplayName);

public sealed record TaskbookCatalogItemDto(string FileName, long SizeBytes);

public sealed record OnlineStudentSession(string SessionId, DateTimeOffset StartedAt);

public sealed record StudentTaskProgressDto(int EarnedPoints, bool Completed);

public sealed record OnlineProgressUpdate(string SessionId, IReadOnlyList<StudentTaskProgressDto> Tasks);

public sealed record OnlineStudentInfo(string FirstName, string LastName, string ClassName);

public sealed record ScreenWatchStateDto(bool Watching);

public sealed record TeacherLoginReceipt(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record TeacherSessionUploadReceipt(string Id, string Token, DateTimeOffset ExpiresAt, RetentionPeriod Retention);

public sealed record SessionExtendReceipt(DateTimeOffset ExpiresAt);

public sealed record AccountAuthReceipt(string AccessToken, DateTimeOffset ExpiresAt, AccountProfileDto Profile);

public sealed record SessionStudentDto(
    string FirstName,
    string LastName,
    string ClassName,
    DateTimeOffset StartedAt,
    DateTimeOffset LastActivityAt,
    long ElapsedSeconds,
    int EarnedPoints,
    int MaximumPoints,
    int CompletedTasks,
    int TotalTasks,
    bool IsOnline,
    IReadOnlyList<StudentTaskProgressDto> Tasks,
    string SessionId = "");

public sealed record TeacherSessionDto(
    string Id,
    string TokenHint,
    DateTimeOffset UploadedAt,
    DateTimeOffset ExpiresAt,
    int TotalTasks,
    int MaximumPoints,
    RetentionPeriod Retention,
    IReadOnlyList<SessionStudentDto> Students,
    long? OwnerId = null,
    string? OwnerName = null);

public sealed record SessionTaskStatDto(
    int Number,
    string Title,
    int MaxPoints,
    int Solved,
    int Partially,
    int Untouched,
    double AveragePoints,
    double AveragePercent);

public sealed record SessionBestStudentDto(string Name, string ClassName, int EarnedPoints);

public sealed record SessionStatsDto(
    string SessionId,
    string TokenHint,
    DateTimeOffset UploadedAt,
    DateTimeOffset ExpiresAt,
    int TotalTasks,
    int MaximumPoints,
    int Students,
    int Online,
    int SolvedEverything,
    double AveragePercent,
    SessionBestStudentDto? BestStudent,
    IReadOnlyList<SessionTaskStatDto> Tasks);

/// <summary>Аккаунт учителя, подтверждённый локальным токеном.</summary>
public sealed record AccountPrincipal(long UserId, string Username, string Email, string FullName, bool IsAdmin, string Kind);

/// <summary>Занятость одной корзины хранения. Limit равен -1, когда ограничений нет.</summary>
public sealed record StorageBucketDto(RetentionPeriod Retention, int Used, int Limit);

public sealed record AccountProfileDto(
    long Id,
    string Username,
    string Email,
    string FullName,
    string School,
    bool IsAdmin,
    IReadOnlyList<StorageBucketDto> Storage,
    string Role = "teacher",
    bool IsGuest = false,
    string ClassName = "");

public sealed record StudentHistoryItemDto(
    string SessionId,
    string Subject,
    int? ClassNumber,
    DateTimeOffset UploadedAt,
    DateTimeOffset UpdatedAt,
    int EarnedPoints,
    int MaximumPoints,
    int CompletedTasks,
    int TotalTasks,
    string TeacherName);

/// <summary>Кому принадлежит аккаунт: учитель публикует задачники, ученик решает их.</summary>
public static class AccountRoles
{
    public const string Teacher = "teacher";
    public const string Student = "student";
}

public sealed record AdminTeacherDto(
    long Id,
    string Username,
    string Email,
    string FullName,
    string School,
    IReadOnlyList<TeacherSessionDto> Sessions);

public static class ExchangeFileKinds
{
    public const string Material = "material";
    public const string Work = "work";

    public static string DisplayName(string kind) => kind == Work ? "Работа ученика" : "Материал";
}

public sealed record ExchangeFileDto(
    string Id,
    string Name,
    string Kind,
    long SizeBytes,
    string? StudentName,
    DateTimeOffset UploadedAt);

/// <summary>Что видит ученик: все материалы задачника и только свои работы.</summary>
public sealed record StudentExchangeFilesDto(
    IReadOnlyList<ExchangeFileDto> Materials,
    IReadOnlyList<ExchangeFileDto> Works);

public sealed record CabinetFileDto(
    string Id,
    string TokenHint,
    DateTimeOffset UploadedAt,
    DateTimeOffset ExpiresAt,
    RetentionPeriod Retention,
    int TotalTasks,
    int MaximumPoints,
    int StudentsCount);

/// <summary>
/// Срок хранения онлайн-задачника. Задачник учителя лежит две недели, гостевой хранится 48 часов.
/// </summary>
public enum RetentionPeriod
{
    Hours48 = 0,
    Week1 = 1,
    Month1 = 2,
    Year1 = 3,
    Forever = 4,
    TwoWeeks = 5,
}

public static class RetentionPeriods
{
    public static IReadOnlyList<RetentionPeriod> All { get; } =
    [
        RetentionPeriod.Hours48,
        RetentionPeriod.Week1,
        RetentionPeriod.Month1,
        RetentionPeriod.Year1,
        RetentionPeriod.Forever,
        RetentionPeriod.TwoWeeks,
    ];

    public static TimeSpan Lifetime(RetentionPeriod retention) => retention switch
    {
        RetentionPeriod.Hours48 => TimeSpan.FromHours(48),
        RetentionPeriod.Week1 => TimeSpan.FromDays(7),
        RetentionPeriod.Month1 => TimeSpan.FromDays(30),
        RetentionPeriod.Year1 => TimeSpan.FromDays(365),
        // «Навсегда» храним сто лет, этого хватит любому школьному архиву.
        RetentionPeriod.Forever => TimeSpan.FromDays(36500),
        _ => TimeSpan.FromDays(14),
    };

    public static string DisplayName(RetentionPeriod retention) => retention switch
    {
        RetentionPeriod.Hours48 => "Хранение · 48 часов",
        RetentionPeriod.Week1 => "Хранение · 1 неделя",
        RetentionPeriod.Month1 => "Хранение · 1 месяц",
        RetentionPeriod.Year1 => "Хранение · 1 год",
        RetentionPeriod.Forever => "Постоянное хранение",
        _ => "Хранение · 2 недели",
    };

    public static string ShortLabel(RetentionPeriod retention) => retention switch
    {
        RetentionPeriod.Hours48 => "48 ч",
        RetentionPeriod.Week1 => "1 нед",
        RetentionPeriod.Month1 => "1 мес",
        RetentionPeriod.Year1 => "1 год",
        RetentionPeriod.Forever => "∞",
        _ => "2 нед",
    };
}
