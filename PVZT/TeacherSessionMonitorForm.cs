// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using PVZ.Core;

namespace PVZ.Player;

internal sealed class TeacherSessionMonitorForm : Form
{
    private readonly Label _status = new()
    {
        AutoSize = true,
        ForeColor = PvzTheme.Muted,
        Margin = new Padding(0, 0, 0, 8),
    };
    private readonly ListBox _sessionsList = new()
    {
        Dock = DockStyle.Fill,
        IntegralHeight = false,
        BorderStyle = BorderStyle.None,
        BackColor = PvzTheme.Surface,
        ForeColor = PvzTheme.Ink,
        ItemHeight = 22,
    };
    private readonly ListView _students = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = false,
        HideSelection = false,
        MultiSelect = false,
        BorderStyle = BorderStyle.None,
        BackColor = PvzTheme.Surface,
        ForeColor = PvzTheme.Ink,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
    };
    private readonly TextBox _taskDetails = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        BackColor = PvzTheme.Surface,
        ForeColor = PvzTheme.Ink,
        BorderStyle = BorderStyle.None,
        Text = "Выберите ученика, чтобы увидеть прогресс по задачам.",
    };
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 3000 };

    private string? _serverUrl;
    private string? _accessToken;
    private int _refreshPending;

    public TeacherSessionMonitorForm()
    {
        PvzFormIcon.TryApply(this);
        Text = "Пункт Выдачи Задачек: прогресс учеников";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 620);
        ClientSize = new Size(1140, 700);
        PvzTheme.ApplyWindow(this);
        PvzTheme.EnableAutoScale(this);
        PvzTheme.FitToScreen(this);

        BuildLayout();
        _sessionsList.SelectedIndexChanged += (_, _) => RenderStudents();
        _students.SelectedIndexChanged += (_, _) => ShowTaskDetails();
        _refreshTimer.Tick += async (_, _) => await RefreshSessionsAsync();
        Shown += async (_, _) => await InitializeAsync();
        FormClosed += (_, _) => _refreshTimer.Dispose();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PvzTheme.Canvas,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "Наблюдение за прохождениями",
            AutoSize = true,
            Font = PvzTheme.Title,
            ForeColor = PvzTheme.Ink,
            Margin = new Padding(0, 0, 0, 12),
        };

        _students.Columns.Add("Ученик", 210);
        _students.Columns.Add("Класс", 90);
        _students.Columns.Add("Баллы", 100);
        _students.Columns.Add("Задачи", 100);
        _students.Columns.Add("Время", 120);
        _students.Columns.Add("Статус", 100);

        var sessionsGroup = new GroupBox
        {
            Text = "Задачники на сервере",
            Dock = DockStyle.Fill,
            ForeColor = PvzTheme.Muted,
            Padding = new Padding(8, 20, 8, 8),
            Margin = new Padding(0, 0, 8, 0),
        };
        sessionsGroup.Controls.Add(_sessionsList);

        var csvButton = PvzTheme.SecondaryButton("Выгрузить CSV");
        var statsButton = PvzTheme.SecondaryButton("Статистика");
        csvButton.AutoSize = false;
        csvButton.Width = 150;
        csvButton.Height = 34;
        statsButton.AutoSize = false;
        statsButton.Width = 140;
        statsButton.Height = 34;
        csvButton.Click += async (_, _) => await ExportResultsCsvAsync();
        statsButton.Click += async (_, _) => await ShowSessionStatsAsync();
        var reportButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 8, 0, 0),
        };
        reportButtons.Controls.AddRange([csvButton, statsButton]);
        sessionsGroup.Controls.Add(reportButtons);

        var studentsGroup = new GroupBox
        {
            Text = "Ученики выбранного задачника",
            Dock = DockStyle.Fill,
            ForeColor = PvzTheme.Muted,
            Padding = new Padding(8, 20, 8, 8),
            Margin = new Padding(0),
        };
        studentsGroup.Controls.Add(_students);

        var detailsGroup = new GroupBox
        {
            Text = "Прогресс по задачам",
            Dock = DockStyle.Fill,
            ForeColor = PvzTheme.Muted,
            Padding = new Padding(8, 20, 8, 8),
            Margin = new Padding(0, 8, 0, 0),
        };
        detailsGroup.Controls.Add(_taskDetails);

        var rightColumn = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PvzTheme.Canvas,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
        };
        rightColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        rightColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        rightColumn.Controls.Add(studentsGroup, 0, 0);
        rightColumn.Controls.Add(detailsGroup, 0, 1);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PvzTheme.Canvas,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0),
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.Controls.Add(sessionsGroup, 0, 0);
        content.Controls.Add(rightColumn, 1, 0);

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(_status, 0, 1);
        root.Controls.Add(content, 0, 2);
        Controls.Add(root);
    }

    private async Task InitializeAsync()
    {
        _status.Text = "Подключение к серверу…";
        UseWaitCursor = true;
        try
        {
            _serverUrl = await TaskbookOnlineClient.ResolveServerUrlAsync();
            _accessToken = await TeacherServerAuth.EnsureAccessTokenAsync(this, _serverUrl);
            if (_accessToken is null)
            {
                Close();
                return;
            }

            _status.Text = "Загрузка…";
            await RefreshSessionsAsync();
            _refreshTimer.Start();
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException)
        {
            _status.Text = exception.Message;
        }
        finally
        {
            if (!IsDisposed)
                UseWaitCursor = false;
        }
    }

    private async Task RefreshSessionsAsync()
    {
        if (_accessToken is null || _serverUrl is null ||
            IsDisposed || Interlocked.Exchange(ref _refreshPending, 1) != 0)
            return;

        try
        {
            var sessions = await TaskbookOnlineClient.ListTeacherSessionsAsync(_accessToken, _serverUrl);
            RenderSessions(sessions);
            var totalStudents = sessions.Sum(session => session.Students.Count);
            _status.Text = $"Задачников: {sessions.Count}, учеников: {totalStudents}. Обновлено {DateTime.Now:HH:mm:ss}.";
        }
        catch (ServerUnauthorizedException)
        {
            _accessToken = null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            _status.Text = exception.Message;
        }
        finally
        {
            Interlocked.Exchange(ref _refreshPending, 0);
        }

        if (_accessToken is null && _serverUrl is not null && !IsDisposed)
        {
            _refreshTimer.Stop();
            TeacherServerAuth.Invalidate();
            _accessToken = await TeacherServerAuth.EnsureAccessTokenAsync(this, _serverUrl);
            if (_accessToken is null)
            {
                Close();
                return;
            }

            _refreshTimer.Start();
            await RefreshSessionsAsync();
        }
    }

    private void RenderSessions(IReadOnlyList<TeacherSessionDto> sessions)
    {
        var selectedId = (_sessionsList.SelectedItem as SessionListItem)?.Session.Id;
        _sessionsList.BeginUpdate();
        _sessionsList.Items.Clear();
        foreach (var session in sessions)
        {
            var item = new SessionListItem(session);
            _sessionsList.Items.Add(item);
            if (session.Id == selectedId)
                _sessionsList.SelectedItem = item;
        }
        if (_sessionsList.SelectedItem is null && _sessionsList.Items.Count > 0)
            _sessionsList.SelectedIndex = 0;
        _sessionsList.EndUpdate();
        RenderStudents();
    }

    private void RenderStudents()
    {
        var selectedName = _students.SelectedItems.Count == 0
            ? null
            : _students.SelectedItems[0].SubItems[0].Text;
        _students.BeginUpdate();
        _students.Items.Clear();
        var detailsHeaderText = "Выберите ученика, чтобы увидеть прогресс по задачам.";
        if (_sessionsList.SelectedItem is SessionListItem listItem)
        {
            var session = listItem.Session;
            foreach (var student in session.Students)
            {
                var item = new ListViewItem($"{student.FirstName} {student.LastName}")
                {
                    Tag = student,
                };
                item.SubItems.Add(student.ClassName);
                item.SubItems.Add($"{student.EarnedPoints}/{student.MaximumPoints}");
                item.SubItems.Add($"{student.CompletedTasks}/{student.TotalTasks}");
                item.SubItems.Add(FormatDuration(student.ElapsedSeconds));
                item.SubItems.Add(student.IsOnline ? "В сети" : "Не в сети");
                _students.Items.Add(item);
                if (item.Text == selectedName)
                    item.Selected = true;
            }

            detailsHeaderText = $"Задачник с токеном {session.TokenHint} ({RetentionPeriods.DisplayName(session.Retention)}): " +
                $"задач {session.TotalTasks}, максимум {session.MaximumPoints} баллов. " +
                $"Хранится до {session.ExpiresAt.ToLocalTime():dd.MM.yyyy HH:mm}.";
        }
        _students.EndUpdate();
        if (_students.SelectedItems.Count == 0)
            _taskDetails.Text = detailsHeaderText;
        ShowTaskDetails();
    }

    private void ShowTaskDetails()
    {
        if (_students.SelectedItems.Count == 0 ||
            _students.SelectedItems[0].Tag is not SessionStudentDto student)
            return;

        var header = $"{student.FirstName} {student.LastName}, класс {student.ClassName}: {student.EarnedPoints}/{student.MaximumPoints} баллов.";
        var tasks = student.Tasks.Count == 0
            ? ["Пока нет данных о задачах."]
            : student.Tasks.Select((task, index) =>
                $"Задача {index + 1}: {task.EarnedPoints} баллов, {(task.Completed ? "выполнена" : "в процессе")}");
        _taskDetails.Text = string.Join(Environment.NewLine, tasks.Prepend(header));
    }

    private TeacherSessionDto? SelectedSession() => (_sessionsList.SelectedItem as SessionListItem)?.Session;

    private async Task ExportResultsCsvAsync()
    {
        var session = SelectedSession();
        if (session is null || _accessToken is null || _serverUrl is null)
        {
            MessageBox.Show(this, "Сначала выберите задачник в списке слева.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Выгрузить результаты учеников",
            FileName = $"pvz-rezultaty-{DateTime.Now:yyyy-MM-dd-HHmm}.csv",
            Filter = "Таблица CSV (*.csv)|*.csv",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        await RunReportAsync(async () =>
        {
            var content = await TaskbookOnlineClient.DownloadResultsCsvAsync(_accessToken, session.Id, _serverUrl);
            await File.WriteAllBytesAsync(dialog.FileName, content);
            _status.Text = $"Результаты сохранены в {dialog.FileName}.";
        });
    }

    private async Task ShowSessionStatsAsync()
    {
        var session = SelectedSession();
        if (session is null || _accessToken is null || _serverUrl is null)
        {
            MessageBox.Show(this, "Сначала выберите задачник в списке слева.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        await RunReportAsync(async () =>
        {
            var stats = await TaskbookOnlineClient.GetSessionStatsAsync(_accessToken, session.Id, _serverUrl);
            TeacherSessionStatsForm.ShowForSession(this, stats);
        });
    }

    private async Task RunReportAsync(Func<Task> action)
    {
        UseWaitCursor = true;
        try
        {
            await action();
        }
        catch (ServerUnauthorizedException)
        {
            _accessToken = null;
            await RefreshSessionsAsync();
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException
            or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
                UseWaitCursor = false;
        }
    }

    private static string FormatDuration(long seconds)
    {
        var total = Math.Max(0, (int)seconds);
        return $"{total / 60} мин {total % 60} сек";
    }

    private sealed record SessionListItem(TeacherSessionDto Session)
    {
        public override string ToString() =>
            $"Токен {Session.TokenHint} • {RetentionPeriods.ShortLabel(Session.Retention)} • до {Session.ExpiresAt.ToLocalTime():dd.MM HH:mm} • учеников: {Session.Students.Count}";
    }
}
