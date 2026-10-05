using PVZ.Core;
using PVZ.Player;

namespace PVZ;

internal sealed class TeacherOnlineCabinetForm : Form
{
    private static readonly Color Canvas = Color.FromArgb(17, 24, 39);
    private static readonly Color Surface = Color.FromArgb(31, 41, 55);
    private static readonly Color Ink = Color.FromArgb(226, 232, 240);
    private static readonly Color Muted = Color.FromArgb(148, 163, 184);
    private static readonly Color Accent = Color.FromArgb(37, 99, 235);

    private readonly TaskbookDocument _document;
    private readonly Label _status = new() { AutoSize = false, Height = 44, ForeColor = Muted };
    private readonly TextBox _token = new()
    {
        ReadOnly = true,
        Font = new Font("Consolas", 22F, FontStyle.Bold),
        TextAlign = HorizontalAlignment.Center,
        Height = 48,
    };
    private readonly Label _tokenHint = new()
    {
        AutoSize = false,
        Height = 28,
        ForeColor = Muted,
        Text = "Передайте ученикам этот токен (действует 48 часа).",
    };
    private readonly DataGridView _studentsGrid = CreateGrid();
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 3000 };

    private string? _serverUrl;
    private string? _apiKey;

    private TeacherOnlineCabinetForm(TaskbookDocument document)
    {
        PvzFormIcon.TryApply(this);
        _document = document;
        Text = "Онлайн-задачник";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(720, 520);
        ClientSize = new Size(880, 620);
        BackColor = Canvas;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 10F);

        BuildLayout();
        _refreshTimer.Tick += async (_, _) => await RefreshStudentsAsync(silent: true);
        Shown += async (_, _) => await InitializeAsync();
        FormClosed += (_, _) => _refreshTimer.Stop();
    }

    public static void ShowForUpload(IWin32Window owner, TaskbookDocument document)
    {
        using var form = new TeacherOnlineCabinetForm(document);
        form.ShowDialog(owner);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(20),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _status.Dock = DockStyle.Top;
        _token.Dock = DockStyle.Top;
        _tokenHint.Dock = DockStyle.Top;

        ConfigureStudentsGrid();
        var studentsGroup = new GroupBox
        {
            Text = "Результаты учеников",
            Dock = DockStyle.Fill,
            ForeColor = Ink,
            Padding = new Padding(12, 24, 12, 8),
        };
        var studentsNote = new Label
        {
            Text = "Обновление каждые 3 с.",
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = Muted,
        };
        _studentsGrid.Dock = DockStyle.Fill;
        studentsGroup.Controls.Add(_studentsGrid);
        studentsGroup.Controls.Add(studentsNote);

        root.Controls.Add(_status, 0, 0);
        root.Controls.Add(_token, 0, 1);
        root.Controls.Add(_tokenHint, 0, 2);
        root.Controls.Add(studentsGroup, 0, 3);

        var close = new Button
        {
            Text = "Закрыть",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.White,
        };
        close.FlatAppearance.BorderColor = Accent;
        var closeRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
        };
        closeRow.Controls.Add(close);
        root.Controls.Add(closeRow, 0, 4);
        AcceptButton = close;

        Controls.Add(root);
        _token.BackColor = Color.FromArgb(24, 34, 48);
        _token.ForeColor = Ink;
        _token.BorderStyle = BorderStyle.FixedSingle;
    }

    private static DataGridView CreateGrid() => new()
    {
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Surface,
        BorderStyle = BorderStyle.None,
        EnableHeadersVisualStyles = false,
        ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(38, 50, 68), ForeColor = Ink },
        DefaultCellStyle = new DataGridViewCellStyle { BackColor = Surface, ForeColor = Ink, SelectionBackColor = Accent },
    };

    private void ConfigureStudentsGrid()
    {
        _studentsGrid.Columns.Add("Student", "Ученик");
        _studentsGrid.Columns.Add("ClassName", "Класс");
        _studentsGrid.Columns.Add("Points", "Баллы");
        _studentsGrid.Columns.Add("Tasks", "Задачи");
        _studentsGrid.Columns.Add("Elapsed", "Время");
        _studentsGrid.Columns.Add("Online", "Статус");
    }

    private async Task InitializeAsync()
    {
        _status.Text = "Подключение к серверу…";
        UseWaitCursor = true;
        Enabled = false;
        try
        {
            _serverUrl = await TaskbookOnlineClient.ResolveServerUrlAsync();
            _apiKey = await TeacherOnlineCredentials.EnsureApiKeyAsync(_serverUrl);
            _status.Text = "Загрузка задачника…";
            var result = await UploadWithFreshTeacherKeyIfNeededAsync();
            if (!result.Success)
            {
                _status.Text = result.ErrorMessage ?? "Не удалось загрузить задачник.";
                return;
            }

            _token.Text = result.AccessToken ?? "";
            var expires = result.TokenExpiresAt?.ToLocalTime();
            _tokenHint.Text = expires is null
                ? "Передайте ученикам этот токен."
                : $"Передайте ученикам этот токен. Действует до {expires:dd.MM.yyyy HH:mm}.";
            _status.Text = "Задачник на сервере. Ожидаем учеников…";
            _refreshTimer.Start();
            await RefreshStudentsAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException)
        {
            _status.Text = ex.Message;
        }
        finally
        {
            Enabled = true;
            UseWaitCursor = false;
        }
    }

    private async Task<TaskbookPublishResult> UploadWithFreshTeacherKeyIfNeededAsync()
    {
        if (_apiKey is null || _serverUrl is null)
            return new TaskbookPublishResult { Success = false, ErrorMessage = "Сервер не настроен." };

        var result = await TaskbookPublisher.UploadOnlineAsync(_document, _serverUrl, _apiKey);
        if (result.Success || !IsStaleTeacherKeyError(result.ErrorMessage))
            return result;

        TeacherOnlineCredentials.ClearStoredApiKey();
        _apiKey = await TeacherOnlineCredentials.EnsureApiKeyAsync(_serverUrl);
        return await TaskbookPublisher.UploadOnlineAsync(_document, _serverUrl, _apiKey);
    }

    private static bool IsStaleTeacherKeyError(string? message) =>
        message is not null &&
        message.Contains("Ключ учителя неверен", StringComparison.Ordinal);

    private async Task RefreshStudentsAsync(bool silent = false)
    {
        if (_apiKey is null || string.IsNullOrWhiteSpace(_serverUrl))
            return;

        if (!silent)
        {
            UseWaitCursor = true;
            Enabled = false;
        }

        try
        {
            var sessions = await TaskbookOnlineClient.ListStudentProgressAsync(_apiKey, _serverUrl);
            _studentsGrid.Rows.Clear();
            foreach (var session in sessions)
            {
                _studentsGrid.Rows.Add(
                    $"{session.FirstName} {session.LastName}",
                    session.ClassName,
                    $"{session.EarnedPoints}/{session.MaximumPoints}",
                    $"{session.CompletedTasks}/{session.TotalTasks}",
                    FormatDuration(session.ElapsedSeconds),
                    session.IsOnline ? "В сети" : "Не в сети");
            }

            if (!silent)
                _status.Text = $"Учеников: {sessions.Count}. Обновлено {DateTime.Now:HH:mm:ss}.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            if (!silent)
                _status.Text = ex.Message;
        }
        finally
        {
            if (!silent)
            {
                Enabled = true;
                UseWaitCursor = false;
            }
        }
    }

    private static string FormatDuration(long seconds)
    {
        var total = Math.Max(0, (int)seconds);
        return $"{total / 60} мин {total % 60} сек";
    }
}
