// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using PVZ.Core;

namespace PVZ.Player;

public sealed class StudentStartForm : Form
{
    private const int ContentWidth = 680;
    private const int FieldWidth = 360;

    private readonly List<(Control Control, int Top)> _layoutItems = new();
    private readonly float _scale;
    private LocalIdentity? _identity;

    public StudentStartForm()
    {
        PvzFormIcon.TryApply(this);
        _scale = PvzTheme.ScreenScale(this);
        Text = "Пункт Выдачи Задачек: ученик";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = ScaleSize(new Size(720, 500));
        ClientSize = ScaleSize(new Size(860, 600));
        PvzTheme.ApplyWindow(this);
        Resize += (_, _) => RelayoutCentered();
        PvzTheme.FitToScreen(this);
        AutoScroll = true;
        _identity = LocalIdentityStore.Load("student.json");
        ShowWelcome();
    }

    private Size ScaleSize(Size size) => new((int)(size.Width * _scale), (int)(size.Height * _scale));

    private void ShowWelcome()
    {
        ClearScreen();
        var title = CreateCenteredLabel("Добро пожаловать!", 25F, FontStyle.Bold);
        var subtitle = CreateCenteredLabel("Пункт Выдачи Задачек", 13F, FontStyle.Regular, PvzTheme.Muted);
        var info = CreateCenteredLabel("Чтобы начать, выберите способ получения задачника.", 10.5F, FontStyle.Regular, PvzTheme.Muted);
        var continueButton = CreateButton("Продолжить", primary: true, PvzFormButtonIcons.Glyphs.Continue);
        continueButton.Click += (_, _) => ShowSourceChoices();
        AddCentered(title, 95);
        AddCentered(subtitle, 158);
        AddCentered(info, 218);
        AddCentered(continueButton, 292);
    }

    private void ShowSourceChoices()
    {
        ClearScreen();
        var title = CreateCenteredLabel("Получение задачника", 23F, FontStyle.Bold);
        var subtitle = CreateCenteredLabel("Оффлайн откроет файл на устройстве; онлайн потребует подключения к серверу.", 10.5F, FontStyle.Regular, PvzTheme.Muted);
        var offline = CreateButton("Открыть оффлайн-файл .pvz", primary: true, PvzFormButtonIcons.Glyphs.OpenFile);
        var online = CreateButton("Ввести онлайн-токен", primary: false, PvzFormButtonIcons.Glyphs.Cloud);
        var back = CreateButton("Назад", primary: false, PvzFormButtonIcons.Glyphs.Back);
        offline.Click += (_, _) => OpenOfflineTaskbook();
        online.Click += (_, _) => ShowOnlineTokenEntry();
        back.Click += (_, _) => ShowWelcome();
        AddCentered(title, 70);
        AddCentered(subtitle, 128);
        AddCentered(offline, 196);
        AddCentered(online, 262);

        if (_identity is null)
        {
            var account = CreateButton("Войти или создать аккаунт", primary: false, PvzFormButtonIcons.Glyphs.Teacher);
            account.Click += async (_, _) => await ShowAccountDialogAsync();
            AddCentered(account, 328);
        }
        else
        {
            var greeting = CreateCenteredLabel(
                $"Вы вошли как: {_identity.FullName}{(_identity.ClassName.Length > 0 ? $", {_identity.ClassName}" : "")}",
                10.5F,
                FontStyle.Regular,
                PvzTheme.Muted);
            var results = CreateButton("Мои результаты", primary: false, PvzFormButtonIcons.Glyphs.OpenFile);
            results.Click += (_, _) => ShowResults();
            var logout = CreateButton("Выйти из аккаунта", primary: false, PvzFormButtonIcons.Glyphs.Back);
            logout.Click += (_, _) => SignOut();
            AddCentered(greeting, 330);
            AddCentered(results, 366);
            AddCentered(logout, 430);
        }

        AddCentered(back, _identity is null ? 414 : 494);
    }

    private async Task ShowAccountDialogAsync()
    {
        using var dialog = new StudentAccountForm();
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Identity is not null)
        {
            _identity = dialog.Identity;
            ShowSourceChoices();
        }
    }

    private void SignOut()
    {
        LocalIdentityStore.Delete("student.json");
        _identity = null;
        ShowSourceChoices();
    }

    private void ShowResults()
    {
        if (_identity is null)
            return;
        using var results = new StudentResultsForm(_identity);
        results.ShowDialog(this);
    }

    private void ShowOnlineTokenEntry()
    {
        ClearScreen();
        var title = CreateCenteredLabel("Онлайн-задачник", 22F, FontStyle.Bold);
        var subtitle = CreateCenteredLabel(
            "Введите токен, выданный сервером после загрузки задачника учителем.\nНужны только имя, фамилия и класс.",
            10.5F,
            FontStyle.Regular,
            PvzTheme.Muted);
        var token = CreateStudentField("Токен задачника");
        var firstName = CreateStudentField("Имя");
        var lastName = CreateStudentField("Фамилия");
        var className = CreateStudentField("Класс");
        if (_identity is not null)
        {
            // Аккаунт ученика подставляет имя и класс: писать их вручную больше не нужно.
            var parts = _identity.FullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) firstName.Text = parts[0];
            if (parts.Length > 1) lastName.Text = parts[1];
            if (_identity.ClassName.Length > 0) className.Text = _identity.ClassName;
        }
        var open = CreateButton("Подключиться", primary: true, PvzFormButtonIcons.Glyphs.Connect);
        open.Click += async (_, _) =>
        {
            if (token.Text.Trim().Length != 6)
            {
                MessageBox.Show(this, "Введите шестизначный токен задачника.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(firstName.Text) ||
                string.IsNullOrWhiteSpace(lastName.Text) ||
                string.IsNullOrWhiteSpace(className.Text))
            {
                MessageBox.Show(this, "Укажите имя, фамилию и класс.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            await OpenOnlineTaskbookAsync(
                token.Text.Trim().ToUpperInvariant(),
                new OnlineStudentInfo(firstName.Text.Trim(), lastName.Text.Trim(), className.Text.Trim()));
        };
        var back = CreateButton("Назад", primary: false, PvzFormButtonIcons.Glyphs.Back);
        back.Click += (_, _) => ShowSourceChoices();
        AddCentered(title, 105);
        AddCentered(subtitle, 158);
        AddCentered(token, 240);
        AddCentered(firstName, 288);
        AddCentered(lastName, 336);
        AddCentered(className, 384);
        AddCentered(open, 444);
        AddCentered(back, 516);
    }

    private TextBox CreateStudentField(string placeholder)
    {
        var field = PvzTheme.InputBox(placeholder);
        field.AutoSize = false;
        field.Width = (int)(FieldWidth * _scale);
        field.Height = (int)(38 * _scale);
        return field;
    }

    private async Task OpenOnlineTaskbookAsync(string token, OnlineStudentInfo student)
    {
        UseWaitCursor = true;
        Enabled = false;
        try
        {
            var serverUrl = await TaskbookOnlineClient.ResolveServerUrlAsync();
            var document = await TaskbookOnlineClient.DownloadAsync(token, serverUrl);
            var session = await TaskbookOnlineClient.StartStudentSessionAsync(
                token, student, serverUrl, _identity?.AccessToken);
            Hide();
            using (var taskbook = new MainForm(document, session, serverUrl))
                taskbook.ShowDialog(this);
            if (!IsDisposed)
                Show();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or NotSupportedException)
        {
            MessageBox.Show(this, $"Не удалось получить задачник.\n\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
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

    private void OpenOfflineTaskbook()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Задачник PVZ (*.pvz)|*.pvz",
            DefaultExt = "pvz",
            CheckFileExists = true,
            Multiselect = false,
            Title = "Выберите оффлайн-задачник",
            InitialDirectory = TaskbookUserFolders.DownloadsPath,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        TaskbookDocument document;
        try
        {
            document = TaskbookPackageIO.Load(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or NotSupportedException)
        {
            MessageBox.Show(this, $"Не удалось открыть задачник:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Hide();
        using (var taskbook = new MainForm(document))
            taskbook.ShowDialog(this);
        if (!IsDisposed)
            Show();
    }

    private void ClearScreen()
    {
        foreach (Control control in Controls.Cast<Control>().ToList())
        {
            Controls.Remove(control);
            control.Dispose();
        }
        _layoutItems.Clear();
    }

    private void AddCentered(Control control, int top)
    {
        _layoutItems.Add((control, top));
        Controls.Add(control);
        RelayoutCentered();
    }

    private void RelayoutCentered()
    {
        var width = (int)(ContentWidth * _scale);
        foreach (var (control, top) in _layoutItems)
        {
            var scaledTop = (int)(top * _scale);
            if (control is Label label && label.AutoSize == false)
            {
                label.Width = width;
                label.Location = new Point((ClientSize.Width - width) / 2, scaledTop);
                continue;
            }

            control.Location = new Point((ClientSize.Width - control.Width) / 2, scaledTop);
        }
    }

    private Label CreateCenteredLabel(string text, float size, FontStyle style, Color? color = null)
    {
        var width = (int)(ContentWidth * _scale);
        var font = new Font("Segoe UI", size, style);
        var height = TextRenderer.MeasureText(
            text,
            font,
            new Size(width, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter).Height + (int)(8 * _scale);
        return new Label
        {
            Text = text,
            AutoSize = false,
            Width = width,
            Height = height,
            Font = font,
            ForeColor = color ?? PvzTheme.Ink,
            TextAlign = ContentAlignment.TopCenter,
        };
    }

    private Button CreateButton(string text, bool primary, char iconGlyph)
    {
        var button = primary ? PvzTheme.PrimaryButton(text) : PvzTheme.SecondaryButton(text);
        button.Size = new Size((int)(360 * _scale), (int)(52 * _scale));
        button.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
        PvzFormButtonIcons.Apply(button, iconGlyph, button.ForeColor);
        return button;
    }
}
