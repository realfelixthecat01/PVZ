// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using PVZ.Core;

namespace PVZ.Player;

/// <summary>Окно входа и регистрации ученика: имя, пароль и класс. Больше ничего спрашивать не будем.</summary>
internal sealed class StudentAccountForm : Form
{
    private readonly RadioButton _loginMode;
    private readonly RadioButton _registerMode;
    private readonly TextBox _fullName;
    private readonly TextBox _password;
    private readonly TextBox _className;
    private readonly Label _classLabel;
    private readonly Label _passwordLabel;
    private readonly Button _ok;
    private bool _busy;

    public LocalIdentity? Identity { get; private set; }

    public StudentAccountForm()
    {
        PvzFormIcon.TryApply(this);
        Text = "Аккаунт ученика";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        PvzTheme.ApplyWindow(this);
        PvzTheme.EnableAutoScale(this);

        _loginMode = new RadioButton
        {
            Text = "Я уже регистрировался",
            Checked = true,
            AutoSize = true,
            ForeColor = PvzTheme.Ink,
            Location = new Point(18, 16),
        };
        _registerMode = new RadioButton
        {
            Text = "Я здесь впервые",
            AutoSize = true,
            ForeColor = PvzTheme.Ink,
            Location = new Point(190, 16),
        };

        _fullName = AddField(46, "Как вас зовут? Имя и фамилия", "Аня Козлова", out _);
        _className = AddField(104, "В каком вы классе?", "9А", out _classLabel);
        _password = AddField(162, "Пароль (от 6 символов)", "Пароль", out _passwordLabel);
        _password.UseSystemPasswordChar = true;

        _ok = PvzTheme.PrimaryButton("Войти");
        _ok.Click += OnAccept;
        var cancel = PvzTheme.GhostButton("Отмена");
        cancel.DialogResult = DialogResult.Cancel;
        cancel.SetBounds(288, 226, 94, 38);

        Controls.AddRange([_loginMode, _registerMode, _ok, cancel]);
        AcceptButton = _ok;
        CancelButton = cancel;

        _loginMode.CheckedChanged += (_, _) => ApplyMode();
        ApplyMode();
        Shown += (_, _) => _fullName.Focus();
    }

    private TextBox AddField(int top, string labelText, string placeholder, out Label label)
    {
        label = new Label
        {
            Text = labelText,
            AutoSize = true,
            ForeColor = PvzTheme.Muted,
            Font = PvzTheme.Caption,
            Bounds = new Rectangle(18, top, 364, 18),
        };
        var input = PvzTheme.InputBox(placeholder);
        input.SetBounds(18, top + 20, 364, 32);
        Controls.Add(label);
        Controls.Add(input);
        return input;
    }

    private void ApplyMode()
    {
        var registering = _registerMode.Checked;
        _className.Visible = registering;
        _classLabel.Visible = registering;
        // При входе сдвигаем пароль на место класса, чтобы окно оставалось компактным.
        _password.SetBounds(18, registering ? 162 : 104, 364, 32);
        _passwordLabel.SetBounds(18, registering ? 142 : 84, 364, 18);
        _ok.Text = registering ? "Создать аккаунт" : "Войти";
        _ok.SetBounds(148, 226, 130, 38);
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _loginMode.Enabled = !busy;
        _registerMode.Enabled = !busy;
        _fullName.Enabled = !busy;
        _password.Enabled = !busy;
        _className.Enabled = !busy;
        _ok.Enabled = !busy;
        _ok.Text = busy ? "Подключение…" : _registerMode.Checked ? "Создать аккаунт" : "Войти";
    }

    private async void OnAccept(object? sender, EventArgs e)
    {
        if (_busy)
            return;
        if (string.IsNullOrWhiteSpace(_fullName.Text) || string.IsNullOrEmpty(_password.Text))
        {
            MessageBox.Show(this, "Введите имя с фамилией и пароль.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_registerMode.Checked && string.IsNullOrWhiteSpace(_className.Text))
        {
            MessageBox.Show(this, "Напишите ваш класс, например 9А.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        SetBusy(true);
        try
        {
            var serverUrl = await TaskbookOnlineClient.ResolveServerUrlAsync();
            var registering = _registerMode.Checked;
            var receipt = registering
                ? await TaskbookOnlineClient.AccountRegisterAsync(
                    AccountRoles.Student,
                    _fullName.Text.Trim(),
                    _password.Text,
                    _className.Text.Trim(),
                    serverUrl)
                : await TaskbookOnlineClient.AccountLoginAsync(
                    _fullName.Text.Trim(),
                    _password.Text,
                    serverUrl);
            var profile = receipt.Profile;
            Identity = new LocalIdentity(
                receipt.AccessToken,
                receipt.ExpiresAt,
                profile.Role,
                profile.FullName,
                profile.ClassName,
                profile.IsGuest);
            LocalIdentityStore.Save("student.json", Identity);
            DialogResult = DialogResult.OK;
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException)
        {
            MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
                SetBusy(false);
        }
    }
}

/// <summary>Результаты ученика по всем задачникам, где он работал под своим аккаунтом.</summary>
internal sealed class StudentResultsForm : Form
{
    private readonly LocalIdentity _identity;
    private readonly DataGridView _grid = new();
    private readonly Label _status = new();

    public StudentResultsForm(LocalIdentity identity)
    {
        _identity = identity;
        PvzFormIcon.TryApply(this);
        Text = "Мои результаты";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(720, 420);
        ClientSize = new Size(760, 460);
        PvzTheme.ApplyWindow(this);
        PvzTheme.EnableAutoScale(this);

        _status.Text = "Загружаю результаты…";
        _status.AutoSize = false;
        _status.TextAlign = ContentAlignment.MiddleLeft;
        _status.ForeColor = PvzTheme.Muted;
        _status.SetBounds(16, 12, 500, 22);

        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.AllowUserToResizeRows = false;
        _grid.ReadOnly = true;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.BackgroundColor = SystemColors.Window;
        _grid.BorderStyle = BorderStyle.None;
        _grid.SetBounds(16, 40, 728, 380);
        _grid.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

        var columns = new (string Name, string Title, float Weight)[]
        {
            ("Taskbook", "Задачник", 22),
            ("Class", "Класс", 8),
            ("Score", "Баллы", 14),
            ("Tasks", "Задания", 14),
            ("Teacher", "Учитель", 22),
            ("Date", "Дата", 20),
        };
        foreach (var (name, title, weight) in columns)
        {
            _grid.Columns.Add(name, title);
            _grid.Columns[^1].FillWeight = weight;
        }

        Controls.Add(_status);
        Controls.Add(_grid);

        var close = PvzTheme.GhostButton("Закрыть");
        close.SetBounds(644, 424, 100, 34);
        close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        close.DialogResult = DialogResult.Cancel;
        Controls.Add(close);
        CancelButton = close;

        Shown += async (_, _) => await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            var serverUrl = await TaskbookOnlineClient.ResolveServerUrlAsync();
            var history = await TaskbookOnlineClient.StudentHistoryAsync(_identity.AccessToken, serverUrl);
            _grid.Rows.Clear();
            foreach (var row in history)
            {
                var subject = TaskbookCatalogSubjects.All.FirstOrDefault(item => item.Id == row.Subject)?.DisplayName
                    ?? (string.IsNullOrWhiteSpace(row.Subject) ? "Задачник" : row.Subject);
                var classText = row.ClassNumber is null ? "" : $"{row.ClassNumber} класс";
                _grid.Rows.Add(
                    subject,
                    classText,
                    $"{row.EarnedPoints} из {row.MaximumPoints}",
                    $"{row.CompletedTasks} из {row.TotalTasks}",
                    row.TeacherName,
                    row.UpdatedAt.ToLocalTime().ToString("d MMMM yyyy, HH:mm"));
            }
            _status.Text = history.Count == 0
                ? "Пока пусто. Решите онлайн-задачник, и результат появится здесь."
                : $"Задачников в истории: {history.Count}.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException)
        {
            _status.Text = "Не удалось загрузить результаты.";
            MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
