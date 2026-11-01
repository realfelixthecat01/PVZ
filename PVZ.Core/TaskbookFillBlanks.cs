// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using System.Text;
using System.Text.RegularExpressions;

namespace PVZ.Core;

/// <summary>
/// Правила заданий с пропусками: каждый непрерывный ряд подчёркиваний («_», «___»)
/// считается одним пропуском. Сравнение ответов не зависит от регистра, лишних
/// пробелов и вида апострофа.
/// </summary>
public static partial class FillBlankRules
{
    [GeneratedRegex("_+")]
    public static partial Regex BlankPattern { get; }

    public static int CountBlanks(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : BlankPattern.Matches(text).Count;

    /// <summary>
    /// Делит текст с пропусками на части: segments[i] стоит перед пропуском i,
    /// segments[^1] идёт после последнего пропуска. Всегда blanks + 1 элементов.
    /// </summary>
    public static string[] SplitSegments(string? text) =>
        BlankPattern.Split(text ?? "");

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;
        foreach (var character in value)
        {
            var current = character switch
            {
                '\u2019' or '\u2018' or '\u02bc' or '`' or '\u00b4' => '\'',
                '\u00a0' => ' ',
                _ => character,
            };

            if (char.IsWhiteSpace(current))
            {
                if (!previousWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
                continue;
            }

            builder.Append(char.ToLowerInvariant(current));
            previousWasSpace = false;
        }

        if (builder.Length > 0 && builder[^1] == ' ')
            builder.Length--;

        return builder.ToString();
    }

    /// <summary>
    /// Проверяет ответ ученика: пустой ответ не принимается, сравнение идёт по
    /// нормализованным вариантам (Answer + AcceptedAnswers).
    /// </summary>
    public static bool Matches(FillBlankDto blank, string? entered)
    {
        var value = Normalize(entered);
        if (value.Length == 0)
            return false;

        if (Normalize(blank.Answer) == value)
            return true;

        foreach (var variant in blank.AcceptedAnswers)
        {
            if (Normalize(variant) == value)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Разбирает строку вида «went | go | goes» на основной ответ и дополнительные
    /// варианты. Используется редактором PVZT при вводе ответов через «|».
    /// </summary>
    public static (string Answer, string[] ExtraVariants) SplitAnswerVariants(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return ("", []);

        var parts = raw.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return ("", []);

        var answer = parts[0];
        var extras = new List<string>();
        for (var index = 1; index < parts.Length; index++)
        {
            var variant = parts[index];
            if (!string.Equals(Normalize(variant), Normalize(answer), StringComparison.Ordinal) &&
                extras.All(existing => !string.Equals(Normalize(existing), Normalize(variant), StringComparison.Ordinal)))
                extras.Add(variant);
        }

        return (answer, extras.ToArray());
    }

    /// <summary>
    /// Приводит список пропусков к количеству «_» в тексте: лишние обрезаются,
    /// недостающие добавляются. Возвращает true, если список изменился.
    /// </summary>
    public static bool SyncBlanks(List<FillBlankDto> blanks, string? text)
    {
        var count = CountBlanks(text);
        var changed = false;

        while (blanks.Count > count)
        {
            blanks.RemoveAt(blanks.Count - 1);
            changed = true;
        }

        while (blanks.Count < count)
        {
            blanks.Add(new FillBlankDto());
            changed = true;
        }

        return changed;
    }
}
