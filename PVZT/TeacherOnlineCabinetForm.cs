// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using PVZ.Core;
using PVZ.Player;

namespace PVZ;

internal sealed class TeacherOnlineCabinetForm : Form
{
    private readonly TaskbookDocument _document;
    private readonly Label _status = new() { AutoSize = false, Height = 44, ForeColor = PvzTheme.Muted };
    private readonly TextBox _token = new()
    {
        ReadOnly = true,
        Font = PvzTheme.Mono,
        TextAlign = HorizontalAlignment.Center,
        AutoSize = false,
        Height = 48,
        BackColor = PvzTheme.Elevated,
        ForeColor = PvzTheme.Ink,
        BorderStyle = BorderStyle.FixedSingle,
    };
    private readonly Label _tokenHint = new()
    {
        AutoSize = false,
        Height = 28,
        ForeColor = PvzTheme.Muted,
        Text = "Передайте ученикам этот токен.",
    };
    private readonly DataGridView _studentsGrid = CreateGrid();
    private readonly DataGridView _filesGrid = CreateGrid();
    private readonly List<ExchangeFileDto> _files = new();
    private readonly System.Windows.Forms.Timer _refreshTimer = new() { Interval = 3000 };

    private Button? _copyButton;
    private string? _serverUrl;
    private string? _accessToken;
    private string? _sessionId;
    private int _refreshPending;

    private TeacherOnlineCabinetForm(TaskbookDocument document)
    {
        PvzFormIcon.TryApply(this);
        _document = document;
        Text = "Онлайн-задачник";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 600);
        ClientSize = new Size(940, 780);
        PvzTheme.ApplyWindow(this);
        PvzTheme.EnableAutoScale(this);
        PvzTheme.FitToScreen(this);

        BuildLayout();
        _refreshTimer.Tick += async (_, _) => await RefreshStudentsAsync(silent: true);
        Shown += async (_, _) => await InitializeAsync();
        FormClosed += (_, _) => _refreshTimer.Dispose();
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
            RowCount = 7,
            Padding = new Padding(20),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _status.Dock = DockStyle.Top;
        _token.Dock = DockStyle.Top;
        _tokenHint.Dock = DockStyle.Top;

        _copyButton = PvzTheme.SecondaryButton("Скопировать токен");
        _copyButton.AutoSize = false;
        _copyButton.Width = 190;
        _copyButton.Height = 34;
        _copyButton.Click += (_, _) =>
        {
            if (_token.Text.Length == 0)
                return;
            Clipboard.SetText(_token.Text);
            _tokenHint.Text = "Токен скопирован в буфер обмена.";
        };
        var tokenRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 4, 0, 0),
        };
        tokenRow.Controls.Add(_copyButton);

        ConfigureStudentsGrid();
        var studentsGroup = new GroupBox
        {
            Text = "Результаты учеников",
            Dock = DockStyle.Fill,
            ForeColor = PvzTheme.Ink,
            Padding = new Padding(12, 24, 12, 8),
        };
        var studentsNote = new Label
        {
            Text = "Обновление каждые 3 с.",
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = PvzTheme.Subtle,
            Font = PvzTheme.Caption,
        };
        _studentsGrid.Dock = DockStyle.Fill;
        var csvButton = PvzTheme.SecondaryButton("Выгрузить CSV");
        var statsButton = PvzTheme.SecondaryButton("Статистика");
        csvButton.AutoSize = false;
        csvButton.Width = 150;
        csvButton.Height = 32;
        statsButton.AutoSize = false;
        statsButton.Width = 140;
        statsButton.Height = 32;
        csvButton.Click += async (_, _) => await ExportResultsCsvAsync();
        statsButton.Click += async (_, _) => await ShowSessionStatsAsync();
        var studentsButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 4),
        };
        studentsButtons.Controls.AddRange([csvButton, statsButton]);
        studentsGroup.Controls.Add(_studentsGrid);
        studentsGroup.Controls.Add(studentsButtons);
        studentsGroup.Controls.Add(studentsNote);

        ConfigureFilesGrid();
        var filesGroup = new GroupBox
        {
            Text = "Файлы: материалы ученикам и работы учеников",
            Dock = DockStyle.Fill,
            ForeColor = PvzTheme.Ink,
            Padding = new Padding(12, 24, 12, 8),
        };
        var filesNote = new Label
        {
            Text = "«Отправить файл» раздаёт его всем ученикам; работы приходят сюда сами. Обновление каждые 3 с.",
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = PvzTheme.Subtle,
            Font = PvzTheme.Caption,
        };
        var sendButton = PvzTheme.SecondaryButton("Отправить файл…");
        var downloadButton = PvzTheme.SecondaryButton("Скачать выбранный");
        var deleteButton = PvzTheme.GhostButton("Удалить");
        sendButton.Click += async (_, _) => await UploadMaterialAsync();
        downloadButton.Click += async (_, _) => await DownloadSelectedFileAsync();
        deleteButton.Click += async (_, _) => await DeleteSelectedFileAsync();
        var filesButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 0, 0, 4),
        };
        filesButtons.Controls.AddRange([sendButton, downloadButton, deleteButton]);
        _filesGrid.Dock = DockStyle.Fill;
        filesGroup.Controls.Add(_filesGrid);
        filesGroup.Controls.Add(filesButtons);
        filesGroup.Controls.Add(filesNote);

        root.Controls.Add(_status, 0, 0);
        root.Controls.Add(_token, 0, 1);
        root.Controls.Add(tokenRow, 0, 2);
        root.Controls.Add(_tokenHint, 0, 3);
        root.Controls.Add(studentsGroup, 0, 4);
        root.Controls.Add(filesGroup, 0, 5);

        var close = PvzTheme.GhostButton("Закрыть");
        close.DialogResult = DialogResult.OK;
        close.Width = 110;
        var closeRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Margin = new Padding(0, 10, 0, 0),
        };
        closeRow.Controls.Add(close);
        root.Controls.Add(closeRow, 0, 6);

        AcceptButton = close;
        Controls.Add(root);
    }

    private static DataGridView CreateGrid() => new()
    {
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

    private void ConfigureStudentsGrid()
    {
        _studentsGrid.Columns.Add("Student", "Ученик");
        _studentsGrid.Columns.Add("ClassName", "Класс");
        _studentsGrid.Columns.Add("Points", "Баллы");
        _studentsGrid.Columns.Add("Tasks", "Задачи");
        _studentsGrid.Columns.Add("Elapsed", "Время");
        _studentsGrid.Columns.Add("Online", "Статус");
    }

    private void ConfigureFilesGrid()
    {
        _filesGrid.Columns.Add("Name", "Файл");
        _filesGrid.Columns.Add("Kind", "Тип");
        _filesGrid.Columns.Add("Student", "Ученик");
        _filesGrid.Columns.Add("Size", "Размер");
        _filesGrid.Columns.Add("Uploaded", "Дата");
    }

    private async Task InitializeAsync()
    {
        _status.Text = "Подключение к серверу…";
        UseWaitCursor = true;
        Enabled = false;
        try
        {
            _serverUrl = await TaskbookOnlineClient.ResolveServerUrlAsync();
            _accessToken = await TeacherServerAuth.EnsureAccessTokenAsync(this, _serverUrl);
            if (_accessToken is null)
            {
                Close();
                return;
            }

            _status.Text = "Загрузка задачника…";
            var result = await UploadWithFreshAccessTokenIfNeededAsync();
            if (!result.Success)
            {
                _status.Text = result.ErrorMessage ?? "Не удалось загрузить задачник.";
                return;
            }

            _sessionId = result.SessionId;
            _token.Text = result.AccessToken ?? "";
            var expires = result.TokenExpiresAt?.ToLocalTime();
            _tokenHint.Text = expires is null
                ? "Передайте ученикам этот токен."
                : $"Передайте ученикам этот токен. Срок хранения {RetentionPeriods.DisplayName(result.Retention)}, до {expires:dd.MM.yyyy HH:mm}.";
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
            if (!IsDisposed)
            {
                Enabled = true;
                UseWaitCursor = false;
            }
        }
    }

    private async Task<TaskbookPublishResult> UploadWithFreshAccessTokenIfNeededAsync()
    {
        if (_accessToken is null || _serverUrl is null)
            return new TaskbookPublishResult { Success = false, ErrorMessage = "Сервер не настроен." };

        var result = await TaskbookPublisher.UploadOnlineAsync(_document, _serverUrl, _accessToken);
        if (result.Success || !result.Unauthorized)
            return result;

        TeacherServerAuth.Invalidate();
        _accessToken = await TeacherServerAuth.EnsureAccessTokenAsync(this, _serverUrl);
        if (_accessToken is null || IsDisposed)
            return new TaskbookPublishResult { Success = false, ErrorMessage = "Вход отменён." };
        return await TaskbookPublisher.UploadOnlineAsync(_document, _serverUrl, _accessToken);
    }

    private async Task RefreshStudentsAsync(bool silent = false)
    {
        if (_accessToken is null || string.IsNullOrWhiteSpace(_serverUrl) ||
            IsDisposed || Interlocked.Exchange(ref _refreshPending, 1) != 0)
            return;

        try
        {
            await FetchStudentsAsync(silent);
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
                if (silent)
                    Close();
                else
                    _status.Text = "Вход отменён.";
                return;
            }

            _refreshTimer.Start();
            await RefreshStudentsAsync(silent);
        }
    }

    private async Task FetchStudentsAsync(bool silent)
    {
        if (!silent)
        {
            UseWaitCursor = true;
            Enabled = false;
        }

        try
        {
            var sessions = await TaskbookOnlineClient.ListTeacherSessionsAsync(_accessToken!, _serverUrl!);
            var session = sessions.FirstOrDefault(item => item.Id == _sessionId);
            _studentsGrid.Rows.Clear();
            if (session is not null)
            {
                foreach (var student in session.Students)
                {
                    _studentsGrid.Rows.Add(
                        $"{student.FirstName} {student.LastName}",
                        student.ClassName,
                        $"{student.EarnedPoints}/{student.MaximumPoints}",
                        $"{student.CompletedTasks}/{student.TotalTasks}",
                        FormatDuration(student.ElapsedSeconds),
                        student.IsOnline ? "В сети" : "Не в сети");
                }
            }

            if (!string.IsNullOrWhiteSpace(_sessionId))
            {
                var files = await TaskbookOnlineClient.ListSessionFilesAsync(_accessToken!, _sessionId, _serverUrl!);
                _files.Clear();
                _files.AddRange(files);
                _filesGrid.Rows.Clear();
                foreach (var file in files)
                {
                    _filesGrid.Rows.Add(
                        file.Name,
                        ExchangeFileKinds.DisplayName(file.Kind),
                        file.StudentName ?? "-",
                        FormatSize(file.SizeBytes),
                        file.UploadedAt.ToLocalTime().ToString("dd.MM HH:mm"));
                }
            }

            if (!silent)
                _status.Text = $"Учеников: {session?.Students.Count ?? 0}. Обновлено {DateTime.Now:HH:mm:ss}.";
        }
        catch (ServerUnauthorizedException)
        {
            _accessToken = null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            if (!silent)
                _status.Text = ex.Message;
        }
        finally
        {
            if (!silent && !IsDisposed)
            {
                Enabled = true;
                UseWaitCursor = false;
            }
        }
    }

    private ExchangeFileDto? SelectedFile()
    {
        var index = _filesGrid.CurrentRow?.Index ?? -1;
        return index >= 0 && index < _files.Count ? _files[index] : null;
    }

    private async Task UploadMaterialAsync()
    {
        if (_accessToken is null || string.IsNullOrWhiteSpace(_serverUrl) || string.IsNullOrWhiteSpace(_sessionId))
            return;

        using var dialog = new OpenFileDialog
        {
            Title = "Файл для учеников",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        if (new FileInfo(dialog.FileName).Length > TaskbookOnlineClient.MaximumExchangeFileBytes)
        {
            MessageBox.Show(this, "Файл больше 32 МиБ.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        UseWaitCursor = true;
        try
        {
            var uploaded = await TaskbookOnlineClient.UploadSessionFileAsync(
                _accessToken, _sessionId, dialog.FileName, _serverUrl);
            _status.Text = $"Файл «{uploaded.Name}» отправлен всем ученикам.";
            await FetchStudentsAsync(silent: true);
        }
        catch (ServerUnauthorizedException)
        {
            _accessToken = null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
                UseWaitCursor = false;
        }
    }

    private async Task DownloadSelectedFileAsync()
    {
        var file = SelectedFile();
        if (file is null)
        {
            MessageBox.Show(this, "Выберите файл в списке.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_accessToken is null || string.IsNullOrWhiteSpace(_serverUrl) || string.IsNullOrWhiteSpace(_sessionId))
            return;

        using var dialog = new SaveFileDialog
        {
            Title = "Скачать файл",
            FileName = file.Name,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        UseWaitCursor = true;
        try
        {
            var content = await TaskbookOnlineClient.DownloadSessionFileAsync(
                _accessToken, _sessionId, file.Id, _serverUrl);
            await File.WriteAllBytesAsync(dialog.FileName, content);
            _status.Text = $"Файл «{file.Name}» сохранён.";
        }
        catch (ServerUnauthorizedException)
        {
            _accessToken = null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
                UseWaitCursor = false;
        }
    }

    private async Task DeleteSelectedFileAsync()
    {
        var file = SelectedFile();
        if (file is null)
        {
            MessageBox.Show(this, "Выберите файл в списке.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_accessToken is null || string.IsNullOrWhiteSpace(_serverUrl) || string.IsNullOrWhiteSpace(_sessionId))
            return;
        if (MessageBox.Show(
                this,
                $"Удалить файл «{file.Name}»? Он исчезнет у всех учеников.",
                Text,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        UseWaitCursor = true;
        try
        {
            await TaskbookOnlineClient.DeleteSessionFileAsync(_accessToken, _sessionId, file.Id, _serverUrl);
            _status.Text = $"Файл «{file.Name}» удалён.";
            await FetchStudentsAsync(silent: true);
        }
        catch (ServerUnauthorizedException)
        {
            _accessToken = null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
                UseWaitCursor = false;
        }
    }

    private async Task ExportResultsCsvAsync()
    {
        if (_accessToken is null || string.IsNullOrWhiteSpace(_serverUrl) || string.IsNullOrWhiteSpace(_sessionId))
            return;

        using var dialog = new SaveFileDialog
        {
            Title = "Выгрузить результаты учеников",
            FileName = $"pvz-rezultaty-{DateTime.Now:yyyy-MM-dd-HHmm}.csv",
            Filter = "Таблица CSV (*.csv)|*.csv",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        UseWaitCursor = true;
        try
        {
            var content = await TaskbookOnlineClient.DownloadResultsCsvAsync(_accessToken, _sessionId, _serverUrl);
            await File.WriteAllBytesAsync(dialog.FileName, content);
            _status.Text = $"Результаты сохранены в {dialog.FileName}.";
        }
        catch (ServerUnauthorizedException)
        {
            _accessToken = null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException
            or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
                UseWaitCursor = false;
        }
    }

    private async Task ShowSessionStatsAsync()
    {
        if (_accessToken is null || string.IsNullOrWhiteSpace(_serverUrl) || string.IsNullOrWhiteSpace(_sessionId))
            return;

        UseWaitCursor = true;
        try
        {
            var stats = await TaskbookOnlineClient.GetSessionStatsAsync(_accessToken, _sessionId, _serverUrl);
            TeacherSessionStatsForm.ShowForSession(this, stats);
        }
        catch (ServerUnauthorizedException)
        {
            _accessToken = null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
                UseWaitCursor = false;
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0):0.#} МиБ";
        if (bytes >= 1024L)
            return $"{bytes / 1024.0:0.#} КиБ";
        return $"{bytes} Б";
    }

    private static string FormatDuration(long seconds)
    {
        var total = Math.Max(0, (int)seconds);
        return $"{total / 60} мин {total % 60} сек";
    }
}
