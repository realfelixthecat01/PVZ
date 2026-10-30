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
/// Тип задания. CodeTests — обычная задача с кодом и тестами.
/// FillInBlanks — текст с пропусками «_»: ученик вписывает слова, приложение проверяет.
/// </summary>
public enum TaskbookTaskKind
{
    CodeTests = 0,
    FillInBlanks = 1,
}

/// <summary>
/// Один пропуск «_» в тексте задания. BlankPattern считает каждый непрерывный
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
        English => 3,
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

/// <summary>Ответ на запрос/отправку кадра: идёт ли сейчас просмотр экрана ученика.</summary>
public sealed record ScreenWatchStateDto(bool Watching);

public sealed record TeacherLoginReceipt(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record TeacherSessionUploadReceipt(string Id, string Token, DateTimeOffset ExpiresAt, RetentionPeriod Retention);

public sealed record SessionExtendReceipt(DateTimeOffset ExpiresAt);

/// <summary>Ответ сервера на вход или регистрацию аккаунта учителя.</summary>
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

/// <summary>Аккаунт учителя, подтверждённый локальным токеном.</summary>
public sealed record AccountPrincipal(long UserId, string Username, string Email, string FullName, bool IsAdmin, string Kind);

public sealed record AccountSubscriptionDto(string Kind, string Subject, int? ClassNumber, DateTimeOffset ExpiresAt);

/// <summary>Занятость одной корзины хранения. Limit = -1 — без ограничений.</summary>
public sealed record StorageBucketDto(RetentionPeriod Retention, int Used, int Limit);

public sealed record AccountProfileDto(
    long Id,
    string Username,
    string Email,
    string FullName,
    string School,
    bool IsAdmin,
    string Plan,
    string PlanDisplayName,
    DateTimeOffset? PlanUntil,
    IReadOnlyList<AccountSubscriptionDto> Subscriptions,
    IReadOnlyList<StorageBucketDto> Storage);

/// <summary>Учитель со списком своих активных сессий — для админской панели.</summary>
public sealed record AdminTeacherDto(
    long Id,
    string Username,
    string Email,
    string FullName,
    string School,
    string Plan,
    string PlanDisplayName,
    DateTimeOffset? PlanUntil,
    IReadOnlyList<TeacherSessionDto> Sessions);

/// <summary>Типы файлов пересылки внутри онлайн-задачника.</summary>
public static class ExchangeFileKinds
{
    public const string Material = "material";
    public const string Work = "work";

    public static string DisplayName(string kind) => kind == Work ? "Работа ученика" : "Материал";
}

/// <summary>Файл пересылки: материал от учителя или работа ученика. Хранится на сервере рядом с задачником.</summary>
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

public sealed record ActivationKeyDto(
    string Code,
    string Kind,
    string? StoragePlan,
    int DurationDays,
    string? Subject,
    int? ClassNumber,
    DateTimeOffset CreatedAt,
    bool Used,
    DateTimeOffset? UsedAt);

/// <summary>Лимит тарифа исчерпан — сообщение предназначено учителю.</summary>
public sealed class ServerQuotaException(string message) : Exception(message);

/// <summary>
/// Срок хранения онлайн-задачника. TwoWeeks — исторический тариф настольного PVZT «Бесплатный — 2 недели».
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
        // «Навсегда» храним сто лет — этого хватит любому школьному архиву.
        RetentionPeriod.Forever => TimeSpan.FromDays(36500),
        _ => TimeSpan.FromDays(14),
    };

    public static string DisplayName(RetentionPeriod retention) => retention switch
    {
        RetentionPeriod.Hours48 => "Бесплатный · 48 часов",
        RetentionPeriod.Week1 => "Хранение · 1 неделя",
        RetentionPeriod.Month1 => "Хранение · 1 месяц",
        RetentionPeriod.Year1 => "Хранение · 1 год",
        RetentionPeriod.Forever => "Постоянное хранение",
        _ => "Бесплатный · 2 недели",
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

/// <summary>
/// Тариф хранения настольного PVZT. Сервер переводит его в RetentionPeriod:
/// «Бесплатный — 2 недели» хранит 14 дней, «Продлённый месяц» — настоящий месяц (30 дней).
/// </summary>
public enum StoragePlan
{
    FreeTwoWeeks = 0,
    PaidOneMonth = 1,
}

public static class StoragePlans
{
    public static RetentionPeriod ToRetention(StoragePlan plan) => plan switch
    {
        StoragePlan.PaidOneMonth => RetentionPeriod.Month1,
        _ => RetentionPeriod.TwoWeeks,
    };

    public static TimeSpan EffectiveLifetime(StoragePlan plan) => RetentionPeriods.Lifetime(ToRetention(plan));

    public static string DisplayName(StoragePlan plan) => plan switch
    {
        StoragePlan.PaidOneMonth => "Платный · 1 месяц",
        _ => "Бесплатный · 2 недели",
    };

    public static string ShortLabel(StoragePlan plan) => plan switch
    {
        StoragePlan.PaidOneMonth => "1 мес",
        _ => "2 нед",
    };
}

/// <summary>
/// Лимиты облачных тарифов: сколько задачников каждого срока хранения допускает тариф.
/// 0 — срок недоступен, -1 — без ограничений.
/// </summary>
public static class StorageLimits
{
    public const int Unlimited = -1;
    public const string Free = "free";
    public const string Pro = "pro";
    public const string ProPlus = "pro_plus";

    public static int LimitFor(string plan, RetentionPeriod retention) => (plan, retention) switch
    {
        (Free, RetentionPeriod.Hours48) => 5,
        (Pro, RetentionPeriod.Hours48) => 20,
        (Pro, RetentionPeriod.Week1) => 15,
        (Pro, RetentionPeriod.Month1) => 5,
        (Pro, RetentionPeriod.Year1) => 1,
        (ProPlus, RetentionPeriod.Hours48) => Unlimited,
        (ProPlus, RetentionPeriod.Week1) => Unlimited,
        (ProPlus, RetentionPeriod.Month1) => Unlimited,
        (ProPlus, RetentionPeriod.Year1) => 100,
        (ProPlus, RetentionPeriod.Forever) => 20,
        _ => 0,
    };

    public static IReadOnlyList<RetentionPeriod> RetentionsFor(string plan) => plan switch
    {
        Pro => [RetentionPeriod.Hours48, RetentionPeriod.Week1, RetentionPeriod.Month1, RetentionPeriod.Year1],
        ProPlus => [RetentionPeriod.Hours48, RetentionPeriod.Week1, RetentionPeriod.Month1, RetentionPeriod.Year1, RetentionPeriod.Forever],
        _ => [RetentionPeriod.Hours48],
    };

    public static string PlanDisplayName(string plan) => plan switch
    {
        Pro => "Про",
        ProPlus => "Про+",
        _ => "Бесплатный",
    };
}
