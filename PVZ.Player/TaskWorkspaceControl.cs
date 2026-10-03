using PVZ.Core;

namespace PVZ.Player;

internal sealed class TaskWorkspaceControl : UserControl
{
    private static readonly Color CanvasColor = Color.FromArgb(244, 246, 249);
    private static readonly Color SurfaceColor = Color.White;
    private static readonly Color InkColor = Color.FromArgb(30, 41, 59);
    private static readonly Color MutedColor = Color.FromArgb(100, 116, 139);
    private static readonly Color BorderColor = Color.FromArgb(218, 226, 235);
    private static readonly Color AccentColor = Color.FromArgb(37, 99, 165);

    private readonly TaskbookTaskDto _task;
    private readonly int _taskIndex;
    private readonly Func<bool> _canRunTests;
    private readonly Action? _onProgressChanged;
    private readonly TaskbookCompilerFlags _allowedCompilers;

    private readonly TextBox _codeEditor;
    private readonly TextBox _outputBox;
    private readonly Button _btnTest;
    private readonly ComboBox _compilerCombo;
    private readonly TabControl _innerTabs;

    public TaskProgress Progress { get; } = new();

    public TaskWorkspaceControl(
        TaskbookTaskDto task,
        int taskIndex,
        Func<bool> canRunTests,
        Action? onProgressChanged,
        TaskbookCompilerFlags allowedCompilers)
    {
        _task = task;
        _taskIndex = taskIndex;
        _canRunTests = canRunTests;
        _onProgressChanged = onProgressChanged;
        _allowedCompilers = allowedCompilers;
        Dock = DockStyle.Fill;
        BackColor = CanvasColor;
        Padding = new Padding(12);

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(0, 4, 0, 8),
            WrapContents = false,
            BackColor = CanvasColor,
        };

        _compilerCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 170,
            Height = 36,
            Margin = new Padding(0, 0, 8, 0),
        };

        var btnShowCondition = CreateButton("Показать условие");
        _btnTest = CreateButton("Запустить проверку", primary: true);
        btnShowCondition.Click += (_, _) => ShowCondition();
        _btnTest.Click += async (_, _) => await RunTestsAsync();

        top.Controls.Add(_compilerCombo);
        top.Controls.Add(btnShowCondition);
        top.Controls.Add(_btnTest);

        _innerTabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 5) };

        var codeTab = new TabPage("Редактор кода");
        _codeEditor = new TextBox
        {
            Name = "codeEditor",
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 11F),
            BackColor = Color.FromArgb(24, 34, 49),
            ForeColor = Color.FromArgb(226, 232, 240),
            BorderStyle = BorderStyle.FixedSingle,
            AcceptsTab = true,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
        };
        codeTab.Controls.Add(_codeEditor);

        var conditionTab = new TabPage("Условие и тесты");
        var conditionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8),
            BackColor = CanvasColor,
        };
        conditionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        conditionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));

        var conditionBox = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Text = _task.Description,
            Font = new Font("Segoe UI", 10.5F),
            BackColor = SurfaceColor,
            BorderStyle = BorderStyle.FixedSingle,
        };

        var visibleTestsPanel = BuildVisibleTestsPanel();
        conditionBox.Margin = new Padding(0, 0, 0, 10);
        visibleTestsPanel.Margin = new Padding(0);
        conditionLayout.Controls.Add(conditionBox, 0, 0);
        conditionLayout.Controls.Add(visibleTestsPanel, 0, 1);
        conditionTab.Controls.Add(conditionLayout);

        var outputTab = new TabPage("Результат проверки");
        _outputBox = new TextBox
        {
            Name = "outputBox",
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10F),
            BackColor = Color.FromArgb(24, 34, 49),
            ForeColor = Color.FromArgb(226, 232, 240),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false,
        };
        outputTab.Controls.Add(_outputBox);

        _innerTabs.TabPages.Add(codeTab);
        _innerTabs.TabPages.Add(conditionTab);
        _innerTabs.TabPages.Add(outputTab);

        Controls.Add(_innerTabs);
        Controls.Add(top);

        ConfigureCompilerCombo();
    }

    private void ShowOutputTab(string message)
    {
        if (_innerTabs.TabPages.Count >= 3)
            _innerTabs.SelectedTab = _innerTabs.TabPages[2];
        _outputBox.Text = message;
    }

    private void ConfigureCompilerCombo()
    {
        _compilerCombo.SelectedIndexChanged -= OnCompilerComboSelectedIndexChanged;

        var selected = GetSelectedCompiler();
        _compilerCombo.Items.Clear();

        if (_allowedCompilers.HasFlag(TaskbookCompilerFlags.Python))
            _compilerCombo.Items.Add(new CompilerItem("Python", TaskbookCompilerFlags.Python));
        if (_allowedCompilers.HasFlag(TaskbookCompilerFlags.PascalAbc))
            _compilerCombo.Items.Add(new CompilerItem("PascalABC.NET", TaskbookCompilerFlags.PascalAbc));

        if (_compilerCombo.Items.Count == 0)
        {
            _compilerCombo.Items.Add(new CompilerItem("Python", TaskbookCompilerFlags.Python));
            _compilerCombo.SelectedIndex = 0;
            _compilerCombo.Enabled = false;
        }
        else
        {
            _compilerCombo.Enabled = _compilerCombo.Items.Count > 1;
            var index = 0;
            for (var i = 0; i < _compilerCombo.Items.Count; i++)
            {
                if (((CompilerItem)_compilerCombo.Items[i]!).Flag == selected)
                {
                    index = i;
                    break;
                }
            }

            _compilerCombo.SelectedIndex = index;
        }

        _compilerCombo.SelectedIndexChanged += OnCompilerComboSelectedIndexChanged;
        UpdateCodeTabTitle();
    }

    private void OnCompilerComboSelectedIndexChanged(object? sender, EventArgs e) => UpdateCodeTabTitle();

    private TaskbookCompilerFlags GetSelectedCompiler()
    {
        if (_compilerCombo.SelectedItem is CompilerItem item)
            return item.Flag;

        return TaskbookCompilerFlags.Python;
    }

    private void UpdateCodeTabTitle()
    {
        if (_innerTabs.TabPages.Count == 0)
            return;

        _innerTabs.TabPages[0].Text = GetSelectedCompiler() switch
        {
            TaskbookCompilerFlags.PascalAbc => "Редактор PascalABC.NET",
            _ => "Редактор Python",
        };
    }

    private static Button CreateButton(string text, bool primary = false) => new()
    {
        Text = text,
        AutoSize = true,
        Height = 36,
        Padding = new Padding(12, 0, 12, 0),
        FlatStyle = FlatStyle.Flat,
        BackColor = primary ? AccentColor : SurfaceColor,
        ForeColor = primary ? Color.White : InkColor,
        Font = new Font("Segoe UI", 9.5F, primary ? FontStyle.Bold : FontStyle.Regular),
        Cursor = Cursors.Hand,
        FlatAppearance = { BorderColor = primary ? AccentColor : BorderColor },
    };

    private Control BuildVisibleTestsPanel()
    {
        var group = new GroupBox
        {
            Text = "Тесты для ученика",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            BackColor = SurfaceColor,
            ForeColor = InkColor,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
        };

        var list = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4),
            BackColor = SurfaceColor,
        };

        var visibleTests = _task.Tests.Where(t => t.ShowToStudent).ToList();
        if (visibleTests.Count == 0)
        {
            list.Controls.Add(new Label
            {
                Text = "Нет тестов, отмеченных «Показать ученику».",
                AutoSize = true,
                ForeColor = MutedColor,
            });
        }
        else
        {
            foreach (var test in visibleTests)
            {
                var card = new Panel
                {
                    Width = 420,
                    Height = 126,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.FromArgb(248, 250, 252),
                    Margin = new Padding(0, 0, 0, 6),
                };

                card.Controls.Add(new Label
                {
                    Text = "Ввод:",
                    AutoSize = true,
                    Location = new Point(6, 6),
                    ForeColor = MutedColor,
                });
                card.Controls.Add(new TextBox
                {
                    ReadOnly = true,
                    Multiline = true,
                    Location = new Point(6, 24),
                    Size = new Size(190, 82),
                    Text = test.Input,
                    ScrollBars = ScrollBars.Vertical,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Consolas", 9F),
                });
                card.Controls.Add(new Label
                {
                    Text = "Вывод:",
                    AutoSize = true,
                    Location = new Point(210, 6),
                    ForeColor = MutedColor,
                });
                card.Controls.Add(new TextBox
                {
                    ReadOnly = true,
                    Multiline = true,
                    Location = new Point(210, 24),
                    Size = new Size(190, 82),
                    Text = test.Output,
                    ScrollBars = ScrollBars.Vertical,
                    BorderStyle = BorderStyle.FixedSingle,
                    Font = new Font("Consolas", 9F),
                });

                list.Controls.Add(card);
            }
        }

        group.Controls.Add(list);
        return group;
    }

    private void ShowCondition()
    {
        var visible = _task.Tests.Where(t => t.ShowToStudent).ToList();
        var testsText = visible.Count == 0
            ? ""
            : "\n\nТесты:\n" + string.Join("\n\n", visible.Select((t, i) =>
                $"Тест {i + 1}\nВвод: {t.Input}\nВывод: {t.Output}"));

        MessageBox.Show(_task.Description + testsText, $"Условие задачи №{_taskIndex + 1}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public async Task RunTestsAsync()
    {
        if (!_canRunTests())
        {
            MessageBox.Show("Сначала нажмите «Начать тест» на первой вкладке.", "PVZ", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _btnTest.Enabled = false;
        try
        {
            ShowOutputTab("Проверка…");
            Progress.Reset();

            var code = _codeEditor.Text;
            if (string.IsNullOrWhiteSpace(code))
            {
                ShowOutputTab(GetSelectedCompiler() == TaskbookCompilerFlags.PascalAbc
                    ? "Введите код PascalABC.NET."
                    : "Введите код Python.");
                return;
            }

            var compiler = GetSelectedCompiler();
            var lines = new List<string>();
            var visibleTests = _task.Tests.Where(t => t.ShowToStudent).ToList();
            var allPassed = true;
            Progress.EarnedPoints = 0;

            foreach (var test in _task.Tests)
            {
                var result = compiler == TaskbookCompilerFlags.PascalAbc
                    ? await PascalAbcRunner.RunAsync(code, test.Input)
                    : await PythonRunner.RunAsync(code, test.Input);
                var actual = PythonRunner.NormalizeOutput(result.StandardOutput);
                var expected = PythonRunner.NormalizeOutput(test.Output);
                var passed = actual == expected && result.Success;

                if (!passed)
                    allPassed = false;
                if (passed)
                    Progress.EarnedPoints += test.Points;

                if (!test.ShowToStudent)
                    continue;

                lines.Add($"--- Тест (ввод: {test.Input.Replace("\n", "\\n")}) ---");
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                    lines.Add(result.ErrorMessage);
                if (!string.IsNullOrEmpty(result.StandardError))
                    lines.Add(result.StandardError);
                lines.Add($"Вывод:\n{result.StandardOutput}");
                lines.Add(passed ? $"OK (+{test.Points} баллов)" : $"Ошибка. Ожидалось:\n{test.Output}");
                lines.Add("");
            }

            if (visibleTests.Count == 0)
                lines.Add("Скрытые тесты проверены. Детали не показываются.");

            Progress.Completed = allPassed;
            if (Progress.Completed)
                Progress.EarnedPoints += _task.BonusPointsForAllTests;

            Progress.EarnedPoints = Math.Min(Progress.EarnedPoints, TaskbookScoring.GetTaskMaxScore(_task));

            ShowOutputTab(string.Join(Environment.NewLine, lines));
            _onProgressChanged?.Invoke();
        }
        catch (Exception ex)
        {
            ShowOutputTab($"Ошибка проверки:\n{ex.Message}");
        }
        finally
        {
            _btnTest.Enabled = true;
        }
    }

    public void SetTeacherCredited(bool credited) => Progress.TeacherCredited = credited;

    private sealed class CompilerItem(string text, TaskbookCompilerFlags flag)
    {
        public TaskbookCompilerFlags Flag { get; } = flag;

        public override string ToString() => text;
    }
}

internal sealed class TaskProgress
{
    public int EarnedPoints { get; set; }
    public bool Completed { get; set; }
    public bool TeacherCredited { get; set; }

    public void Reset()
    {
        EarnedPoints = 0;
        Completed = false;
    }
}
