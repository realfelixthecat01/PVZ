// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using PVZ.Core;
using PVZ.Player;

namespace PVZ;

/// <summary>
/// Сводка по заданиям онлайн-задачника: сервер уже посчитал, сколько учеников решили
/// каждую задачу и какой у неё средний балл. Здесь эти числа только показываются.
/// </summary>
internal sealed class TeacherSessionStatsForm : Form
{
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = PvzTheme.Surface,
        BorderStyle = BorderStyle.None,
        EnableHeadersVisualStyles = false,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = PvzTheme.RaisedSurface,
            ForeColor = PvzTheme.Ink,
        },
        DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = PvzTheme.Surface,
            ForeColor = PvzTheme.Ink,
            SelectionBackColor = PvzTheme.Accent,
        },
    };

    private TeacherSessionStatsForm(SessionStatsDto stats)
    {
        PvzFormIcon.TryApply(this);
        Text = $"Статистика задачника {stats.TokenHint}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 460);
        ClientSize = new Size(900, 560);
        PvzTheme.ApplyWindow(this);
        PvzTheme.EnableAutoScale(this);
        PvzTheme.FitToScreen(this);

        _grid.Columns.Add("Number", "№");
        _grid.Columns.Add("Task", "Задание");
        _grid.Columns.Add("Max", "Максимум");
        _grid.Columns.Add("Solved", "Решили");
        _grid.Columns.Add("Partial", "Частично");
        _grid.Columns.Add("Untouched", "Не начинали");
        _grid.Columns.Add("Average", "Средний балл");
        _grid.Columns.Add("Percent", "Средний %");
        foreach (var task in stats.Tasks)
        {
            _grid.Rows.Add(
                task.Number,
                task.Title,
                task.MaxPoints,
                task.Solved,
                task.Partially,
                task.Untouched,
                task.AveragePoints.ToString("0.0"),
                $"{task.AveragePercent:0.#}%");
        }

        var summary = new Label
        {
            Text = BuildSummary(stats),
            AutoSize = false,
            Height = 62,
            Dock = DockStyle.Fill,
            ForeColor = PvzTheme.Muted,
            Margin = new Padding(0, 0, 0, 10),
        };

        var closeButton = PvzTheme.GhostButton("Закрыть");
        closeButton.Size = new Size(110, 36);
        closeButton.DialogResult = DialogResult.OK;
        var buttonRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
        };
        buttonRow.Controls.Add(closeButton);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 1,
            RowCount = 3,
            BackColor = PvzTheme.Canvas,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.Controls.Add(summary, 0, 0);
        root.Controls.Add(_grid, 0, 1);
        root.Controls.Add(buttonRow, 0, 2);

        AcceptButton = closeButton;
        CancelButton = closeButton;
        Controls.Add(root);
    }

    private static string BuildSummary(SessionStatsDto stats)
    {
        var lines = new List<string>
        {
            $"Учеников: {stats.Students}, в сети сейчас: {stats.Online}, решили все задач: {stats.SolvedEverything}.",
            $"Средний процент выполнения: {stats.AveragePercent:0.#}% из {stats.MaximumPoints} баллов, задачник хранится до {stats.ExpiresAt.ToLocalTime():dd.MM.yyyy HH:mm}.",
        };
        if (stats.BestStudent is not null)
        {
            lines.Add($"Лучший результат: {stats.BestStudent.Name} ({stats.BestStudent.ClassName}), {stats.BestStudent.EarnedPoints} баллов.");
        }
        if (stats.Students == 0)
        {
            lines[0] = "Ученики ещё не открывали этот задачник.";
        }
        return string.Join(Environment.NewLine, lines);
    }

    public static void ShowForSession(IWin32Window owner, SessionStatsDto stats)
    {
        using var form = new TeacherSessionStatsForm(stats);
        form.ShowDialog(owner);
    }
}
