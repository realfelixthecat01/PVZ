using PVZ.Core;

namespace PVZ.Player;

public sealed class StudentStartForm : Form
{
    private const int ContentWidth = 680;
    private const int FieldWidth = 360;

    private readonly List<(Control Control, int Top)> _layoutItems = new();
    private readonly float _scale;

    public StudentStartForm()
    {
        PvzFormIcon.TryApply(this);
        _scale = PvzTheme.ScreenScale(this);
        Text = "Пункт Выдачи Задачек — ученик";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = ScaleSize(new Size(720, 500));
        ClientSize = ScaleSize(new Size(860, 600));
        PvzTheme.ApplyWindow(this);
        Resize += (_, _) => RelayoutCentered();
        PvzTheme.FitToScreen(this);
        AutoScroll = true;
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
        AddCentered(title, 95);
        AddCentered(subtitle, 154);
        AddCentered(offline, 222);
        AddCentered(online, 294);
        AddCentered(back, 380);
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
        var server = CreateStudentField("Адрес сервера (ссылка из «Сервер и ссылка»)");
        server.Text = ServerAddressStore.Load() ?? string.Empty;
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
            if (TaskbookOnlineClient.RequireExplicitServerUrl && string.IsNullOrWhiteSpace(server.Text))
            {
                MessageBox.Show(
                    this,
                    "Вставьте ссылку на сервер, которую выдал учитель.",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            SaveServerAddress(server.Text);
            await OpenOnlineTaskbookAsync(
                token.Text.Trim().ToUpperInvariant(),
                new OnlineStudentInfo(firstName.Text.Trim(), lastName.Text.Trim(), className.Text.Trim()));
        };
        var back = CreateButton("Назад", primary: false, PvzFormButtonIcons.Glyphs.Back);
        back.Click += (_, _) => ShowSourceChoices();
        AddCentered(title, 105);
        AddCentered(subtitle, 158);
        AddCentered(token, 226);
        AddCentered(firstName, 270);
        AddCentered(lastName, 316);
        AddCentered(className, 362);
        AddCentered(server, 408);
        AddCentered(open, 460);
        AddCentered(back, 528);
    }

    private static void SaveServerAddress(string raw)
    {
        raw = raw.Trim();
        if (raw.Length == 0 || raw == TaskbookOnlineClient.DefaultServerUrl)
            ServerAddressStore.Save(null);
        else
            ServerAddressStore.Save(raw);
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
            var session = await TaskbookOnlineClient.StartStudentSessionAsync(token, student, serverUrl);
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
