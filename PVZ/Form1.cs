namespace PVZ
{
    public partial class Form1 : Form
    {
        private readonly List<TaskItem> _tasks = new();
        private readonly List<Button> _taskTabButtons = new();
        private int _currentTaskIndex;
        private bool _suppressUiSync;

        private FlowLayoutPanel _testsFlow = null!;
        private Panel _taskTabsScroll = null!;
        private FlowLayoutPanel _taskTabsFlow = null!;
        private bool _layingOutTests;
        private int _testsScrollBarReserve;
        private int _basePanel1Width;
        private int _basePanel2Width;
        private int _basePanel3Width;
        private int _baseButton1Left;
        private int _baseTextBox5Left;
        private const int TaskTabRowHeight = 23;
        private const string TestRemoveButtonTag = "test-remove";
        private const int TestCloseButtonReserve = 26;

        public Form1()
        {
            InitializeComponent();
            SetupUi();
            WireEvents();

            var firstTask = new TaskItem
            {
                Description = textBox1.Text,
                BonusPointsForAllTests = ParseIntOrDefault(textBox5.Text, 10),
            };
            firstTask.Tests.Add(new TestCase
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

            button3.Visible = false;
            button4.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button4.Location = new Point(panel1.Width - 27, 3);

            _taskTabsScroll = new Panel
            {
                Location = new Point(3, 3),
                Size = new Size(panel1.Width - 34, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
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
            _taskTabsScroll.Resize += (_, _) => LayoutTaskTabsScroll();
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
            button2.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            button2.Location = new Point(3, panel3.Height - button2.Height - 3);
            button2.Size = new Size(231, button2.Height);

            _basePanel1Width = panel1.Width;
            _basePanel2Width = panel2.Width;
            _basePanel3Width = panel3.Width;
            _baseButton1Left = button1.Left;
            _baseTextBox5Left = textBox5.Left;
        }

        private void WireEvents()
        {
            button2.Click += (_, _) => AddTestToCurrentTask();
            button4.Click += (_, _) => AddTask();
            button1.Click += (_, _) => OnCreateTaskbook();

            textBox1.TextChanged += (_, _) => SyncDescriptionFromUi();
            textBox5.TextChanged += (_, _) => SyncBonusPointsFromUi();

            checkBox1.CheckedChanged += (_, _) => TopMost = checkBox1.Checked;
            checkBox2.CheckedChanged += (_, _) =>
            {
                if (checkBox2.Checked)
                {
                    FormBorderStyle = FormBorderStyle.None;
                    WindowState = FormWindowState.Maximized;
                }
                else
                {
                    WindowState = FormWindowState.Normal;
                    FormBorderStyle = FormBorderStyle.Sizable;
                }
            };
        }

        private void AddTask()
        {
            SaveCurrentTaskFromUi();
            _tasks.Add(new TaskItem());
            _tasks[^1].Tests.Add(new TestCase());
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
                    Text = $"Задача {i + 1}",
                    Size = new Size(75, TaskTabRowHeight),
                    Margin = new Padding(0),
                    UseVisualStyleBackColor = true,
                };
                tab.Click += (_, _) => SelectTask(index);

                var removeTask = new Button
                {
                    Text = "×",
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    Size = new Size(23, TaskTabRowHeight),
                    Margin = new Padding(0),
                    UseVisualStyleBackColor = true,
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
            var needsHorizontalScroll = _taskTabsFlow.PreferredSize.Width > _taskTabsScroll.ClientSize.Width;
            var scrollBarHeight = needsHorizontalScroll ? SystemInformation.HorizontalScrollBarHeight : 0;

            _taskTabsScroll.Height = TaskTabRowHeight + scrollBarHeight;

            const int panel1VerticalPadding = 8;
            var newPanel1Height = TaskTabRowHeight + scrollBarHeight + panel1VerticalPadding;
            if (panel1.Height != newPanel1Height)
            {
                panel1.Height = newPanel1Height;
                panel2.Top = panel1.Bottom + 6;
            }
        }

        private void UpdateTaskTabSelection()
        {
            for (var i = 0; i < _taskTabButtons.Count; i++)
            {
                var selected = i == _currentTaskIndex;
                _taskTabButtons[i].Font = new Font(_taskTabButtons[i].Font, selected ? FontStyle.Bold : FontStyle.Regular);
                _taskTabButtons[i].BackColor = selected ? SystemColors.ControlLight : SystemColors.Control;
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
                if (control is Panel panel && panel.Tag is TestCase test)
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
            var test = new TestCase();
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
                var needsScroll = TestsNeedVerticalScroll();
                UpdateTestsScrollExpansion(needsScroll);
                ApplyTestPanelWidths();

                var needsScrollAfter = TestsNeedVerticalScroll();
                if (needsScroll != needsScrollAfter)
                {
                    UpdateTestsScrollExpansion(needsScrollAfter);
                    ApplyTestPanelWidths();
                }
            }
            finally
            {
                _layingOutTests = false;
            }
        }

        private void UpdateTestsScrollExpansion(bool needsVerticalScroll)
        {
            var targetReserve = needsVerticalScroll ? SystemInformation.VerticalScrollBarWidth : 0;
            if (targetReserve == _testsScrollBarReserve)
                return;

            var delta = targetReserve - _testsScrollBarReserve;
            _testsScrollBarReserve = targetReserve;

            panel3.Width = _basePanel3Width + _testsScrollBarReserve;
            panel2.Width = _basePanel2Width + _testsScrollBarReserve;
            panel1.Width = _basePanel1Width + _testsScrollBarReserve;

            textBox5.Left = _baseTextBox5Left + _testsScrollBarReserve;
            button1.Left = _baseButton1Left + _testsScrollBarReserve;

            ClientSize = new Size(ClientSize.Width + delta, ClientSize.Height);
        }

        private bool TestsNeedVerticalScroll()
        {
            var totalHeight = _testsFlow.Controls.Cast<Control>()
                .Sum(c => c.Height + c.Margin.Top + c.Margin.Bottom);
            return totalHeight > _testsFlow.ClientSize.Height;
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
                TextBox? outputBox = null;
                TextBox? pointsBox = null;
                Label? pointsLabel = null;
                foreach (Control child in panel.Controls)
                {
                    if (child is Button removeBtn && (string?)removeBtn.Tag == TestRemoveButtonTag)
                        removeButton = removeBtn;
                    else if (child is TextBox outputCandidate && outputCandidate.Multiline && outputCandidate.PlaceholderText == "вывод")
                        outputBox = outputCandidate;
                    else if (child is TextBox pointsCandidate && pointsCandidate.PlaceholderText == "25" && !pointsCandidate.Multiline)
                        pointsBox = pointsCandidate;
                    else if (child is Label label && label.Text == "Баллы:")
                        pointsLabel = label;
                }

                if (removeButton != null)
                    removeButton.Location = new Point(width - removeButton.Width - 3, 2);

                if (outputBox != null)
                    outputBox.Width = Math.Max(40, width - outputBox.Left - TestCloseButtonReserve - 2);
                if (pointsBox != null)
                {
                    pointsBox.Left = Math.Max(128, width - TestCloseButtonReserve - pointsBox.Width - 4);
                    if (pointsLabel != null)
                        pointsLabel.Left = Math.Max(3, pointsBox.Left - pointsLabel.Width - 4);
                }
            }
        }

        private void RemoveTestPanel(Panel panel, TestCase test)
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

        private Panel CreateTestPanel(TestCase test)
        {
            var panel = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(220, 98),
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
            };
            remove.Click += (_, _) => RemoveTestPanel(panel, test);

            var input = new TextBox
            {
                Multiline = true,
                PlaceholderText = "ввод",
                Location = new Point(0, 3),
                Size = new Size(105, 64),
                Text = test.Input,
            };
            var output = new TextBox
            {
                Multiline = true,
                PlaceholderText = "вывод",
                Location = new Point(108, 3),
                Size = new Size(80, 64),
                Text = test.Output,
            };
            var show = new CheckBox
            {
                Text = "Показывать",
                AutoSize = true,
                Location = new Point(3, 73),
                Checked = test.ShowToStudent,
            };
            var pointsLabel = new Label
            {
                Text = "Баллы:",
                AutoSize = true,
                Location = new Point(128, 74),
            };
            var points = new TextBox
            {
                PlaceholderText = "25",
                Location = new Point(178, 71),
                Size = new Size(38, 23),
                Text = test.Points > 0 ? test.Points.ToString() : "",
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

            panel.Controls.Add(input);
            panel.Controls.Add(output);
            panel.Controls.Add(show);
            panel.Controls.Add(pointsLabel);
            panel.Controls.Add(points);
            panel.Controls.Add(remove);

            return panel;
        }

        private static void ReadTestFromPanel(Panel panel, TestCase test)
        {
            foreach (Control c in panel.Controls)
            {
                switch (c)
                {
                    case TextBox tb when tb.Multiline && tb.PlaceholderText == "ввод":
                        test.Input = tb.Text;
                        break;
                    case TextBox tb when tb.Multiline && tb.PlaceholderText == "вывод":
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

        private void OnCreateTaskbook()
        {
            SaveCurrentTaskFromUi();
            var totalTests = _tasks.Sum(t => t.Tests.Count);
            MessageBox.Show(
                $"Готово к созданию задачника.\nЗадач: {_tasks.Count}\nТестов всего: {totalTests}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static int ParseIntOrDefault(string? text, int defaultValue)
        {
            return int.TryParse(text, out var value) ? value : defaultValue;
        }
    }
}
