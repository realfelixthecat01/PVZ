using System.Diagnostics;
using PVZ.Core;

namespace PVZ.Player;

internal sealed class MainForm : Form
{
    private static readonly Color CanvasColor = PvzTheme.Canvas;
    private static readonly Color SurfaceColor = PvzTheme.Surface;
    private static readonly Color RaisedSurfaceColor = PvzTheme.Elevated;
    private static readonly Color InkColor = PvzTheme.Ink;
    private static readonly Color MutedColor = PvzTheme.Muted;
    private static readonly Color BorderColor = PvzTheme.Border;
    private static readonly Color AccentColor = PvzTheme.Accent;

    private readonly TaskbookDocument _document;
    private readonly OnlineStudentSession? _onlineSession;
    private readonly string? _serverUrl;
    private readonly System.Windows.Forms.Timer _onlineHeartbeat = new() { Interval = 5000 };
    private readonly TabControl _tabs;
    private readonly List<TaskWorkspaceControl> _taskViews = new();
    private ListView _resultsList = null!;
    private CheckedListBox _teacherTasks = null!;
    private TextBox _teacherPassword = null!;
    private Button _btnRefreshResults = null!;
    private Panel _teacherLockedPanel = null!;
    private Panel _teacherUnlockedPanel = null!;
    private TabPage _teacherTab = null!;

    private bool _testStarted;
    private bool _teacherUnlocked;
    private TabPage? _startTab;
    private TabPage? _lastSelectedTab;
    private Label _totalScoreLabel = null!;
    private bool _allowClose;
    private bool _explorerStopped;
    private bool _maxScoreCongratsShown;
    private int _reportPending;
    private Label _onlineStatusLabel = null!;
    private readonly List<ExchangeFileDto> _exchangeFiles = new();
    private ListView _exchangeList = null!;
    private Label _exchangeStatus = null!;

    public MainForm(TaskbookDocument document, OnlineStudentSession? onlineSession = null, string? serverUrl = null)
    {
        PvzTheme.EnableAutoScale(this);
        PvzTheme.FitToScreen(this);
        Text = "Пункт Выдачи Задач — тест";
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        ControlBox = false;
        FormClosing += MainForm_FormClosing;

        PvzFormIcon.TryApply(this);

        _document = document;
        _onlineSession = onlineSession;
        _serverUrl = serverUrl;
        _onlineHeartbeat.Tick += async (_, _) => await ReportOnlineProgressAsync();
        FormClosed += (_, _) => _onlineHeartbeat.Dispose();
        MinimumSize = new Size(900, 600);
        ClientSize = new Size(1180, 780);
        BackColor = CanvasColor;
        ForeColor = InkColor;
        Font = new Font("Segoe UI", 10F);
        TopMost = _document.TopMost;
        if (_document.FullScreen)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }

        _tabs = new TabControl { Dock = DockStyle.Fill };
        _tabs.Selecting += Tabs_Selecting;
        _tabs.SelectedIndexChanged += Tabs_SelectedIndexChanged;

        BuildStartTab();
        BuildTaskTabs();
        BuildResultsTab();
        if (_onlineSession is not null && !string.IsNullOrWhiteSpace(_serverUrl))
            BuildFilesTab();
        BuildTeacherTab();

        Controls.Add(_tabs);
        ApplyVisualStyle(this);
        _lastSelectedTab = _tabs.SelectedTab;
        ApplySequentialUnlock();
        if (_onlineSession is not null && !string.IsNullOrWhiteSpace(_serverUrl))
        {
            _onlineStatusLabel.Text = "Онлайн-синхронизация подключается...";
            _onlineHeartbeat.Start();
            Shown += async (_, _) => await ReportOnlineProgressAsync();
            Shown += async (_, _) => await LoadExchangeFilesAsync();
        }
    }

    private static void ApplyVisualStyle(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            switch (control)
            {
                case TabControl:
                    control.BackColor = CanvasColor;
                    break;
                case TabPage:
                    control.BackColor = SurfaceColor;
                    control.ForeColor = InkColor;
                    break;
                case TableLayoutPanel tableLayout when tableLayout.BackColor == SystemColors.Control:
                    tableLayout.BackColor = CanvasColor;
                    break;
                case FlowLayoutPanel flowLayout when flowLayout.BackColor == SystemColors.Control:
                    flowLayout.BackColor = CanvasColor;
                    break;
                case GroupBox groupBox when groupBox.BackColor == SystemColors.Control:
                    groupBox.BackColor = SurfaceColor;
                    groupBox.ForeColor = InkColor;
                    break;
                case Panel panel when panel.BackColor == SystemColors.Control:
                    panel.BackColor = CanvasColor;
                    break;
                case TextBox textBox:
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    if (textBox.Name is "codeEditor" or "outputBox")
                    {
                        textBox.BackColor = PvzTheme.Elevated;
                        textBox.ForeColor = PvzTheme.Ink;
                    }
                    else
                    {
                        textBox.BackColor = SurfaceColor;
                        textBox.ForeColor = InkColor;
                    }
                    break;
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = BorderColor;
                    button.BackColor = RaisedSurfaceColor;
                    button.ForeColor = InkColor;
                    button.Cursor = Cursors.Hand;
                    if (button.Text.Contains("Начать", StringComparison.OrdinalIgnoreCase) ||
                        button.Text.Contains("Засчитать", StringComparison.OrdinalIgnoreCase) ||
                        button.Text.Contains("провер", StringComparison.OrdinalIgnoreCase))
                    {
                        button.BackColor = AccentColor;
                        button.ForeColor = Color.White;
                        button.FlatAppearance.BorderColor = AccentColor;
                        button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    }
                    break;
                case Label label:
                    if (!label.Text.StartsWith("Задач в задачнике:", StringComparison.Ordinal))
                        label.ForeColor = InkColor;
                    break;
                case ListView listView:
                    listView.BackColor = SurfaceColor;
                    listView.ForeColor = InkColor;
                    listView.BorderStyle = BorderStyle.FixedSingle;
                    listView.GridLines = true;
                    break;
                case CheckedListBox checkedListBox:
                    checkedListBox.BackColor = SurfaceColor;
                    checkedListBox.ForeColor = InkColor;
                    checkedListBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ComboBox comboBox:
                    comboBox.BackColor = SurfaceColor;
                    comboBox.ForeColor = InkColor;
                    comboBox.FlatStyle = FlatStyle.Flat;
                    break;
            }

            if (control.HasChildren)
                ApplyVisualStyle(control);
        }
    }

    private static TaskbookCompilerFlags ResolveAllowedCompilers(TaskbookDocument document)
    {
        var allowed = document.AllowedCompilers;
        return allowed == TaskbookCompilerFlags.None ? TaskbookCompilerFlags.All : allowed;
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            return;
        }

        if (_explorerStopped)
            ExplorerHost.Restart();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Alt | Keys.F4))
            return true;

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void RequestCloseFromTeacher()
    {
        if (!_teacherUnlocked)
            return;

        if (MessageBox.Show(
                this,
                "Закрыть программу и вернуть рабочий стол?",
                Text,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _allowClose = true;
        Close();
    }

    private void Tabs_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_lastSelectedTab == _teacherTab)
            LockTeacherSession();

        _lastSelectedTab = _tabs.SelectedTab;
    }

    private void LockTeacherSession()
    {
        if (!_teacherUnlocked)
            return;

        _teacherUnlocked = false;
        _teacherLockedPanel.Visible = true;
        _teacherUnlockedPanel.Visible = false;
        _teacherPassword.Clear();
    }

    private void Tabs_Selecting(object? sender, TabControlCancelEventArgs e)
    {
        if (e.TabPage != _teacherTab || _teacherUnlocked)
            return;

        e.Cancel = true;
        TryUnlockTeacherPanel();
    }

    private void TryUnlockTeacherPanel()
    {
        Activate();
        BringToFront();

        using var dlg = new Form
        {
            Text = "Панель преподавателя",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            TopMost = true,
            ClientSize = new Size(360, 120),
        };
        PvzFormIcon.TryApply(dlg);
        dlg.Owner = this;

        var label = new Label { Text = "Введите пароль:", AutoSize = true, Location = new Point(12, 14) };
        var password = new TextBox
        {
            UseSystemPasswordChar = true,
            Location = new Point(12, 36),
            Width = 330,
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(186, 72), Width = 75 };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Location = new Point(267, 72), Width = 75 };
        dlg.Controls.AddRange([label, password, ok, cancel]);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;

        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        if (!PasswordUtil.Verify(password.Text, _document.TeacherPasswordHash, _document.TeacherPasswordSalt))
        {
            MessageBox.Show(this, "Неверный пароль.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _teacherUnlocked = true;
        _teacherLockedPanel.Visible = false;
        _teacherUnlockedPanel.Visible = true;
        _teacherPassword.Text = password.Text;
        _tabs.SelectedTab = _teacherTab;
    }

    private void BuildStartTab()
    {
        _startTab = new TabPage("Начать тест");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24),
            BackColor = CanvasColor,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        var title = new Label
        {
            Text = "Добро пожаловать в тестирование",
            AutoSize = true,
            Font = new Font("Segoe UI", 23F, FontStyle.Bold),
            Padding = new Padding(4, 12, 4, 12),
        };

        var info = new Label
        {
            Text = $"Задач в задачнике: {_document.Tasks.Count}",
            AutoSize = true,
            Font = new Font("Segoe UI", 11F),
            Padding = new Padding(4, 0, 4, 12),
            ForeColor = MutedColor,
        };

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Padding = new Padding(4, 12, 4, 12),
            WrapContents = false,
            Margin = new Padding(4),
        };

        var btnStart = new Button
        {
            Text = "Начать тест",
            AutoSize = true,
            Height = 42,
        };
        btnStart.Click += (_, _) =>
        {
            _testStarted = true;
            if (_document.KillExplorer)
                _explorerStopped = ExplorerHost.TryStop();

            MessageBox.Show("Тест начат. Перейдите к задачам.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (_tabs.TabPages.Count > 1)
                _tabs.SelectedIndex = 1;
            ApplySequentialUnlock();
            RefreshResults();
        };

        var btnShutdown = new Button
        {
            Text = "Выключить компьютер",
            AutoSize = true,
            Height = 42,
        };
        btnShutdown.Click += (_, _) =>
        {
            if (MessageBox.Show(
                    "Выключить компьютер сейчас?",
                    Text,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            Process.Start(new ProcessStartInfo("shutdown", "/s /t 0")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
        };

        buttons.Controls.Add(btnStart);
        buttons.Controls.Add(btnShutdown);

        layout.Controls.Add(title, 0, 1);
        layout.Controls.Add(info, 0, 2);
        layout.Controls.Add(buttons, 0, 3);

        _startTab.Controls.Add(layout);
        _tabs.TabPages.Add(_startTab);
    }

    private void BuildTaskTabs()
    {
        var allowedCompilers = ResolveAllowedCompilers(_document);
        for (var i = 0; i < _document.Tasks.Count; i++)
        {
            var page = new TabPage($"Задача {i + 1}");
            var view = new TaskWorkspaceControl(
                _document.Tasks[i],
                i,
                () => _testStarted,
                OnProgressChanged,
                allowedCompilers);
            _taskViews.Add(view);
            page.Controls.Add(view);
            _tabs.TabPages.Add(page);
        }
    }

    private void BuildResultsTab()
    {
        var page = new TabPage("Результаты");

        _totalScoreLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 36,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 8, 12, 8),
            Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
            Text = "Итого: 0/0",
        };

        _resultsList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Padding = new Padding(8),
        };
        _resultsList.Columns.Add("Задача", 80);
        _resultsList.Columns.Add("Баллы", 100);
        _resultsList.Columns.Add("Задание выполнено", 140);
        _resultsList.Columns.Add("Засчитано преподавателем", 160);

        _btnRefreshResults = new Button
        {
            Text = "Обновить",
            Dock = DockStyle.Bottom,
            Height = 32,
        };
        _btnRefreshResults.Click += (_, _) => RefreshResults();
        _onlineStatusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            Padding = new Padding(10, 4, 4, 4),
            Text = _onlineSession is null ? "" : "Ожидание онлайн-синхронизации...",
            ForeColor = MutedColor,
            AutoEllipsis = true,
        };

        page.Controls.Add(_resultsList);
        page.Controls.Add(_totalScoreLabel);
        page.Controls.Add(_onlineStatusLabel);
        page.Controls.Add(_btnRefreshResults);
        _tabs.TabPages.Add(page);
    }

    private void BuildFilesTab()
    {
        var page = new TabPage("Файлы");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var hint = new Label
        {
            Text = "Материалы от преподавателя и ваши работы. Файлы хранятся на сервере преподавателя и удаляются вместе с задачником.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = MutedColor,
            AutoEllipsis = true,
            Padding = new Padding(4, 0, 4, 4),
        };

        _exchangeList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
        };
        _exchangeList.Columns.Add("Файл", 320);
        _exchangeList.Columns.Add("Тип", 140);
        _exchangeList.Columns.Add("Размер", 100);
        _exchangeList.Columns.Add("Дата", 150);

        _exchangeStatus = new Label
        {
            Height = 26,
            Dock = DockStyle.Top,
            Padding = new Padding(4, 4, 4, 0),
            Text = "Загрузка списка файлов…",
            ForeColor = MutedColor,
            AutoEllipsis = true,
        };

        var downloadButton = new Button { Text = "Скачать выбранное", AutoSize = true, Height = 34 };
        var uploadButton = new Button { Text = "Отправить файл учителю…", AutoSize = true, Height = 34 };
        var refreshButton = new Button { Text = "Обновить", AutoSize = true, Height = 34 };
        downloadButton.Click += async (_, _) => await DownloadSelectedExchangeFileAsync();
        uploadButton.Click += async (_, _) => await UploadExchangeFilesAsync();
        refreshButton.Click += async (_, _) => await LoadExchangeFilesAsync();
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 4, 0, 0),
        };
        buttons.Controls.AddRange([downloadButton, uploadButton, refreshButton]);

        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottom.Controls.Add(buttons, 0, 0);
        bottom.Controls.Add(_exchangeStatus, 0, 1);

        layout.Controls.Add(hint, 0, 0);
        layout.Controls.Add(_exchangeList, 0, 1);
        layout.Controls.Add(bottom, 0, 2);
        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
    }

    private async Task LoadExchangeFilesAsync()
    {
        if (_onlineSession is null || string.IsNullOrWhiteSpace(_serverUrl) || IsDisposed)
            return;

        try
        {
            var files = await TaskbookOnlineClient.ListStudentFilesAsync(_onlineSession.SessionId, _serverUrl);
            if (IsDisposed)
                return;
            _exchangeFiles.Clear();
            _exchangeFiles.AddRange(files.Materials.Concat(files.Works));
            _exchangeList.Items.Clear();
            foreach (var file in _exchangeFiles)
            {
                var item = new ListViewItem(file.Name);
                item.SubItems.Add(ExchangeFileKinds.DisplayName(file.Kind));
                item.SubItems.Add(FormatExchangeSize(file.SizeBytes));
                item.SubItems.Add(file.UploadedAt.ToLocalTime().ToString("dd.MM HH:mm"));
                _exchangeList.Items.Add(item);
            }

            _exchangeStatus.ForeColor = MutedColor;
            _exchangeStatus.Text =
                $"Обновлено {DateTime.Now:HH:mm:ss}. Материалов: {files.Materials.Count}, ваших работ: {files.Works.Count}.";
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException)
        {
            if (!IsDisposed)
            {
                _exchangeStatus.ForeColor = PvzTheme.Warning;
                _exchangeStatus.Text = $"Не удалось получить файлы: {exception.Message}";
            }
        }
    }

    private async Task DownloadSelectedExchangeFileAsync()
    {
        if (_exchangeList.SelectedIndices.Count == 0)
        {
            MessageBox.Show(this, "Выберите файл в списке.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var index = _exchangeList.SelectedIndices[0];
        if (index < 0 || index >= _exchangeFiles.Count || _onlineSession is null || string.IsNullOrWhiteSpace(_serverUrl))
            return;
        var file = _exchangeFiles[index];

        using var dialog = new SaveFileDialog
        {
            Title = "Скачать файл",
            FileName = file.Name,
            InitialDirectory = TaskbookUserFolders.DownloadsPath,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var content = await TaskbookOnlineClient.DownloadStudentFileAsync(
                _onlineSession.SessionId, file.Id, _serverUrl);
            await File.WriteAllBytesAsync(dialog.FileName, content);
            if (!IsDisposed)
            {
                _exchangeStatus.ForeColor = MutedColor;
                _exchangeStatus.Text = $"Файл «{file.Name}» сохранён.";
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task UploadExchangeFilesAsync()
    {
        if (_onlineSession is null || string.IsNullOrWhiteSpace(_serverUrl))
            return;

        using var dialog = new OpenFileDialog
        {
            Title = "Отправить файл учителю",
            CheckFileExists = true,
            Multiselect = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        UseWaitCursor = true;
        try
        {
            var sent = 0;
            var skipped = 0;
            foreach (var path in dialog.FileNames)
            {
                if (new FileInfo(path).Length is <= 0 or > TaskbookOnlineClient.MaximumExchangeFileBytes)
                {
                    skipped++;
                    continue;
                }

                await TaskbookOnlineClient.UploadStudentWorkAsync(_onlineSession.SessionId, path, _serverUrl);
                sent++;
            }

            if (!IsDisposed)
            {
                _exchangeStatus.ForeColor = skipped == 0 ? PvzTheme.Success : PvzTheme.Warning;
                _exchangeStatus.Text = skipped == 0
                    ? $"Отправлено файлов: {sent}."
                    : $"Отправлено файлов: {sent}, пропущено (пустые или больше 32 МиБ): {skipped}.";
            }
            await LoadExchangeFilesAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException or UnauthorizedAccessException)
        {
            if (!IsDisposed)
            {
                _exchangeStatus.ForeColor = PvzTheme.Warning;
                _exchangeStatus.Text = $"Не отправлено: {exception.Message}";
            }
        }
        finally
        {
            if (!IsDisposed)
                UseWaitCursor = false;
        }
    }

    private static string FormatExchangeSize(long bytes)
    {
        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0):0.#} МиБ";
        if (bytes >= 1024L)
            return $"{bytes / 1024.0:0.#} КиБ";
        return $"{bytes} Б";
    }

    private void BuildTeacherTab()
    {
        _teacherTab = new TabPage("Панель преподавателя");
        var root = new Panel { Dock = DockStyle.Fill };

        _teacherLockedPanel = new Panel { Dock = DockStyle.Fill };
        var lockLabel = new Label
        {
            Text = "Для доступа к панели преподавателя требуется пароль.\nВыберите эту вкладку ещё раз и введите пароль.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        _teacherLockedPanel.Controls.Add(lockLabel);

        _teacherUnlockedPanel = new Panel { Dock = DockStyle.Fill, Visible = false };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8),
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label { Text = "Пароль подтверждён. Отметьте задачи для зачёта:", AutoSize = true }, 0, 0);

        _teacherTasks = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true };
        for (var i = 0; i < _document.Tasks.Count; i++)
            _teacherTasks.Items.Add($"Задача {i + 1}", false);
        layout.Controls.Add(_teacherTasks, 0, 1);

        var btnApply = new Button { Text = "Засчитать выбранные", AutoSize = true };
        btnApply.Click += (_, _) =>
        {
            if (!_teacherUnlocked)
                return;

            for (var i = 0; i < _teacherTasks.Items.Count; i++)
            {
                var credited = _teacherTasks.GetItemChecked(i);
                _taskViews[i].SetTeacherCredited(credited);
                if (credited)
                    _taskViews[i].Progress.Completed = true;
            }

            OnProgressChanged();
            MessageBox.Show(this, "Зачёт обновлён.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var btnCloseApp = new Button { Text = "Закрыть программу", AutoSize = true };
        btnCloseApp.Click += (_, _) => RequestCloseFromTeacher();

        var teacherButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0),
        };
        teacherButtons.Controls.Add(btnApply);
        teacherButtons.Controls.Add(btnCloseApp);

        layout.Controls.Add(teacherButtons, 0, 2);

        _teacherUnlockedPanel.Controls.Add(layout);
        _teacherPassword = new TextBox();

        root.Controls.Add(_teacherUnlockedPanel);
        root.Controls.Add(_teacherLockedPanel);
        _teacherTab.Controls.Add(root);
        _tabs.TabPages.Add(_teacherTab);
    }

    private void ApplySequentialUnlock()
    {
        if (!_document.SequentialUnlock)
            return;

        for (var i = 0; i < _taskViews.Count; i++)
        {
            var page = _taskViews[i].Parent as TabPage;
            if (page == null)
                continue;

            if (!_testStarted)
            {
                page.Enabled = false;
                continue;
            }

            if (i == 0)
            {
                page.Enabled = true;
                continue;
            }

            var prev = _taskViews[i - 1];
            page.Enabled = prev.Progress.Completed || prev.Progress.TeacherCredited;
        }
    }

    private void RefreshResults()
    {
        _resultsList.Items.Clear();

        var maxTotal = TaskbookScoring.GetDocumentMaxScore(_document);
        var earnedTotal = 0;

        for (var i = 0; i < _taskViews.Count; i++)
        {
            var p = _taskViews[i].Progress;
            var taskMax = TaskbookScoring.GetTaskMaxScore(_document.Tasks[i]);
            var earned = p.TeacherCredited ? taskMax : Math.Min(p.EarnedPoints, taskMax);
            earnedTotal += earned;

            var item = new ListViewItem($"№{i + 1}");
            item.SubItems.Add($"{earned}/{taskMax}");
            item.SubItems.Add(p.Completed ? "Да" : "Нет");
            item.SubItems.Add(p.TeacherCredited ? "Да" : "Нет");
            _resultsList.Items.Add(item);
        }

        _totalScoreLabel.Text = $"Итого: {earnedTotal}/{maxTotal}";
        ApplySequentialUnlock();

        if (maxTotal > 0 && earnedTotal >= maxTotal && !_maxScoreCongratsShown)
        {
            _maxScoreCongratsShown = true;
            MessageBox.Show(
                this,
                "Поздравляем! Вы набрали максимальный балл по всему тесту. Отличная работа!",
                "Поздравляем!",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    private void OnProgressChanged()
    {
        RefreshResults();
        if (_onlineSession is not null)
            _ = ReportOnlineProgressAsync();
    }

    private async Task ReportOnlineProgressAsync()
    {
        if (_onlineSession is null || string.IsNullOrWhiteSpace(_serverUrl) ||
            IsDisposed || Interlocked.Exchange(ref _reportPending, 1) != 0)
            return;

        try
        {
            var progress = _taskViews.Select((view, index) =>
            {
                var maximum = TaskbookScoring.GetTaskMaxScore(_document.Tasks[index]);
                var earned = view.Progress.TeacherCredited
                    ? maximum
                    : Math.Clamp(view.Progress.EarnedPoints, 0, maximum);
                return new StudentTaskProgressDto(earned, view.Progress.Completed || view.Progress.TeacherCredited);
            }).ToArray();
            await TaskbookOnlineClient.ReportProgressAsync(
                new OnlineProgressUpdate(_onlineSession.SessionId, progress),
                _serverUrl);
            if (!IsDisposed)
            {
                _onlineStatusLabel.ForeColor = PvzTheme.Success;
                _onlineStatusLabel.Text = $"Прогресс сохранён: {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidOperationException or TaskCanceledException)
        {
            if (!IsDisposed)
            {
                _onlineStatusLabel.ForeColor = PvzTheme.Warning;
                _onlineStatusLabel.Text = $"Не сохранено на сервере: {exception.Message}";
            }
        }
        finally
        {
            Interlocked.Exchange(ref _reportPending, 0);
        }
    }
}
