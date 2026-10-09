using PVZ.Core;

namespace PVZ.Player;

internal sealed class TaskWorkspaceControl : UserControl
{
    private static readonly Color CanvasColor = PvzTheme.Canvas;
    private static readonly Color SurfaceColor = PvzTheme.Surface;
    private static readonly Color RaisedSurfaceColor = PvzTheme.Elevated;
    private static readonly Color InkColor = PvzTheme.Ink;
    private static readonly Color MutedColor = PvzTheme.Muted;
    private static readonly Color BorderColor = PvzTheme.Border;
    private static readonly Color AccentColor = PvzTheme.Accent;
    private static readonly Color CorrectColor = Color.FromArgb(22, 101, 52);
    private static readonly Color WrongColor = Color.FromArgb(127, 29, 29);

    private readonly TaskbookTaskDto _task;
    private readonly int _taskIndex;
    private readonly Func<bool> _canRunTests;
    private readonly Action? _onProgressChanged;
    private readonly TaskbookCompilerFlags _allowedCompilers;
    private readonly bool _isFillTask;

    private readonly TextBox? _codeEditor;
    private readonly TextBox _outputBox;
    private readonly Button _btnTest;
    private readonly ComboBox? _compilerCombo;
    private readonly TabControl _innerTabs;
    private readonly List<TextBox> _blankInputs = new();
    private readonly List<FlowLayoutPanel> _worksheetRows = new();

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
        _isFillTask = task.Kind == TaskbookTaskKind.FillInBlanks;
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

        _innerTabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 5) };

        _outputBox = new TextBox
        {
            Name = "outputBox",
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10F),
            BackColor = PvzTheme.Elevated,
            ForeColor = PvzTheme.Ink,
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false,
        };

        var btnShowCondition = CreateButton("Показать условие");
        btnShowCondition.Click += (_, _) => ShowCondition();

        if (_isFillTask)
        {
            _btnTest = CreateButton("Проверить ответы", primary: true);
            _btnTest.Click += (_, _) => CheckFillBlanks();

            var worksheetTab = new TabPage("Выполнить задание");
            worksheetTab.Controls.Add(BuildWorksheetPanel());

            var conditionTab = new TabPage("Условие задания");
            conditionTab.Controls.Add(BuildFillConditionBox());

            _innerTabs.TabPages.Add(worksheetTab);
            _innerTabs.TabPages.Add(conditionTab);
        }
        else
        {
            _compilerCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 170,
                Height = 36,
                Margin = new Padding(0, 0, 8, 0),
            };

            _btnTest = CreateButton("Запустить проверку", primary: true);
            _btnTest.Click += async (_, _) => await RunTestsAsync();

            var codeTab = new TabPage("Редактор кода");
            _codeEditor = new TextBox
            {
                Name = "codeEditor",
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 11F),
                BackColor = PvzTheme.Elevated,
                ForeColor = PvzTheme.Ink,
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

            _innerTabs.TabPages.Add(codeTab);
            _innerTabs.TabPages.Add(conditionTab);
        }

        var outputTab = new TabPage("Результат проверки");
        outputTab.Controls.Add(_outputBox);
        _innerTabs.TabPages.Add(outputTab);

        if (_compilerCombo is not null)
            top.Controls.Add(_compilerCombo);
        top.Controls.Add(btnShowCondition);
        top.Controls.Add(_btnTest);

        Controls.Add(_innerTabs);
        Controls.Add(top);

        if (!_isFillTask)
            ConfigureCompilerCombo();
    }

    private void ShowOutputTab(string message)
    {
        if (_innerTabs.TabPages.Count >= 3)
            _innerTabs.SelectedTab = _innerTabs.TabPages[^1];
        _outputBox.Text = message;
    }

    private void ConfigureCompilerCombo()
    {
        if (_compilerCombo is null)
            return;

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
        if (_compilerCombo?.SelectedItem is CompilerItem item)
            return item.Flag;

        return TaskbookCompilerFlags.Python;
    }

    private void UpdateCodeTabTitle()
    {
        if (_compilerCombo is null || _innerTabs.TabPages.Count == 0)
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
                    BackColor = RaisedSurfaceColor,
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

    private Control BuildFillConditionBox()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8),
            BackColor = CanvasColor,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 28));

        var descriptionBox = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Text = _task.Description,
            Font = new Font("Segoe UI", 11F),
            BackColor = SurfaceColor,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 0, 10),
        };

        var hint = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Впишите пропущенные слова вместо «_» на вкладке «Выполнить задание» " +
                   "и нажмите «Проверить ответы».",
            Font = new Font("Segoe UI", 10F),
            ForeColor = MutedColor,
        };

        layout.Controls.Add(descriptionBox, 0, 0);
        layout.Controls.Add(hint, 0, 1);
        return layout;
    }

    private Control BuildWorksheetPanel()
    {
        var group = new GroupBox
        {
            Text = "Впишите пропущенные слова",
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            BackColor = SurfaceColor,
            ForeColor = InkColor,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
        };

        var flow = new FlowLayoutPanel
        {
            Name = "worksheetFlow",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(4),
            BackColor = SurfaceColor,
        };

        BuildWorksheetRows(flow);
        flow.SizeChanged += (_, _) => UpdateWorksheetRowWidths(flow);
        group.Controls.Add(flow);
        return group;
    }

    private void BuildWorksheetRows(FlowLayoutPanel flow)
    {
        var text = (_task.Description ?? "").Replace("\r\n", "\n").Replace('\r', '\n');
        var paragraphs = text.Split('\n');
        var blankNumber = 0;
        var rowFont = new Font("Segoe UI", 11.5F);

        foreach (var paragraph in paragraphs)
        {
            var row = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 2),
                BackColor = SurfaceColor,
                MaximumSize = new Size(600, 0),
            };

            if (paragraph.Length == 0)
            {
                row.Controls.Add(new Label { AutoSize = false, Width = 1, Height = 16, Margin = new Padding(0) });
                _worksheetRows.Add(row);
                flow.Controls.Add(row);
                continue;
            }

            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var segments = FillBlankRules.BlankPattern.Split(word);
                for (var index = 0; index < segments.Length; index++)
                {
                    if (segments[index].Length > 0)
                    {
                        row.Controls.Add(new Label
                        {
                            Text = segments[index],
                            AutoSize = true,
                            Font = rowFont,
                            ForeColor = InkColor,
                            Margin = new Padding(0, 5, 7, 5),
                        });
                    }

                    if (index < segments.Length - 1)
                    {
                        blankNumber++;
                        var box = CreateBlankInput(blankNumber);
                        _blankInputs.Add(box);
                        row.Controls.Add(box);
                    }
                }
            }

            _worksheetRows.Add(row);
            flow.Controls.Add(row);
        }

        if (blankNumber == 0)
        {
            flow.Controls.Add(new Label
            {
                Text = "В этом тексте нет пропусков «_».",
                AutoSize = true,
                ForeColor = MutedColor,
            });
        }
    }

    private static TextBox CreateBlankInput(int number) => new()
    {
        Width = 130,
        Font = new Font("Segoe UI", 11F, FontStyle.Bold),
        BackColor = RaisedSurfaceColor,
        ForeColor = InkColor,
        BorderStyle = BorderStyle.FixedSingle,
        PlaceholderText = number.ToString(),
        TextAlign = HorizontalAlignment.Center,
        Margin = new Padding(0, 1, 7, 1),
    };

    private void UpdateWorksheetRowWidths(FlowLayoutPanel flow)
    {
        var available = flow.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 16;
        if (available < 200)
            available = 200;

        foreach (var row in _worksheetRows)
            row.MaximumSize = new Size(available, 0);
    }

    private void ShowCondition()
    {
        if (_isFillTask)
        {
            MessageBox.Show(
                _task.Description + $"\n\nВпишите пропущенные слова вместо «_». Пропусков: {_task.Blanks.Count}.",
                $"Условие задания №{_taskIndex + 1}",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var visible = _task.Tests.Where(t => t.ShowToStudent).ToList();
        var testsText = visible.Count == 0
            ? ""
            : "\n\nТесты:\n" + string.Join("\n\n", visible.Select((t, i) =>
                $"Тест {i + 1}\nВвод: {t.Input}\nВывод: {t.Output}"));

        MessageBox.Show(_task.Description + testsText, $"Условие задачи №{_taskIndex + 1}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void CheckFillBlanks()
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

            var blanks = _task.Blanks;
            if (blanks.Count == 0 || _blankInputs.Count == 0)
            {
                ShowOutputTab("В задании нет пропусков для проверки.");
                return;
            }

            var emptyCount = _blankInputs.Count(box => string.IsNullOrWhiteSpace(box.Text));
            if (emptyCount > 0)
            {
                ShowOutputTab($"Заполните все пропуски: пустых полей — {emptyCount}.");
                MessageBox.Show(
                    $"Заполните все пропуски: осталось пустых полей — {emptyCount}.",
                    "PVZ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var lines = new List<string>();
            var total = Math.Min(blanks.Count, _blankInputs.Count);
            var correct = 0;
            Progress.EarnedPoints = 0;

            for (var index = 0; index < total; index++)
            {
                var blank = blanks[index];
                var box = _blankInputs[index];
                var passed = FillBlankRules.Matches(blank, box.Text);
                box.BackColor = passed ? CorrectColor : WrongColor;

                var number = index + 1;
                if (passed)
                {
                    correct++;
                    Progress.EarnedPoints += blank.Points;
                    lines.Add($"Пропуск {number}: верно (+{blank.Points} баллов)");
                }
                else if (blank.ShowToStudent && !string.IsNullOrWhiteSpace(blank.Answer))
                {
                    lines.Add($"Пропуск {number}: неверно. Ваш ответ: «{box.Text.Trim()}». Правильный ответ: «{blank.Answer}»");
                }
                else
                {
                    lines.Add($"Пропуск {number}: неверно. Правильный ответ скрыт.");
                }
            }

            if (blanks.Count != _blankInputs.Count)
                lines.Add($"Внимание: в тексте {_blankInputs.Count} пропусков, а ответов сохранено {blanks.Count}.");

            Progress.Completed = correct == total;
            if (Progress.Completed)
            {
                lines.Add("");
                lines.Add($"Все пропуски заполнены верно! Бонус: +{_task.BonusPointsForAllTests} баллов");
                Progress.EarnedPoints += _task.BonusPointsForAllTests;
            }

            Progress.EarnedPoints = Math.Min(Progress.EarnedPoints, TaskbookScoring.GetTaskMaxScore(_task));

            lines.Add("");
            lines.Add($"Итог: {correct} из {total} пропусков, {Progress.EarnedPoints} баллов.");
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

            var code = _codeEditor?.Text ?? "";
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
