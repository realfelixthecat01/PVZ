using System.Diagnostics;
using PVZ.Core;

namespace PVZ.Player;

internal sealed class MainForm : Form
{
    private static readonly Color CanvasColor = Color.FromArgb(244, 246, 249);
    private static readonly Color SurfaceColor = Color.White;
    private static readonly Color InkColor = Color.FromArgb(30, 41, 59);
    private static readonly Color MutedColor = Color.FromArgb(100, 116, 139);
    private static readonly Color BorderColor = Color.FromArgb(218, 226, 235);
    private static readonly Color AccentColor = Color.FromArgb(37, 99, 165);

    private readonly TaskbookDocument _document;
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

    public MainForm()
    {
        Text = "Пункт Выдачи Задач — тест";
        Width = 1000;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        ControlBox = false;
        FormClosing += MainForm_FormClosing;

        try
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            // optional window icon
        }

        _document = EmbeddedTaskbook.LoadForRunningApp();
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
        BuildTeacherTab();

        Controls.Add(_tabs);
        ApplyVisualStyle(this);
        _lastSelectedTab = _tabs.SelectedTab;
        ApplySequentialUnlock();
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
                        textBox.BackColor = Color.FromArgb(24, 34, 49);
                        textBox.ForeColor = Color.FromArgb(226, 232, 240);
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
                    button.BackColor = Color.FromArgb(237, 242, 248);
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
                RefreshResults,
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
        _resultsList.Columns.Add("Все тесты выполнены", 140);
        _resultsList.Columns.Add("Засчитано преподавателем", 160);

        _btnRefreshResults = new Button
        {
            Text = "Обновить",
            Dock = DockStyle.Bottom,
            Height = 32,
        };
        _btnRefreshResults.Click += (_, _) => RefreshResults();

        page.Controls.Add(_resultsList);
        page.Controls.Add(_totalScoreLabel);
        page.Controls.Add(_btnRefreshResults);
        _tabs.TabPages.Add(page);
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

            RefreshResults();
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
}
