// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using PVZ.Core;
using PVZ.Player;

namespace PVZ
{
    public partial class Form1 : Form
    {
        private readonly List<TaskbookTaskDto> _tasks = new();
        private readonly List<Button> _taskTabButtons = new();
        private int _currentTaskIndex;
        private bool _suppressUiSync;

        private FlowLayoutPanel _testsFlow = null!;
        private Panel _taskTabsScroll = null!;
        private Panel _headerPanel = null!;
        private Panel _headerAccent = null!;
        private Label _headerEyebrow = null!;
        private Label _headerTitle = null!;
        private Label _headerSubtitle = null!;
        private FlowLayoutPanel _taskTabsFlow = null!;
        private bool _layingOutTests;
        private TaskbookCompilerFlags _allowedCompilers = TaskbookCompilerFlags.All;
        private BuilderThemeMode _themeMode = BuilderThemeMode.Dark;
        private Button _btnAllowedCompilers = null!;
        private Button _btnColorScheme = null!;
        private Color _canvasColor = Color.FromArgb(13, 17, 27);
        private Color _surfaceColor = Color.FromArgb(21, 27, 42);
        private Color _raisedSurfaceColor = Color.FromArgb(30, 38, 56);
        private Color _inkColor = Color.FromArgb(226, 232, 240);
        private Color _mutedColor = Color.FromArgb(148, 163, 184);
        private Color _borderColor = Color.FromArgb(55, 65, 95);
        private Color _accentColor = Color.FromArgb(99, 102, 241);
        private const int TaskTabRowHeight = 30;
        private const string TestRemoveButtonTag = "test-remove";
        private const int TestCloseButtonReserve = 26;
        private const string BlankHeaderName = "blankHeader";
        private const string BlankContextName = "blankContext";
        private const string BlankAnswerName = "blankAnswer";
        private const string BlankPointsName = "blankPoints";
        private const string BlankShowName = "blankShow";
        private const string OptionHeaderName = "optionHeader";
        private const string OptionTextName = "optionText";
        private const string OptionCorrectName = "optionCorrect";
        private const string OptionPointsLabelName = "optionPointsLabel";
        private const string OptionPointsName = "optionPoints";
        private const string PairHeaderName = "pairHeader";
        private const string PairLeftName = "pairLeft";
        private const string PairRightName = "pairRight";
        private const string PairPointsLabelName = "pairPointsLabel";
        private const string PairPointsName = "pairPoints";

        private static readonly TaskbookTaskKind[] ComboKinds =
        [
            TaskbookTaskKind.CodeTests,
            TaskbookTaskKind.FillInBlanks,
            TaskbookTaskKind.Choice,
            TaskbookTaskKind.Matching,
            TaskbookTaskKind.ShortAnswer,
        ];

        private Button _btnLoadTaskbook = null!;
        private ComboBox _kindCombo = null!;

        public Form1()
        {
            InitializeComponent();
            PvzFormIcon.TryApply(this);
            var settings = BuilderThemePreferences.Load(out var settingsWarning);
            _themeMode = settings.Mode;
            _accentColor = Color.FromArgb(settings.AccentArgb);
            SetThemeColors();
            if (settingsWarning != null)
                MessageBox.Show(settingsWarning, "Настройки темы", MessageBoxButtons.OK, MessageBoxIcon.Warning);

            MinimumSize = new Size(1120, 700);
            ClientSize = new Size(1120, 720);
            AutoScroll = true;
            BackColor = _canvasColor;
            ForeColor = _inkColor;
            Font = new Font("Segoe UI", 9.5F);
            SetupUi();
            Resize += (_, _) => LayoutEditor();
            PvzTheme.FitToScreen(this);
            WireEvents();

            var firstTask = new TaskbookTaskDto
            {
                Description = textBox1.Text,
                BonusPointsForAllTests = ParseIntOrDefault(textBox5.Text, 10),
            };
            firstTask.Tests.Add(new TestCaseDto
            {
                Input = textBox2.Text,
                Output = textBox3.Text,
                ShowToStudent = checkBox4.Checked,
                Points = ParseIntOrDefault(textBox6.Text, 25),
            });
            _tasks.Add(firstTask);

            panel4.Parent?.Controls.Remove(panel4);
            panel4.Dispose();

            RebuildTaskTabs();
            LoadTaskToUi(0);
        }

        private void SetupUi()
        {
            textBox4.PasswordChar = '*';
            textBox1.PlaceholderText = "Введите условие задачи...";
            label1.Text = "УСЛОВИЕ ЗАДАЧИ";
            label2.Text = "ТЕСТОВЫЕ ПРИМЕРЫ";
            button2.Text = "+  Добавить тест";

            button3.Visible = false;
            panel1.BorderStyle = BorderStyle.None;
            panel1.BackColor = _raisedSurfaceColor;
            panel2.BorderStyle = BorderStyle.None;
            panel2.BackColor = _surfaceColor;
            panel3.BorderStyle = BorderStyle.None;
            panel3.BackColor = _raisedSurfaceColor;

            _headerPanel = new Panel { BackColor = _canvasColor };
            _headerAccent = new Panel { BackColor = _accentColor };
            _headerEyebrow = new Label
            {
                Name = "headerEyebrow",
                Text = "КОНСТРУКТОР ТЕСТИРОВАНИЯ",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = _accentColor,
            };
            _headerTitle = new Label
            {
                Name = "headerTitle",
                Text = "Пункт выдачи задач",
                AutoSize = true,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = _inkColor,
            };
            _headerSubtitle = new Label
            {
                Name = "headerSubtitle",
                Text = "Подготовьте условие, тесты и параметры нового задачника",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = _mutedColor,
            };
            _headerPanel.Controls.AddRange([_headerAccent, _headerEyebrow, _headerTitle, _headerSubtitle]);
            Controls.Add(_headerPanel);

            _taskTabsScroll = new Panel
            {
                Location = new Point(8, 4),
                Size = new Size(panel1.Width - 50, 40),
                AutoScroll = true,
                BackColor = _raisedSurfaceColor,
            };
            _taskTabsFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = _raisedSurfaceColor,
            };
            _taskTabsScroll.Controls.Add(_taskTabsFlow);
            panel1.Controls.Add(_taskTabsScroll);

            _testsFlow = new FlowLayoutPanel
            {
                Location = new Point(3, 3),
                Size = new Size(231, panel3.Height - button2.Height - 8),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = _raisedSurfaceColor,
            };
            _testsFlow.HorizontalScroll.Enabled = false;
            _testsFlow.HorizontalScroll.Visible = false;
            _testsFlow.Resize += (_, _) => LayoutTestPanels();
            _testsFlow.ControlAdded += (_, _) => ScheduleLayoutTestPanels();
            _testsFlow.ControlRemoved += (_, _) => ScheduleLayoutTestPanels();
            panel3.Controls.Add(_testsFlow);

            _btnAllowedCompilers = new Button
            {
                Text = "Разрешённые компиляторы",
                AutoSize = true,
                Height = 32,
            };
            _btnAllowedCompilers.Click += (_, _) =>
            {
                var allowed = _allowedCompilers;
                if (!BuilderSettingsDialogs.TryPickAllowedCompilers(this, ref allowed))
                    return;

                _allowedCompilers = allowed;
            };

            _btnColorScheme = new Button
            {
                Text = "Цветовая схема",
                AutoSize = true,
                Height = 32,
            };
            _btnColorScheme.Click += (_, _) =>
            {
                if (!BuilderSettingsDialogs.TryPickColorScheme(
                        this,
                        _themeMode,
                        _accentColor,
                        out var mode,
                        out var accent))
                    return;

                ApplyBuilderTheme(mode, accent);
            };

            _btnLoadTaskbook = new Button
            {
                Text = "Открыть .pvz",
                AutoSize = true,
                Height = 32,
            };
            _btnLoadTaskbook.Click += (_, _) => OnLoadTaskbook();

            _kindCombo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Height = 26,
                Width = 230,
            };
            _kindCombo.Items.Add("Задача с кодом (тесты)");
            _kindCombo.Items.Add("Задание с пропусками «_»");
            _kindCombo.Items.Add("Выбор ответа (варианты)");
            _kindCombo.Items.Add("Соответствие (пары)");
            _kindCombo.Items.Add("Короткий ответ (ввод)");
            _kindCombo.SelectedIndexChanged += (_, _) => OnKindComboChanged();

            Controls.Add(_btnAllowedCompilers);
            Controls.Add(_btnColorScheme);
            Controls.Add(_btnLoadTaskbook);
            Controls.Add(_kindCombo);
            StyleEditorControls(this);
            LayoutEditor();
            _headerPanel.BringToFront();
        }

        private void LayoutEditor()
        {
            if (_taskTabsScroll == null || _testsFlow == null)
                return;

            const int margin = 16;
            const int gap = 12;
            const int footerHeight = 108;
            const int testsWidth = 264;
            var contentWidth = ClientSize.Width - margin * 2;
            const int headerHeight = 66;
            var tabsHeight = 42;
            var headerTop = margin;
            var tabsTop = headerTop + headerHeight + gap;
            var contentTop = tabsTop + tabsHeight + gap;
            var contentHeight = Math.Max(280, ClientSize.Height - contentTop - footerHeight - margin);

            _headerPanel.SetBounds(margin, headerTop, contentWidth, headerHeight);
            _headerAccent.SetBounds(0, 8, 3, 50);
            _headerEyebrow.Location = new Point(14, 3);
            _headerTitle.Location = new Point(12, 14);
            _headerSubtitle.Location = new Point(14, 45);

            panel1.SetBounds(margin, tabsTop, contentWidth, tabsHeight);
            panel2.SetBounds(margin, contentTop, contentWidth, contentHeight);

            button4.SetBounds(panel1.ClientSize.Width - 42, 7, 34, 28);
            _taskTabsScroll.SetBounds(8, 5, Math.Max(100, panel1.ClientSize.Width - 58), 32);

            const int contentPadding = 16;
            var testsLeft = panel2.ClientSize.Width - testsWidth - contentPadding;
            panel3.SetBounds(testsLeft, 42, testsWidth, panel2.ClientSize.Height - 58);
            _testsFlow.SetBounds(10, 10, panel3.ClientSize.Width - 20, panel3.ClientSize.Height - 60);
            button2.SetBounds(10, panel3.ClientSize.Height - 42, panel3.ClientSize.Width - 20, 34);

            label1.SetBounds(contentPadding, 12, Math.Max(80, testsLeft - contentPadding - 260), 22);
            _kindCombo.SetBounds(testsLeft - contentPadding - 230, 9, 230, 26);
            textBox1.SetBounds(contentPadding, 40, Math.Max(180, testsLeft - contentPadding - 16), panel2.ClientSize.Height - 56);
            label2.SetBounds(testsLeft, 12, testsWidth, 22);
            label4.SetBounds(testsLeft, panel2.ClientSize.Height - 39, testsWidth - 32, 24);
            textBox5.SetBounds(panel2.ClientSize.Width - contentPadding - 48, panel2.ClientSize.Height - 40, 48, 26);

            var footerTop = panel2.Bottom + 10;
            label3.SetBounds(margin, footerTop + 2, 156, 24);
            textBox4.SetBounds(margin + 160, footerTop, 120, 28);
            checkBox1.SetBounds(margin, footerTop + 34, 200, 24);
            checkBox2.SetBounds(margin + 210, footerTop + 34, 150, 24);
            checkBox3.SetBounds(margin + 380, footerTop + 2, 310, 24);
            checkBox5.SetBounds(margin + 380, footerTop + 34, 260, 24);
            _btnAllowedCompilers.SetBounds(margin + 660, footerTop + 2, 190, 32);
            _btnColorScheme.SetBounds(margin + 660, footerTop + 38, 190, 32);
            _btnLoadTaskbook.SetBounds(margin + 660, footerTop + 74, 190, 32);
            button1.SetBounds(ClientSize.Width - margin - 210, footerTop + 8, 210, 42);

            LayoutTaskTabsScroll();
            LayoutTestPanels();
        }

        private void StyleEditorControls(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Name is not ("headerEyebrow" or "headerTitle" or "headerSubtitle"))
                    control.Font = new Font("Segoe UI", 9.5F);
                control.ForeColor = _inkColor;

                switch (control)
                {
                    case TextBox textBox:
                        textBox.BorderStyle = BorderStyle.FixedSingle;
                        textBox.BackColor = _surfaceColor;
                        break;
                    case ComboBox combo:
                        combo.FlatStyle = FlatStyle.Flat;
                        combo.BackColor = _themeMode == BuilderThemeMode.Light ? Color.White : _surfaceColor;
                        combo.ForeColor = _inkColor;
                        break;
                    case Button button:
                        button.FlatStyle = FlatStyle.Flat;
                        button.FlatAppearance.BorderColor = _borderColor;
                        button.BackColor = _themeMode == BuilderThemeMode.Light
                            ? Color.FromArgb(237, 242, 248)
                            : _raisedSurfaceColor;
                        button.Cursor = Cursors.Hand;
                        button.Height = Math.Max(button.Height, 30);
                        if (button.Name == "button1")
                        {
                            button.BackColor = _accentColor;
                            button.ForeColor = GetContrastingTextColor(_accentColor);
                            button.FlatAppearance.BorderColor = _accentColor;
                            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                        }
                        break;
                    case Label label:
                        label.ForeColor = _mutedColor;
                        if (label.Name == "headerTitle")
                        {
                            label.ForeColor = _inkColor;
                        }
                        else if (label.Name == "headerEyebrow")
                        {
                            label.ForeColor = HasAccentContrast()
                                ? _accentColor
                                : _mutedColor;
                        }
                        else if (label.Name == "label1" || label.Name == "label2")
                        {
                            label.ForeColor = _inkColor;
                            label.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                        }
                        break;
                    case CheckBox:
                        control.ForeColor = _mutedColor;
                        break;
                }

                if (control.HasChildren)
                    StyleEditorControls(control);
            }
        }

        private void ApplyBuilderTheme(BuilderThemeMode mode, Color accentColor)
        {
            _themeMode = mode;
            _accentColor = accentColor;
            SetThemeColors();

            BackColor = _canvasColor;
            ForeColor = _inkColor;
            _headerPanel.BackColor = _canvasColor;
            _headerAccent.BackColor = _accentColor;
            _headerEyebrow.ForeColor = HasAccentContrast()
                ? _accentColor
                : _mutedColor;
            _headerTitle.ForeColor = _inkColor;
            _headerSubtitle.ForeColor = _mutedColor;
            panel1.BackColor = _raisedSurfaceColor;
            panel2.BackColor = _surfaceColor;
            panel3.BackColor = _raisedSurfaceColor;
            _taskTabsScroll.BackColor = _raisedSurfaceColor;
            _taskTabsFlow.BackColor = _raisedSurfaceColor;
            _testsFlow.BackColor = _raisedSurfaceColor;

            StyleEditorControls(this);
            UpdateTaskTabSelection();

            foreach (Control control in _testsFlow.Controls)
            {
                if (control is not Panel panel)
                    continue;

                panel.BackColor = _surfaceColor;
                foreach (Control child in panel.Controls)
                {
                    switch (child)
                    {
                        case Button button when (string?)button.Tag == TestRemoveButtonTag:
                            button.BackColor = _raisedSurfaceColor;
                            button.ForeColor = _mutedColor;
                            break;
                        case TextBox textBox:
                            textBox.BackColor = _surfaceColor;
                            textBox.ForeColor = _inkColor;
                            break;
                        case CheckBox checkBox:
                            checkBox.ForeColor = _mutedColor;
                            break;
                        case Label label:
                            label.ForeColor = label.Name is BlankHeaderName or OptionHeaderName or PairHeaderName
                                ? _inkColor
                                : _mutedColor;
                            break;
                    }
                }
            }

            if (!BuilderThemePreferences.TrySave(
                    new BuilderThemeSettings(_themeMode, _accentColor.ToArgb()),
                    out var error))
                MessageBox.Show(this, $"Не удалось сохранить настройки темы: {error}", "Настройки темы", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void SetThemeColors()
        {
            if (_themeMode == BuilderThemeMode.Light)
            {
                _canvasColor = Color.FromArgb(244, 246, 249);
                _surfaceColor = Color.White;
                _raisedSurfaceColor = Color.FromArgb(247, 249, 252);
                _inkColor = Color.FromArgb(30, 41, 59);
                _mutedColor = Color.FromArgb(100, 116, 139);
                _borderColor = Color.FromArgb(218, 226, 235);
            }
            else
            {
                _canvasColor = Color.FromArgb(13, 17, 27);
                _surfaceColor = Color.FromArgb(21, 27, 42);
                _raisedSurfaceColor = Color.FromArgb(30, 38, 56);
                _inkColor = Color.FromArgb(226, 232, 240);
                _mutedColor = Color.FromArgb(148, 163, 184);
                _borderColor = Color.FromArgb(55, 65, 95);
            }
        }

        private bool HasAccentContrast()
        {
            static double Luminance(Color color)
            {
                static double Linearize(byte channel)
                {
                    var value = channel / 255d;
                    return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
                }

                return 0.2126 * Linearize(color.R)
                    + 0.7152 * Linearize(color.G)
                    + 0.0722 * Linearize(color.B);
            }

            var accentLuminance = Luminance(_accentColor);
            var canvasLuminance = Luminance(_canvasColor);
            var contrast = (Math.Max(accentLuminance, canvasLuminance) + 0.05)
                / (Math.Min(accentLuminance, canvasLuminance) + 0.05);
            return contrast >= 3;
        }

        private static Color GetContrastingTextColor(Color background) =>
            background.R * 299 + background.G * 587 + background.B * 114 >= 150_000
                ? Color.FromArgb(13, 17, 27)
                : Color.White;

        private void WireEvents()
        {
            button2.Click += (_, _) => AddTestToCurrentTask();
            button4.Click += (_, _) => AddTask();
            button1.Click += (_, _) => OnCreateTaskbook();

            textBox1.TextChanged += (_, _) => SyncDescriptionFromUi();
            textBox5.TextChanged += (_, _) => SyncBonusPointsFromUi();

        }

        private void AddTask()
        {
            SaveCurrentTaskFromUi();
            _tasks.Add(new TaskbookTaskDto());
            _tasks[^1].Tests.Add(new TestCaseDto());
            _currentTaskIndex = _tasks.Count - 1;
            RebuildTaskTabs();
            LoadTaskToUi(_currentTaskIndex);
        }

        private void SelectTask(int index)
        {
            if (index < 0 || index >= _tasks.Count || index == _currentTaskIndex)
                return;

            SaveCurrentTaskFromUi();
            _currentTaskIndex = index;
            LoadTaskToUi(_currentTaskIndex);
            UpdateTaskTabSelection();
        }

        private void RebuildTaskTabs()
        {
            for (var i = _taskTabsFlow.Controls.Count - 1; i >= 0; i--)
                _taskTabsFlow.Controls[i].Dispose();
            _taskTabButtons.Clear();

            for (var i = 0; i < _tasks.Count; i++)
            {
                var index = i;
                var tabRow = new FlowLayoutPanel
                {
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    Margin = new Padding(0, 0, 6, 0),
                };

                var marker = _tasks[i].Kind switch
                {
                    TaskbookTaskKind.FillInBlanks => "_",
                    TaskbookTaskKind.Choice => "?",
                    TaskbookTaskKind.Matching => "=",
                    TaskbookTaskKind.ShortAnswer => "A",
                    _ => "",
                };
                var hasMarker = marker.Length > 0;
                var tab = new Button
                {
                    Text = hasMarker ? $"ЗАДАЧА  {i + 1:00}  {marker}" : $"ЗАДАЧА  {i + 1:00}",
                    Size = new Size(hasMarker ? 110 : 94, TaskTabRowHeight),
                    Margin = new Padding(0),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = _raisedSurfaceColor,
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                };
                tab.FlatAppearance.BorderSize = 0;
                tab.Click += (_, _) => SelectTask(index);

                var removeTask = new Button
                {
                    Text = "×",
                    Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                    Size = new Size(27, TaskTabRowHeight),
                    Margin = new Padding(0),
                    FlatStyle = FlatStyle.Flat,
                    FlatAppearance = { BorderSize = 0 },
                    BackColor = _raisedSurfaceColor,
                    ForeColor = _mutedColor,
                    Cursor = Cursors.Hand,
                };
                removeTask.Click += (_, _) => DeleteTask(index);

                tabRow.Controls.Add(tab);
                tabRow.Controls.Add(removeTask);
                _taskTabButtons.Add(tab);
                _taskTabsFlow.Controls.Add(tabRow);
            }

            UpdateTaskTabSelection();
            LayoutTaskTabsScroll();
        }

        private void DeleteTask(int index)
        {
            if (_tasks.Count <= 1)
            {
                MessageBox.Show("Нельзя удалить последнюю задачу", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveCurrentTaskFromUi();
            _tasks.RemoveAt(index);

            if (_currentTaskIndex > index)
                _currentTaskIndex--;
            else if (_currentTaskIndex == index)
                _currentTaskIndex = Math.Min(index, _tasks.Count - 1);

            RebuildTaskTabs();
            LoadTaskToUi(_currentTaskIndex);
        }

        private void LayoutTaskTabsScroll()
        {
            _taskTabsFlow.PerformLayout();
        }

        private void UpdateTaskTabSelection()
        {
            for (var i = 0; i < _taskTabButtons.Count; i++)
            {
                var selected = i == _currentTaskIndex;
                _taskTabButtons[i].Font = new Font(_taskTabButtons[i].Font, selected ? FontStyle.Bold : FontStyle.Regular);
                _taskTabButtons[i].FlatStyle = FlatStyle.Flat;
                _taskTabButtons[i].FlatAppearance.BorderSize = 0;
                _taskTabButtons[i].BackColor = selected
                    ? _accentColor
                    : _themeMode == BuilderThemeMode.Light
                        ? _inkColor
                        : _raisedSurfaceColor;
                _taskTabButtons[i].ForeColor = selected
                    ? GetContrastingTextColor(_accentColor)
                    : Color.White;
            }

            if (_currentTaskIndex >= 0 && _currentTaskIndex < _taskTabButtons.Count)
                _taskTabsScroll.ScrollControlIntoView(_taskTabButtons[_currentTaskIndex]);
        }

        private void LoadTaskToUi(int index)
        {
            _suppressUiSync = true;
            var task = _tasks[index];
            var kind = task.Kind;

            _kindCombo.SelectedIndex = Array.IndexOf(ComboKinds, kind);

            switch (kind)
            {
                case TaskbookTaskKind.FillInBlanks:
                    FillBlankRules.SyncBlanks(task.Blanks, task.Description);
                    label1.Text = $"Текст с пропусками «_» · задание №{index + 1}:";
                    textBox1.PlaceholderText = "Напишите текст, а пропуски отмечайте знаком «_»: I _ to school every day.";
                    label2.Text = "ОТВЕТЫ НА ПРОПУСКИ";
                    label4.Text = "Бонус, если все пропуски верны:";
                    break;
                case TaskbookTaskKind.Choice:
                    label1.Text = $"Вопрос с вариантами №{index + 1}:";
                    textBox1.PlaceholderText = "Введите вопрос...";
                    label2.Text = "ВАРИАНТЫ ОТВЕТА";
                    label4.Text = "Бонус за полностью верный ответ:";
                    break;
                case TaskbookTaskKind.Matching:
                    label1.Text = $"Задание на соответствие №{index + 1}:";
                    textBox1.PlaceholderText = "Напишите общую инструкцию: Соотнесите слова и переводы...";
                    label2.Text = "ПАРЫ (ЛЕВОЕ = ПРАВОЕ)";
                    label4.Text = "Бонус, если все пары верны:";
                    break;
                case TaskbookTaskKind.ShortAnswer:
                    if (task.Blanks.Count == 0)
                        task.Blanks.Add(new FillBlankDto());
                    label1.Text = $"Вопрос с коротким ответом №{index + 1}:";
                    textBox1.PlaceholderText = "Введите вопрос: Сколько будет 7 умножить на 8?";
                    label2.Text = "ПРАВИЛЬНЫЙ ОТВЕТ";
                    label4.Text = "Бонус за верный ответ:";
                    break;
                default:
                    label1.Text = $"Условие задачи №{index + 1}:";
                    textBox1.PlaceholderText = "Введите условие задачи...";
                    label2.Text = "ТЕСТОВЫЕ ПРИМЕРЫ";
                    label4.Text = "Бонусные баллы за все тесты:";
                    break;
            }

            button2.Visible = kind is not (TaskbookTaskKind.FillInBlanks or TaskbookTaskKind.ShortAnswer);
            button2.Text = kind switch
            {
                TaskbookTaskKind.Choice => "+  Добавить вариант",
                TaskbookTaskKind.Matching => "+  Добавить пару",
                _ => "+  Добавить тест",
            };

            textBox1.Text = task.Description;
            textBox5.Text = task.BonusPointsForAllTests.ToString();

            _testsFlow.SuspendLayout();
            _testsFlow.Controls.Clear();
            switch (kind)
            {
                case TaskbookTaskKind.FillInBlanks:
                    for (var i = 0; i < task.Blanks.Count; i++)
                        _testsFlow.Controls.Add(CreateBlankPanel(task.Blanks[i], i));
                    break;
                case TaskbookTaskKind.Choice:
                    for (var i = 0; i < task.Options.Count; i++)
                        _testsFlow.Controls.Add(CreateOptionPanel(task.Options[i], i));
                    break;
                case TaskbookTaskKind.Matching:
                    for (var i = 0; i < task.Pairs.Count; i++)
                        _testsFlow.Controls.Add(CreatePairPanel(task.Pairs[i], i));
                    break;
                case TaskbookTaskKind.ShortAnswer:
                    _testsFlow.Controls.Add(CreateBlankPanel(task.Blanks[0], 0, "ОТВЕТ"));
                    break;
                default:
                    foreach (var test in task.Tests)
                        _testsFlow.Controls.Add(CreateTestPanel(test));
                    break;
            }
            _testsFlow.ResumeLayout(true);
            LayoutTestPanels();

            UpdateTaskTabSelection();
            _suppressUiSync = false;
        }

        private void SaveCurrentTaskFromUi()
        {
            if (_tasks.Count == 0 || _currentTaskIndex < 0 || _currentTaskIndex >= _tasks.Count)
                return;

            var task = _tasks[_currentTaskIndex];
            task.Description = textBox1.Text;
            task.BonusPointsForAllTests = ParseIntOrDefault(textBox5.Text, task.BonusPointsForAllTests);

            switch (task.Kind)
            {
                case TaskbookTaskKind.FillInBlanks:
                    task.Blanks.Clear();
                    CollectPanels(task.Blanks);
                    FillBlankRules.SyncBlanks(task.Blanks, task.Description);
                    return;
                case TaskbookTaskKind.Choice:
                    task.Options.Clear();
                    CollectPanels(task.Options);
                    return;
                case TaskbookTaskKind.Matching:
                    task.Pairs.Clear();
                    CollectPanels(task.Pairs);
                    return;
                case TaskbookTaskKind.ShortAnswer:
                    var answers = new List<FillBlankDto>();
                    CollectPanels(answers);
                    task.Blanks.Clear();
                    task.Blanks.Add(answers.Count > 0 ? answers[0] : new FillBlankDto());
                    return;
            }

            task.Tests.Clear();
            foreach (Control control in _testsFlow.Controls)
            {
                if (control is Panel panel && panel.Tag is TestCaseDto test)
                {
                    ReadTestFromPanel(panel, test);
                    task.Tests.Add(test);
                }
            }
        }

        private void CollectPanels<T>(List<T> target) where T : class
        {
            foreach (Control control in _testsFlow.Controls)
            {
                if (control is Panel panel && panel.Tag is T item)
                    target.Add(item);
            }
        }

        private void SyncDescriptionFromUi()
        {
            if (_suppressUiSync) return;

            var task = _tasks[_currentTaskIndex];
            task.Description = textBox1.Text;

            if (task.Kind != TaskbookTaskKind.FillInBlanks)
                return;

            var countBefore = task.Blanks.Count;
            FillBlankRules.SyncBlanks(task.Blanks, task.Description);
            if (task.Blanks.Count != countBefore)
                RefreshFillAnswerPanels();
            else
                UpdateBlankContexts();
        }

        private void SyncBonusPointsFromUi()
        {
            if (_suppressUiSync) return;
            _tasks[_currentTaskIndex].BonusPointsForAllTests =
                ParseIntOrDefault(textBox5.Text, _tasks[_currentTaskIndex].BonusPointsForAllTests);
        }

        private void AddTestToCurrentTask()
        {
            var task = _tasks[_currentTaskIndex];
            Control added;
            switch (task.Kind)
            {
                case TaskbookTaskKind.FillInBlanks:
                case TaskbookTaskKind.ShortAnswer:
                    return;
                case TaskbookTaskKind.Choice:
                    SaveCurrentTaskFromUi();
                    var option = new ChoiceOptionDto();
                    task.Options.Add(option);
                    added = CreateOptionPanel(option, task.Options.Count - 1);
                    break;
                case TaskbookTaskKind.Matching:
                    SaveCurrentTaskFromUi();
                    var pair = new MatchPairDto();
                    task.Pairs.Add(pair);
                    added = CreatePairPanel(pair, task.Pairs.Count - 1);
                    break;
                default:
                    SaveCurrentTaskFromUi();
                    var test = new TestCaseDto();
                    task.Tests.Add(test);
                    added = CreateTestPanel(test);
                    break;
            }

            _testsFlow.Controls.Add(added);
            LayoutTestPanels();
            _testsFlow.ScrollControlIntoView(added);
        }

        private void ScheduleLayoutTestPanels()
        {
            if (IsDisposed)
                return;

            if (!IsHandleCreated)
            {
                LayoutTestPanels();
                return;
            }

            BeginInvoke(LayoutTestPanels);
        }

        private void LayoutTestPanels()
        {
            if (_layingOutTests || _testsFlow.Controls.Count == 0)
                return;

            _layingOutTests = true;
            try
            {
                ApplyTestPanelWidths();
            }
            finally
            {
                _layingOutTests = false;
            }
        }

        private void ApplyTestPanelWidths()
        {
            var width = Math.Max(160, _testsFlow.ClientSize.Width - 2);

            foreach (Control control in _testsFlow.Controls)
            {
                if (control is not Panel panel)
                    continue;

                panel.Width = width;

                if (panel.Tag is FillBlankDto)
                {
                    ApplyBlankPanelWidths(panel);
                    continue;
                }

                if (panel.Tag is ChoiceOptionDto)
                {
                    ApplyOptionPanelWidths(panel);
                    continue;
                }

                if (panel.Tag is MatchPairDto)
                {
                    ApplyPairPanelWidths(panel);
                    continue;
                }

                Button? removeButton = null;
                TextBox? inputBox = null;
                TextBox? outputBox = null;
                TextBox? pointsBox = null;
                Label? pointsLabel = null;
                Label? outputHeading = null;
                foreach (Control child in panel.Controls)
                {
                    if (child is Button removeBtn && (string?)removeBtn.Tag == TestRemoveButtonTag)
                        removeButton = removeBtn;
                    else if (child is TextBox inputCandidate && inputCandidate.Multiline && inputCandidate.PlaceholderText == "Данные для проверки")
                        inputBox = inputCandidate;
                    else if (child is TextBox outputCandidate && outputCandidate.Multiline && outputCandidate.PlaceholderText == "Результат")
                        outputBox = outputCandidate;
                    else if (child is TextBox pointsCandidate && pointsCandidate.PlaceholderText == "25" && !pointsCandidate.Multiline)
                        pointsBox = pointsCandidate;
                    else if (child is Label label && label.Text == "Баллы:")
                        pointsLabel = label;
                    else if (child is Label headingCandidate && headingCandidate.Text == "РЕЗУЛЬТАТ")
                        outputHeading = headingCandidate;
                }

                if (removeButton != null)
                    removeButton.Location = new Point(width - removeButton.Width - 3, 2);

                if (inputBox != null && outputBox != null)
                {
                    var fieldWidth = Math.Max(40, (width - 18) / 2);
                    inputBox.SetBounds(4, 21, fieldWidth, 52);
                    outputBox.SetBounds(inputBox.Right + 6, 21, Math.Max(40, width - inputBox.Right - 14), 52);
                    if (outputHeading != null)
                        outputHeading.Left = outputBox.Left;
                }
                if (pointsBox != null)
                {
                    pointsBox.Left = Math.Max(128, width - TestCloseButtonReserve - pointsBox.Width - 4);
                    if (pointsLabel != null)
                        pointsLabel.Left = Math.Max(3, pointsBox.Left - pointsLabel.Width - 4);
                }
            }
        }

        private void RemoveItemPanel(Panel panel)
        {
            var task = _tasks[_currentTaskIndex];
            switch (panel.Tag)
            {
                case TestCaseDto test:
                    if (task.Tests.Count <= 1)
                    {
                        MessageBox.Show("Нельзя удалить последний тест.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    SaveCurrentTaskFromUi();
                    task.Tests.Remove(test);
                    break;
                case ChoiceOptionDto option:
                    if (task.Options.Count <= 2)
                    {
                        MessageBox.Show("Нужно хотя бы два варианта ответа.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    SaveCurrentTaskFromUi();
                    task.Options.Remove(option);
                    break;
                case MatchPairDto pair:
                    if (task.Pairs.Count <= 2)
                    {
                        MessageBox.Show("Нужно хотя бы две пары.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    SaveCurrentTaskFromUi();
                    task.Pairs.Remove(pair);
                    break;
                default:
                    // Пропуски и короткий ответ существуют по одному на задание и не удаляются.
                    return;
            }

            _testsFlow.Controls.Remove(panel);
            panel.Dispose();
            LoadTaskToUi(_currentTaskIndex);
        }

        private Panel CreateTestPanel(TestCaseDto test)
        {
            var panel = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(220, 112),
                BackColor = _surfaceColor,
                Margin = new Padding(0, 0, 0, 6),
                Tag = test,
            };

            var remove = new Button
            {
                Text = "×",
                Tag = TestRemoveButtonTag,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Size = new Size(22, 22),
                TabStop = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = _raisedSurfaceColor,
                ForeColor = _mutedColor,
                Cursor = Cursors.Hand,
            };
            remove.FlatAppearance.BorderSize = 0;
            remove.Click += (_, _) => RemoveItemPanel(panel);

            var inputLabel = new Label
            {
                Text = "ВВОД",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = _mutedColor,
                Location = new Point(5, 3),
            };
            var outputLabel = new Label
            {
                Text = "РЕЗУЛЬТАТ",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = _mutedColor,
                Location = new Point(110, 3),
            };
            var input = new TextBox
            {
                Multiline = true,
                PlaceholderText = "Данные для проверки",
                Location = new Point(4, 21),
                Size = new Size(96, 52),
                Text = test.Input,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                BackColor = _surfaceColor,
                ForeColor = _inkColor,
            };
            var output = new TextBox
            {
                Multiline = true,
                PlaceholderText = "Результат",
                Location = new Point(106, 21),
                Size = new Size(82, 52),
                Text = test.Output,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9F),
                BackColor = _surfaceColor,
                ForeColor = _inkColor,
            };
            var show = new CheckBox
            {
                Text = "Показывать",
                AutoSize = true,
                Location = new Point(4, 81),
                Checked = test.ShowToStudent,
                ForeColor = _mutedColor,
            };
            var pointsLabel = new Label
            {
                Text = "Баллы:",
                AutoSize = true,
                Location = new Point(128, 82),
                ForeColor = _mutedColor,
            };
            var points = new TextBox
            {
                PlaceholderText = "25",
                Location = new Point(178, 79),
                Size = new Size(38, 23),
                Text = test.Points > 0 ? test.Points.ToString() : "",
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _surfaceColor,
                ForeColor = _inkColor,
            };

            void Bind()
            {
                test.Input = input.Text;
                test.Output = output.Text;
                test.ShowToStudent = show.Checked;
                test.Points = ParseIntOrDefault(points.Text, 25);
            }

            input.TextChanged += (_, _) => Bind();
            output.TextChanged += (_, _) => Bind();
            show.CheckedChanged += (_, _) => Bind();
            points.TextChanged += (_, _) => Bind();

            panel.Controls.Add(inputLabel);
            panel.Controls.Add(outputLabel);
            panel.Controls.Add(input);
            panel.Controls.Add(output);
            panel.Controls.Add(show);
            panel.Controls.Add(pointsLabel);
            panel.Controls.Add(points);
            panel.Controls.Add(remove);

            return panel;
        }

        private static void ReadTestFromPanel(Panel panel, TestCaseDto test)
        {
            foreach (Control c in panel.Controls)
            {
                switch (c)
                {
                    case TextBox tb when tb.Multiline && tb.PlaceholderText == "Данные для проверки":
                        test.Input = tb.Text;
                        break;
                    case TextBox tb when tb.Multiline && tb.PlaceholderText == "Результат":
                        test.Output = tb.Text;
                        break;
                    case TextBox tb when tb.PlaceholderText == "25" && !tb.Multiline:
                        test.Points = ParseIntOrDefault(tb.Text, 25);
                        break;
                    case CheckBox cb:
                        test.ShowToStudent = cb.Checked;
                        break;
                }
            }
        }

        private void OnKindComboChanged()
        {
            if (_suppressUiSync || _currentTaskIndex < 0 || _currentTaskIndex >= _tasks.Count)
                return;

            var comboIndex = _kindCombo.SelectedIndex;
            if (comboIndex < 0 || comboIndex >= ComboKinds.Length)
                return;

            SaveCurrentTaskFromUi();
            var task = _tasks[_currentTaskIndex];
            var kind = ComboKinds[comboIndex];
            if (task.Kind == kind)
                return;

            task.Kind = kind;
            switch (kind)
            {
                case TaskbookTaskKind.FillInBlanks:
                    FillBlankRules.SyncBlanks(task.Blanks, task.Description);
                    break;
                case TaskbookTaskKind.Choice:
                    if (task.Options.Count == 0)
                    {
                        task.Options.Add(new ChoiceOptionDto { Correct = true });
                        task.Options.Add(new ChoiceOptionDto());
                    }
                    break;
                case TaskbookTaskKind.Matching:
                    if (task.Pairs.Count == 0)
                    {
                        task.Pairs.Add(new MatchPairDto());
                        task.Pairs.Add(new MatchPairDto());
                    }
                    break;
                case TaskbookTaskKind.ShortAnswer:
                    if (task.Blanks.Count == 0)
                        task.Blanks.Add(new FillBlankDto());
                    break;
                default:
                    if (task.Tests.Count == 0)
                        task.Tests.Add(new TestCaseDto());
                    break;
            }

            RebuildTaskTabs();
            LoadTaskToUi(_currentTaskIndex);
        }

        private Panel CreateBlankPanel(FillBlankDto blank, int index, string? headerText = null)
        {
            var panel = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(220, 128),
                BackColor = _surfaceColor,
                Margin = new Padding(0, 0, 0, 6),
                Tag = blank,
            };

            var header = new Label
            {
                Name = BlankHeaderName,
                Text = headerText ?? $"ПРОПУСК  {index + 1:00}",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = _inkColor,
                Location = new Point(5, 4),
            };
            var context = new Label
            {
                Name = BlankContextName,
                Text = BuildBlankContext(textBox1.Text, index),
                AutoSize = false,
                UseMnemonic = false,
                AutoEllipsis = true,
                Font = new Font("Segoe UI", 8F),
                ForeColor = _mutedColor,
                Location = new Point(5, 24),
                Size = new Size(210, 16),
            };
            var answerLabel = new Label
            {
                Text = "Ответы (варианты через |):",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F),
                ForeColor = _mutedColor,
                Location = new Point(5, 44),
            };
            var answer = new TextBox
            {
                Name = BlankAnswerName,
                PlaceholderText = "went | go | goes",
                Location = new Point(4, 62),
                Size = new Size(212, 23),
                Text = ComposeAnswerText(blank),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _surfaceColor,
                ForeColor = _inkColor,
            };
            var pointsLabel = new Label
            {
                Text = "Баллы:",
                AutoSize = true,
                ForeColor = _mutedColor,
                Location = new Point(5, 94),
            };
            var points = new TextBox
            {
                Name = BlankPointsName,
                PlaceholderText = "25",
                Location = new Point(52, 91),
                Size = new Size(38, 23),
                Text = blank.Points > 0 ? blank.Points.ToString() : "",
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _surfaceColor,
                ForeColor = _inkColor,
            };
            var show = new CheckBox
            {
                Name = BlankShowName,
                Text = "Раскрывать ответ",
                AutoSize = true,
                ForeColor = _mutedColor,
                Checked = blank.ShowToStudent,
                Location = new Point(100, 92),
            };

            void Bind()
            {
                var (answerText, extraVariants) = FillBlankRules.SplitAnswerVariants(answer.Text);
                blank.Answer = answerText;
                blank.AcceptedAnswers = extraVariants.ToList();
                blank.Points = ParseIntOrDefault(points.Text, 25);
                blank.ShowToStudent = show.Checked;
            }

            answer.TextChanged += (_, _) => Bind();
            points.TextChanged += (_, _) => Bind();
            show.CheckedChanged += (_, _) => Bind();

            panel.Controls.Add(header);
            panel.Controls.Add(context);
            panel.Controls.Add(answerLabel);
            panel.Controls.Add(answer);
            panel.Controls.Add(pointsLabel);
            panel.Controls.Add(points);
            panel.Controls.Add(show);
            return panel;
        }

        private static string ComposeAnswerText(FillBlankDto blank)
        {
            if (blank.AcceptedAnswers.Count == 0)
                return blank.Answer;

            return string.Join(" | ", new[] { blank.Answer }.Concat(blank.AcceptedAnswers));
        }

        private void ApplyBlankPanelWidths(Panel panel)
        {
            var width = panel.Width;
            foreach (Control child in panel.Controls)
            {
                switch (child.Name)
                {
                    case BlankContextName:
                        child.SetBounds(5, 24, Math.Max(60, width - 14), 16);
                        break;
                    case BlankAnswerName:
                        child.SetBounds(4, 62, Math.Max(60, width - 14), 23);
                        break;
                    case BlankShowName:
                        child.Location = new Point(Math.Max(80, width - child.Width - 8), 92);
                        break;
                }
            }
        }

        private void RefreshFillAnswerPanels()
        {
            var task = _tasks[_currentTaskIndex];
            if (task.Kind != TaskbookTaskKind.FillInBlanks)
                return;

            _suppressUiSync = true;
            _testsFlow.SuspendLayout();
            _testsFlow.Controls.Clear();
            for (var i = 0; i < task.Blanks.Count; i++)
                _testsFlow.Controls.Add(CreateBlankPanel(task.Blanks[i], i));
            _testsFlow.ResumeLayout(true);
            _suppressUiSync = false;
            LayoutTestPanels();
        }

        private void UpdateBlankContexts()
        {
            var text = textBox1.Text;
            var index = 0;
            foreach (Control control in _testsFlow.Controls)
            {
                if (control is not Panel panel || panel.Tag is not FillBlankDto)
                    continue;

                foreach (Control child in panel.Controls)
                {
                    if (child.Name == BlankContextName)
                        child.Text = BuildBlankContext(text, index);
                }
                index++;
            }
        }

        private static string BuildBlankContext(string description, int blankIndex)
        {
            var segments = FillBlankRules.SplitSegments(description);
            if (blankIndex + 1 >= segments.Length)
                return "";

            static string Snippet(string value, int count, bool fromEnd)
            {
                var compact = value.Replace('\n', ' ').Replace('\r', ' ').Trim();
                while (compact.Contains("  ", StringComparison.Ordinal))
                    compact = compact.Replace("  ", " ", StringComparison.Ordinal);
                if (compact.Length <= count)
                    return compact;
                return fromEnd ? compact[^count..] : compact[..count];
            }

            var before = Snippet(segments[blankIndex], 20, fromEnd: true);
            var after = Snippet(segments[blankIndex + 1], 14, fromEnd: false);
            var combined = $"…{before} ___ {after}…";
            return combined.Length > 52 ? combined[..52] + "…" : combined;
        }

        private Button CreatePanelRemoveButton()
        {
            var remove = new Button
            {
                Text = "×",
                Tag = TestRemoveButtonTag,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Size = new Size(22, 22),
                TabStop = false,
                FlatStyle = FlatStyle.Flat,
                BackColor = _raisedSurfaceColor,
                ForeColor = _mutedColor,
                Cursor = Cursors.Hand,
            };
            remove.FlatAppearance.BorderSize = 0;
            return remove;
        }

        private Panel CreateOptionPanel(ChoiceOptionDto option, int index)
        {
            var panel = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(220, 82),
                BackColor = _surfaceColor,
                Margin = new Padding(0, 0, 0, 6),
                Tag = option,
            };

            var header = new Label
            {
                Name = OptionHeaderName,
                Text = $"ВАРИАНТ  {index + 1:00}",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = _inkColor,
                Location = new Point(5, 4),
            };
            var remove = CreatePanelRemoveButton();
            remove.Click += (_, _) => RemoveItemPanel(panel);

            var text = new TextBox
            {
                Name = OptionTextName,
                PlaceholderText = "Текст варианта",
                Location = new Point(4, 21),
                Size = new Size(212, 23),
                Text = option.Text,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _surfaceColor,
                ForeColor = _inkColor,
            };
            var correct = new CheckBox
            {
                Name = OptionCorrectName,
                Text = "Верный",
                AutoSize = true,
                ForeColor = _mutedColor,
                Checked = option.Correct,
                Location = new Point(4, 52),
            };
            var pointsLabel = new Label
            {
                Name = OptionPointsLabelName,
                Text = "Баллы:",
                AutoSize = true,
                ForeColor = _mutedColor,
                Location = new Point(92, 55),
            };
            var points = new TextBox
            {
                Name = OptionPointsName,
                PlaceholderText = "25",
                Location = new Point(140, 52),
                Size = new Size(38, 23),
                Text = option.Points > 0 ? option.Points.ToString() : "",
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _surfaceColor,
                ForeColor = _inkColor,
            };

            void Bind()
            {
                option.Text = text.Text;
                option.Correct = correct.Checked;
                option.Points = ParseIntOrDefault(points.Text, 25);
            }

            text.TextChanged += (_, _) => Bind();
            correct.CheckedChanged += (_, _) => Bind();
            points.TextChanged += (_, _) => Bind();

            panel.Controls.Add(header);
            panel.Controls.Add(text);
            panel.Controls.Add(correct);
            panel.Controls.Add(pointsLabel);
            panel.Controls.Add(points);
            panel.Controls.Add(remove);

            return panel;
        }

        private Panel CreatePairPanel(MatchPairDto pair, int index)
        {
            var panel = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(220, 82),
                BackColor = _surfaceColor,
                Margin = new Padding(0, 0, 0, 6),
                Tag = pair,
            };

            var header = new Label
            {
                Name = PairHeaderName,
                Text = $"ПАРА  {index + 1:00}",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = _inkColor,
                Location = new Point(5, 4),
            };
            var remove = CreatePanelRemoveButton();
            remove.Click += (_, _) => RemoveItemPanel(panel);

            var left = new TextBox
            {
                Name = PairLeftName,
                PlaceholderText = "Левое",
                Location = new Point(4, 21),
                Size = new Size(102, 23),
                Text = pair.Left,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _surfaceColor,
                ForeColor = _inkColor,
            };
            var right = new TextBox
            {
                Name = PairRightName,
                PlaceholderText = "Правое",
                Location = new Point(112, 21),
                Size = new Size(104, 23),
                Text = pair.Right,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _surfaceColor,
                ForeColor = _inkColor,
            };
            var pointsLabel = new Label
            {
                Name = PairPointsLabelName,
                Text = "Баллы:",
                AutoSize = true,
                ForeColor = _mutedColor,
                Location = new Point(4, 55),
            };
            var points = new TextBox
            {
                Name = PairPointsName,
                PlaceholderText = "25",
                Location = new Point(52, 52),
                Size = new Size(38, 23),
                Text = pair.Points > 0 ? pair.Points.ToString() : "",
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _surfaceColor,
                ForeColor = _inkColor,
            };

            void Bind()
            {
                pair.Left = left.Text;
                pair.Right = right.Text;
                pair.Points = ParseIntOrDefault(points.Text, 25);
            }

            left.TextChanged += (_, _) => Bind();
            right.TextChanged += (_, _) => Bind();
            points.TextChanged += (_, _) => Bind();

            panel.Controls.Add(header);
            panel.Controls.Add(left);
            panel.Controls.Add(right);
            panel.Controls.Add(pointsLabel);
            panel.Controls.Add(points);
            panel.Controls.Add(remove);

            return panel;
        }

        private void ApplyOptionPanelWidths(Panel panel)
        {
            var width = panel.Width;
            Button? remove = null;
            foreach (Control child in panel.Controls)
            {
                if (child is Button button && (string?)button.Tag == TestRemoveButtonTag)
                    remove = button;
            }
            remove?.Location = new Point(width - remove.Width - 3, 2);

            foreach (Control child in panel.Controls)
            {
                switch (child.Name)
                {
                    case OptionTextName:
                        child.SetBounds(4, 21, Math.Max(60, width - 14), 23);
                        break;
                    case OptionCorrectName:
                        child.Location = new Point(4, 52);
                        break;
                    case OptionPointsLabelName:
                        child.Left = Math.Max(100, width - TestCloseButtonReserve - child.Width - 42);
                        break;
                    case OptionPointsName:
                        child.Left = Math.Max(140, width - TestCloseButtonReserve - child.Width - 4);
                        break;
                }
            }
        }

        private void ApplyPairPanelWidths(Panel panel)
        {
            var width = panel.Width;
            Button? remove = null;
            foreach (Control child in panel.Controls)
            {
                if (child is Button button && (string?)button.Tag == TestRemoveButtonTag)
                    remove = button;
            }
            remove?.Location = new Point(width - remove.Width - 3, 2);

            TextBox? left = null;
            foreach (Control child in panel.Controls)
            {
                switch (child.Name)
                {
                    case PairLeftName:
                        child.SetBounds(4, 21, Math.Max(40, (width - 18) / 2), 23);
                        left = (TextBox)child;
                        break;
                    case PairRightName:
                        child.SetBounds(0, 21, 104, 23);
                        break;
                    case PairPointsLabelName:
                        child.Location = new Point(4, 55);
                        break;
                    case PairPointsName:
                        child.Location = new Point(52, 52);
                        break;
                }
            }

            if (left != null)
            {
                foreach (Control child in panel.Controls)
                {
                    if (child.Name == PairRightName)
                        child.SetBounds(left.Right + 6, 21, Math.Max(40, width - left.Right - 14), 23);
                }
            }
        }

        private void OnLoadTaskbook()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Задачник PVZ (*.pvz)|*.pvz",
                DefaultExt = "pvz",
                CheckFileExists = true,
                Title = "Открыть задачник для редактирования",
                InitialDirectory = TaskbookUserFolders.ProjectsPath,
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var document = TaskbookPackageIO.Load(dialog.FileName);
                LoadFromDocument(document);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or NotSupportedException)
            {
                MessageBox.Show(this, $"Не удалось открыть задачник:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadFromDocument(TaskbookDocument document)
        {
            if (document.Tasks.Count == 0)
            {
                MessageBox.Show(this, "В файле нет задач.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveCurrentTaskFromUi();
            _tasks.Clear();
            foreach (var task in document.Tasks)
            {
                var copy = new TaskbookTaskDto
                {
                    Description = task.Description,
                    BonusPointsForAllTests = task.BonusPointsForAllTests,
                    Kind = task.Kind,
                    Tests = task.Tests.Select(test => new TestCaseDto
                    {
                        Input = test.Input,
                        Output = test.Output,
                        ShowToStudent = test.ShowToStudent,
                        Points = test.Points,
                    }).ToList(),
                    Blanks = task.Blanks.Select(blank => new FillBlankDto
                    {
                        Answer = blank.Answer,
                        AcceptedAnswers = blank.AcceptedAnswers.ToList(),
                        Points = blank.Points,
                        ShowToStudent = blank.ShowToStudent,
                    }).ToList(),
                    Options = task.Options.Select(option => new ChoiceOptionDto
                    {
                        Text = option.Text,
                        Correct = option.Correct,
                        Points = option.Points,
                    }).ToList(),
                    Pairs = task.Pairs.Select(pair => new MatchPairDto
                    {
                        Left = pair.Left,
                        Right = pair.Right,
                        Points = pair.Points,
                    }).ToList(),
                };

                switch (copy.Kind)
                {
                    case TaskbookTaskKind.FillInBlanks:
                        FillBlankRules.SyncBlanks(copy.Blanks, copy.Description);
                        break;
                    case TaskbookTaskKind.Choice:
                        if (copy.Options.Count == 0)
                        {
                            copy.Options.Add(new ChoiceOptionDto { Correct = true });
                            copy.Options.Add(new ChoiceOptionDto());
                        }
                        break;
                    case TaskbookTaskKind.Matching:
                        if (copy.Pairs.Count == 0)
                        {
                            copy.Pairs.Add(new MatchPairDto());
                            copy.Pairs.Add(new MatchPairDto());
                        }
                        break;
                    case TaskbookTaskKind.ShortAnswer:
                        if (copy.Blanks.Count == 0)
                            copy.Blanks.Add(new FillBlankDto());
                        else if (copy.Blanks.Count > 1)
                            copy.Blanks.RemoveRange(1, copy.Blanks.Count - 1);
                        break;
                    default:
                        if (copy.Tests.Count == 0)
                            copy.Tests.Add(new TestCaseDto());
                        break;
                }

                _tasks.Add(copy);
            }

            checkBox1.Checked = document.TopMost;
            checkBox2.Checked = document.FullScreen;
            checkBox3.Checked = document.SequentialUnlock;
            checkBox5.Checked = document.KillExplorer;
            _allowedCompilers = document.AllowedCompilers;
            textBox4.Clear();
            textBox4.PlaceholderText = "Пароль из файла недоступен, задайте заново";
            _currentTaskIndex = 0;
            RebuildTaskTabs();
            LoadTaskToUi(0);
            MessageBox.Show(
                this,
                "Задачник загружен. Пароль преподавателя в .pvz не хранится в открытом виде, укажите его снова перед сохранением.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private async void OnCreateTaskbook()
        {
            SaveCurrentTaskFromUi();

            if (string.IsNullOrWhiteSpace(textBox4.Text))
            {
                MessageBox.Show("Укажите пароль преподавателя.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_tasks.Count == 0)
            {
                MessageBox.Show("Добавьте хотя бы одну задачу.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            for (var i = 0; i < _tasks.Count; i++)
            {
                var task = _tasks[i];
                var title = $"В задании №{i + 1}";
                switch (task.Kind)
                {
                    case TaskbookTaskKind.FillInBlanks:
                        if (FillBlankRules.CountBlanks(task.Description) == 0)
                        {
                            MessageBox.Show($"{title} нет ни одного пропуска «_». Отметьте пропуски подчёркиванием.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (task.Blanks.Any(blank => string.IsNullOrWhiteSpace(blank.Answer)))
                        {
                            MessageBox.Show($"{title} у каждого пропуска «_» должен быть указан ответ.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        break;
                    case TaskbookTaskKind.Choice:
                        if (task.Options.Count < 2)
                        {
                            MessageBox.Show($"{title} нужно хотя бы два варианта ответа.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (task.Options.Any(option => string.IsNullOrWhiteSpace(option.Text)))
                        {
                            MessageBox.Show($"{title} у каждого варианта должен быть текст.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (!task.Options.Any(option => option.Correct))
                        {
                            MessageBox.Show($"{title} отметьте галочкой хотя бы один верный вариант.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        break;
                    case TaskbookTaskKind.Matching:
                        if (task.Pairs.Count < 2)
                        {
                            MessageBox.Show($"{title} нужно хотя бы две пары.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        if (task.Pairs.Any(pair => string.IsNullOrWhiteSpace(pair.Left) || string.IsNullOrWhiteSpace(pair.Right)))
                        {
                            MessageBox.Show($"{title} заполните обе части каждой пары.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        break;
                    case TaskbookTaskKind.ShortAnswer:
                        if (task.Blanks.All(blank => string.IsNullOrWhiteSpace(blank.Answer)))
                        {
                            MessageBox.Show($"{title} укажите правильный ответ.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        break;
                    default:
                        if (task.Tests.Count == 0)
                        {
                            MessageBox.Show($"У задачи №{i + 1} должен быть хотя бы один тест.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        break;
                }
            }

            var firstKind = _tasks[0].Kind;
            var subject = firstKind is TaskbookTaskKind.FillInBlanks or TaskbookTaskKind.Matching
                ? TaskbookCatalogSubjects.English
                : firstKind == TaskbookTaskKind.ShortAnswer
                    ? TaskbookCatalogSubjects.Math
                    : TaskbookCatalogSubjects.Informatics;
            var classNumber = subject == TaskbookCatalogSubjects.Informatics ? 6 : 5;
            if (!TaskbookDetailsDialog.TryChoose(this, subject, classNumber, out subject, out classNumber))
                return;

            if (!BuilderSettingsDialogs.TryPickDeliveryMode(this, out var mode))
                return;

            var document = TaskbookPublisher.CreateDocument(
                _tasks,
                textBox4.Text,
                checkBox3.Checked,
                checkBox1.Checked,
                checkBox2.Checked,
                checkBox5.Checked,
                _allowedCompilers);
            document.Subject = subject;
            document.ClassNumber = classNumber;

            if (mode == TaskbookDeliveryMode.Online)
            {
                TeacherOnlineCabinetForm.ShowForUpload(this, document);
                return;
            }

            bool buildExecutable = mode == TaskbookDeliveryMode.OfflineExecutable;
            using var dlg = new SaveFileDialog
            {
                Filter = buildExecutable
                    ? "Исполняемый файл PVZ (*.exe)|*.exe"
                    : "Задачник PVZ (*.pvz)|*.pvz",
                DefaultExt = buildExecutable ? "exe" : "pvz",
                AddExtension = true,
                FileName = buildExecutable ? "Задачник.exe" : "Задачник.pvz",
                InitialDirectory = TaskbookUserFolders.ProjectsPath,
                Title = buildExecutable
                    ? "Собрать задачник одним .exe в Документы\\PVZ\\projects"
                    : "Сохранить оффлайн-задачник в Документы\\PVZ\\projects",
            };
            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            UseWaitCursor = true;
            Enabled = false;
            try
            {
                var result = buildExecutable
                    ? await TaskbookPublisher.PublishExeAsync(dlg.FileName, document)
                    : await Task.Run(() => TaskbookPublisher.PublishPackage(dlg.FileName, document));
                if (!result.Success)
                {
                    MessageBox.Show(result.ErrorMessage ?? "Не удалось подготовить задачник.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show(
                    (buildExecutable
                        ? "Задачник собран одним файлом: ученику достаточно запустить его, PVZP ему не нужен. Внутри задачник не зашифрован, а Python и PascalABC.NET должны стоять на компьютере ученика."
                        : "Файл .pvz сжат и снабжён контрольной суммой, но не зашифрован. Передайте файл ученику для оффлайн-открытия; пароль преподавателя не нужен ученику и остаётся только у вас.")
                    + $"\n\n{result.OutputExePath ?? result.OutputPath}",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                MessageBox.Show(this, $"Не удалось подготовить задачник:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Enabled = true;
                UseWaitCursor = false;
            }
        }

        private static int ParseIntOrDefault(string? text, int defaultValue)
        {
            return int.TryParse(text, out var value) ? value : defaultValue;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}
