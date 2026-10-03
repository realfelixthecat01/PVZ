using PVZ.Core;

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
        private const int TaskTabRowHeight = 30;
        private const string TestRemoveButtonTag = "test-remove";
        private const int TestCloseButtonReserve = 26;

        private static readonly Color CanvasColor = Color.FromArgb(244, 246, 249);
        private static readonly Color SurfaceColor = Color.White;
        private static readonly Color InkColor = Color.FromArgb(30, 41, 59);
        private static readonly Color MutedColor = Color.FromArgb(100, 116, 139);
        private static readonly Color BorderColor = Color.FromArgb(218, 226, 235);
        private static readonly Color AccentColor = Color.FromArgb(37, 99, 165);

        public Form1()
        {
            InitializeComponent();
            MinimumSize = new Size(1000, 680);
            ClientSize = new Size(1120, 720);
            BackColor = CanvasColor;
            ForeColor = InkColor;
            Font = new Font("Segoe UI", 9.5F);
            SetupUi();
            Resize += (_, _) => LayoutEditor();
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
            panel1.BackColor = InkColor;
            panel2.BorderStyle = BorderStyle.None;
            panel2.BackColor = SurfaceColor;
            panel3.BorderStyle = BorderStyle.None;
            panel3.BackColor = Color.FromArgb(247, 249, 252);

            _headerPanel = new Panel { BackColor = CanvasColor };
            _headerAccent = new Panel { BackColor = AccentColor };
            _headerEyebrow = new Label
            {
                Name = "headerEyebrow",
                Text = "КОНСТРУКТОР ТЕСТИРОВАНИЯ",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = AccentColor,
            };
            _headerTitle = new Label
            {
                Name = "headerTitle",
                Text = "Пункт выдачи задач",
                AutoSize = true,
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = InkColor,
            };
            _headerSubtitle = new Label
            {
                Name = "headerSubtitle",
                Text = "Подготовьте условие, тесты и параметры нового задачника",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = MutedColor,
            };
            _headerPanel.Controls.AddRange([_headerAccent, _headerEyebrow, _headerTitle, _headerSubtitle]);
            Controls.Add(_headerPanel);

            _taskTabsScroll = new Panel
            {
                Location = new Point(8, 4),
                Size = new Size(panel1.Width - 50, 40),
                AutoScroll = true,
            };
            _taskTabsFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
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
            };
            _testsFlow.HorizontalScroll.Enabled = false;
            _testsFlow.HorizontalScroll.Visible = false;
            _testsFlow.Resize += (_, _) => LayoutTestPanels();
            _testsFlow.ControlAdded += (_, _) => ScheduleLayoutTestPanels();
            _testsFlow.ControlRemoved += (_, _) => ScheduleLayoutTestPanels();
            panel3.Controls.Add(_testsFlow);
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
            const int footerHeight = 76;
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

            label1.SetBounds(contentPadding, 12, Math.Max(80, testsLeft - contentPadding - 16), 22);
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
            button1.SetBounds(ClientSize.Width - margin - 210, footerTop + 8, 210, 42);

            LayoutTaskTabsScroll();
            LayoutTestPanels();
        }

        private static void StyleEditorControls(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Name is not ("headerEyebrow" or "headerTitle" or "headerSubtitle"))
                    control.Font = new Font("Segoe UI", 9.5F);
                control.ForeColor = InkColor;

                switch (control)
                {
                    case TextBox textBox:
                        textBox.BorderStyle = BorderStyle.FixedSingle;
                        textBox.BackColor = SurfaceColor;
                        break;
                    case Button button:
                        button.FlatStyle = FlatStyle.Flat;
                        button.FlatAppearance.BorderColor = BorderColor;
                        button.BackColor = Color.FromArgb(237, 242, 248);
                        button.Cursor = Cursors.Hand;
                        button.Height = Math.Max(button.Height, 30);
                        if (button.Name == "button1")
                        {
                            button.BackColor = AccentColor;
                            button.ForeColor = Color.White;
                            button.FlatAppearance.BorderColor = AccentColor;
                            button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                        }
                        break;
                    case Label label:
                        label.ForeColor = MutedColor;
                        if (label.Name == "headerTitle")
                        {
                            label.ForeColor = InkColor;
                        }
                        else if (label.Name == "headerEyebrow")
                        {
                            label.ForeColor = AccentColor;
                        }
                        else if (label.Name == "label1" || label.Name == "label2")
                        {
                            label.ForeColor = InkColor;
                            label.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                        }
                        break;
                    case CheckBox:
                        control.ForeColor = MutedColor;
                        break;
                }

                if (control.HasChildren)
                    StyleEditorControls(control);
            }
        }

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
            foreach (var tab in _taskTabButtons)
                tab.Dispose();
            _taskTabButtons.Clear();
            _taskTabsFlow.Controls.Clear();

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

                var tab = new Button
                {
                    Text = $"ЗАДАЧА  {i + 1:00}",
                    Size = new Size(94, TaskTabRowHeight),
                    Margin = new Padding(0),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = InkColor,
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
                    BackColor = Color.FromArgb(54, 65, 82),
                    ForeColor = Color.White,
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
                _taskTabButtons[i].BackColor = selected ? Color.FromArgb(53, 105, 164) : InkColor;
                _taskTabButtons[i].ForeColor = Color.White;
            }

            if (_currentTaskIndex >= 0 && _currentTaskIndex < _taskTabButtons.Count)
                _taskTabsScroll.ScrollControlIntoView(_taskTabButtons[_currentTaskIndex]);
        }

        private void LoadTaskToUi(int index)
        {
            _suppressUiSync = true;
            var task = _tasks[index];

            label1.Text = $"Условие задачи №{index + 1}:";
            textBox1.Text = task.Description;
            textBox5.Text = task.BonusPointsForAllTests.ToString();

            _testsFlow.SuspendLayout();
            _testsFlow.Controls.Clear();
            foreach (var test in task.Tests)
                _testsFlow.Controls.Add(CreateTestPanel(test));
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

        private void SyncDescriptionFromUi()
        {
            if (_suppressUiSync) return;
            _tasks[_currentTaskIndex].Description = textBox1.Text;
        }

        private void SyncBonusPointsFromUi()
        {
            if (_suppressUiSync) return;
            _tasks[_currentTaskIndex].BonusPointsForAllTests =
                ParseIntOrDefault(textBox5.Text, _tasks[_currentTaskIndex].BonusPointsForAllTests);
        }

        private void AddTestToCurrentTask()
        {
            SaveCurrentTaskFromUi();
            var test = new TestCaseDto();
            _tasks[_currentTaskIndex].Tests.Add(test);
            _testsFlow.Controls.Add(CreateTestPanel(test));
            LayoutTestPanels();
            _testsFlow.ScrollControlIntoView(_testsFlow.Controls[^1]);
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

        private void RemoveTestPanel(Panel panel, TestCaseDto test)
        {
            var task = _tasks[_currentTaskIndex];
            if (task.Tests.Count <= 1)
            {
                MessageBox.Show("Нельзя удалить последний тест.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SaveCurrentTaskFromUi();
            task.Tests.Remove(test);
            _testsFlow.Controls.Remove(panel);
            panel.Dispose();
            LayoutTestPanels();
        }

        private Panel CreateTestPanel(TestCaseDto test)
        {
            var panel = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(220, 112),
                BackColor = SurfaceColor,
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
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = MutedColor,
                Cursor = Cursors.Hand,
            };
            remove.FlatAppearance.BorderSize = 0;
            remove.Click += (_, _) => RemoveTestPanel(panel, test);

            var inputLabel = new Label
            {
                Text = "ВВОД",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = MutedColor,
                Location = new Point(5, 3),
            };
            var outputLabel = new Label
            {
                Text = "РЕЗУЛЬТАТ",
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = MutedColor,
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
            };
            var show = new CheckBox
            {
                Text = "Показывать",
                AutoSize = true,
                Location = new Point(4, 81),
                Checked = test.ShowToStudent,
            };
            var pointsLabel = new Label
            {
                Text = "Баллы:",
                AutoSize = true,
                Location = new Point(128, 82),
                ForeColor = MutedColor,
            };
            var points = new TextBox
            {
                PlaceholderText = "25",
                Location = new Point(178, 79),
                Size = new Size(38, 23),
                Text = test.Points > 0 ? test.Points.ToString() : "",
                BorderStyle = BorderStyle.FixedSingle,
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

        private async void OnCreateTaskbook()
        {
            SaveCurrentTaskFromUi();

            if (string.IsNullOrWhiteSpace(textBox4.Text))
            {
                MessageBox.Show("Укажите пароль преподавателя.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_tasks.Count == 0 || _tasks.Any(t => t.Tests.Count == 0))
            {
                MessageBox.Show("У каждой задачи должен быть хотя бы один тест.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Filter = "Задачник (*.exe)|*.exe",
                FileName = "Задачник.exe",
                Title = "Сохранить exe задачника",
            };
            if (dlg.ShowDialog() != DialogResult.OK)
                return;

            var document = TaskbookPublisher.CreateDocument(
                _tasks,
                textBox4.Text,
                checkBox3.Checked,
                checkBox1.Checked,
                checkBox2.Checked,
                checkBox5.Checked);

            UseWaitCursor = true;
            Enabled = false;
            try
            {
                var result = await TaskbookPublisher.PublishExeAsync(dlg.FileName, document);
                if (!result.Success)
                {
                    MessageBox.Show(result.ErrorMessage ?? "Ошибка сборки.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show(
                    $"Задачник создан:\n{result.OutputExePath}",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
    }
}
