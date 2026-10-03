using PVZ.Core;

namespace PVZ.Player;

internal sealed class TaskWorkspaceControl : UserControl
{
    private readonly ThemePalette _palette;
    private Color CanvasColor => _palette.Canvas;
    private Color SurfaceColor => _palette.Surface;
    private Color InkColor => _palette.Ink;
    private Color MutedColor => _palette.Muted;
    private Color BorderColor => _palette.Border;
    private Color AccentColor => _palette.Accent;

    private readonly TaskbookTaskDto _task;
    private readonly int _taskIndex;
    private readonly Func<bool> _canRunTests;
    private readonly Action? _onProgressChanged;
    private readonly IReadOnlyList<LanguageRuntimeInfo> _languageRuntimes;
    private LanguageRuntimeInfo _selectedRuntime = null!;

    private readonly TextBox _codeEditor;
    private readonly TextBox _outputBox;
    private readonly Button _btnTest;
    private readonly ComboBox _languageSelector;

    public TaskProgress Progress { get; } = new();

    public TaskWorkspaceControl(
        TaskbookTaskDto task,
        int taskIndex,
        AppSettings settings,
        IReadOnlyList<LanguageRuntimeInfo> languageRuntimes,
        Func<bool> canRunTests,
        Action? onProgressChanged = null)
    {
        _task = task;
        _taskIndex = taskIndex;
        _palette = ThemePalette.From(settings);
        _languageRuntimes = languageRuntimes;
        _canRunTests = canRunTests;
        _onProgressChanged = onProgressChanged;
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

        var btnShowCondition = CreateButton("Показать условие");
        _btnTest = CreateButton("Запустить проверку", primary: true);
        var languageLabel = new Label
        {
            Text = "Язык:",
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
            ForeColor = MutedColor,
        };
        _languageSelector = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 360,
            Margin = new Padding(0, 2, 8, 0),
        };
        foreach (var runtime in _languageRuntimes)
            _languageSelector.Items.Add(runtime);
        _selectedRuntime = _languageRuntimes.FirstOrDefault(runtime =>
                               runtime.Language == SolutionLanguage.Python && runtime.IsAvailable)
                           ?? _languageRuntimes.FirstOrDefault(runtime => runtime.IsAvailable)
                           ?? _languageRuntimes.First(runtime => runtime.Language == SolutionLanguage.Python);
        _languageSelector.SelectedItem = _selectedRuntime;
        _languageSelector.SelectedIndexChanged += (_, _) =>
        {
            if (_languageSelector.SelectedItem is LanguageRuntimeInfo runtime)
                _selectedRuntime = runtime;
        };
        btnShowCondition.Click += (_, _) => ShowCondition();
        _btnTest.Click += async (_, _) => await RunTestsAsync();

        top.Controls.Add(languageLabel);
        top.Controls.Add(_languageSelector);
        top.Controls.Add(btnShowCondition);
        top.Controls.Add(_btnTest);

        var innerTabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 5) };

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

        innerTabs.TabPages.Add(codeTab);
        innerTabs.TabPages.Add(conditionTab);
        innerTabs.TabPages.Add(outputTab);

        Controls.Add(innerTabs);
        Controls.Add(top);
    }

    private Button CreateButton(string text, bool primary = false) => new()
    {
        Text = text,
        AutoSize = true,
        Height = 36,
        Padding = new Padding(12, 0, 12, 0),
        FlatStyle = FlatStyle.Flat,
        Tag = primary ? "primary" : null,
        BackColor = primary ? AccentColor : SurfaceColor,
        ForeColor = primary ? _palette.AccentText : InkColor,
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
                    Tag = "card",
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
        _outputBox.Clear();
        Progress.Reset();

        try
        {
            if (_task.Tests.Count == 0)
            {
                _outputBox.Text = "У задачи нет тестов, поэтому оценка не выставлена.";
                _onProgressChanged?.Invoke();
                return;
            }

            if (!_selectedRuntime.IsAvailable)
            {
                _outputBox.Text = $"Язык «{_selectedRuntime.DisplayName}» недоступен: {_selectedRuntime.Status}.\n" +
                                  "Установите или настройте runtime самостоятельно, затем перезапустите задачник.";
                _onProgressChanged?.Invoke();
                return;
            }

            var code = _codeEditor.Text;
            if (string.IsNullOrWhiteSpace(code))
            {
                _outputBox.Text = "Введите код на выбранном языке.";
                _onProgressChanged?.Invoke();
                return;
            }

            var batch = await SolutionRunner.RunAllAsync(
                _selectedRuntime,
                code,
                _task.Tests.Select(test => test.Input).ToList());

            if (!batch.RuntimeAvailable)
            {
                _outputBox.Text = batch.ErrorMessage ?? $"Runtime недоступен: {_selectedRuntime.Status}";
                _onProgressChanged?.Invoke();
                return;
            }

            var visibleTests = _task.Tests
                .Select((test, index) => (test, result: batch.Tests[index], index))
                .Where(item => item.test.ShowToStudent)
                .ToList();
            var passedTests = Enumerable.Range(0, batch.Tests.Count).Count(index =>
                batch.Tests[index].Success &&
                PythonRunner.NormalizeOutput(batch.Tests[index].StandardOutput) ==
                PythonRunner.NormalizeOutput(_task.Tests[index].Output));
            var allPassed = _task.Tests.Count > 0 && passedTests == _task.Tests.Count;
            Progress.Completed = allPassed;

            if (_task.ScoringMode == TaskScoringMode.Percentage)
            {
                Progress.EarnedPoints = TaskbookScoring.CalculatePercentageScore(passedTests, _task.Tests.Count);
            }
            else
            {
                for (var i = 0; i < batch.Tests.Count; i++)
                {
                    var result = batch.Tests[i];
                    if (result.Success &&
                        PythonRunner.NormalizeOutput(result.StandardOutput) ==
                        PythonRunner.NormalizeOutput(_task.Tests[i].Output))
                        Progress.EarnedPoints += _task.Tests[i].Points;
                }

                if (Progress.Completed)
                    Progress.EarnedPoints += _task.BonusPointsForAllTests;
            }

            Progress.EarnedPoints = Math.Min(Progress.EarnedPoints, TaskbookScoring.GetTaskMaxScore(_task));

            var lines = new List<string>();
            if (batch.ErrorMessage != null)
                lines.Add(batch.ErrorMessage);

            if (visibleTests.Count == 0)
            {
                lines.Add("Скрытые тесты проверены. Детали не показываются.");
            }
            else if (batch.ErrorMessage == null)
            {
                foreach (var (test, result, index) in visibleTests)
                {
                    lines.Add($"--- Тест {index + 1} (ввод: {test.Input.Replace("\n", "\\n")}) ---");
                    if (!string.IsNullOrEmpty(result.ErrorMessage))
                        lines.Add(result.ErrorMessage);
                    if (!string.IsNullOrEmpty(result.StandardError))
                        lines.Add(result.StandardError);
                    lines.Add($"Вывод:\n{result.StandardOutput}");

                    var passed = result.Success &&
                                 PythonRunner.NormalizeOutput(result.StandardOutput) ==
                                 PythonRunner.NormalizeOutput(test.Output);
                    if (passed)
                        lines.Add(_task.ScoringMode == TaskScoringMode.Percentage
                            ? "Тест пройден."
                            : $"OK (+{test.Points} баллов)");
                    else
                        lines.Add($"Ошибка. Ожидалось:\n{test.Output}");
                    lines.Add("");
                }
            }

            lines.Add(_task.ScoringMode == TaskScoringMode.Percentage
                ? $"Автооценка: {Progress.EarnedPoints}/100 ({passedTests} из {_task.Tests.Count} тестов)."
                : $"Баллы за задачу: {Progress.EarnedPoints}/{TaskbookScoring.GetTaskMaxScore(_task)}.");
            _outputBox.Text = string.Join(Environment.NewLine, lines);
            _onProgressChanged?.Invoke();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            Progress.Reset();
            _outputBox.Text = $"Не удалось выполнить решение.\n{ex.Message}";
            _onProgressChanged?.Invoke();
        }
        finally
        {
            _btnTest.Enabled = true;
        }
    }

    public void SetTeacherCredited(bool credited) => Progress.TeacherCredited = credited;
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
