using PVZ.Core;

namespace PVZ.Player;

internal sealed class StudentStartForm : Form
{
    private static readonly Color Canvas = Color.FromArgb(17, 24, 39);
    private static readonly Color Surface = Color.FromArgb(31, 41, 55);
    private static readonly Color RaisedSurface = Color.FromArgb(38, 50, 68);
    private static readonly Color Ink = Color.FromArgb(226, 232, 240);
    private static readonly Color Muted = Color.FromArgb(148, 163, 184);
    private static readonly Color Border = Color.FromArgb(71, 85, 105);
    private static readonly Color Accent = Color.FromArgb(37, 99, 235);

    public StudentStartForm()
    {
        Text = "Пункт Выдачи Задачек — ученик";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(720, 500);
        ClientSize = new Size(860, 600);
        BackColor = Canvas;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 10F);
        ShowWelcome();
    }

    private void ShowWelcome()
    {
        ClearScreen();
        var title = CreateLabel("Добро пожаловать!", 25F, FontStyle.Bold);
        var subtitle = CreateLabel("Пункт Выдачи Задачек", 13F, FontStyle.Regular, Muted);
        var info = CreateLabel("Чтобы начать, выберите способ получения задачника.", 10.5F, FontStyle.Regular, Muted);
        var continueButton = CreateButton("Продолжить", primary: true);
        continueButton.Click += (_, _) => ShowSourceChoices();
        AddCentered(title, 95);
        AddCentered(subtitle, 158);
        AddCentered(info, 218);
        AddCentered(continueButton, 292);
    }

    private void ShowSourceChoices()
    {
        ClearScreen();
        var title = CreateLabel("Получение задачника", 23F, FontStyle.Bold);
        var subtitle = CreateLabel("Оффлайн откроет файл на устройстве; онлайн потребует подключения к серверу.", 10.5F, FontStyle.Regular, Muted);
        var offline = CreateButton("Открыть оффлайн-файл .pvz", primary: true);
        var online = CreateButton("Ввести онлайн-токен", primary: false);
        var back = CreateButton("Назад", primary: false);
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
        var title = CreateLabel("Онлайн-задачник", 22F, FontStyle.Bold);
        var subtitle = CreateLabel(
            "Токен должен выдать сервер. Серверной админ-панели и проверки токенов пока нет.\nПароль преподавателя предназначен только учителю: не вводите его здесь и не передавайте ученикам.",
            10.5F,
            FontStyle.Regular,
            Muted);
        subtitle.AutoSize = false;
        subtitle.Size = new Size(680, 46);
        subtitle.TextAlign = ContentAlignment.MiddleCenter;
        var token = new TextBox
        {
            Width = 360,
            Height = 38,
            Location = new Point((ClientSize.Width - 360) / 2, 226),
            BackColor = Surface,
            ForeColor = Ink,
            BorderStyle = BorderStyle.FixedSingle,
            PlaceholderText = "Токен задачника",
        };
        var open = CreateButton("Подключиться", primary: true);
        open.Location = new Point((ClientSize.Width - open.Width) / 2, 284);
        open.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(token.Text))
            {
                MessageBox.Show(this, "Введите токен учителя.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(
                this,
                "Онлайн-доступ пока не настроен: токен не проверялся, файл с сервера не загружался. Попросите учителя передать файл .pvz и откройте его в оффлайн-режиме.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        };
        var back = CreateButton("Назад", primary: false);
        back.Location = new Point((ClientSize.Width - back.Width) / 2, 360);
        back.Click += (_, _) => ShowSourceChoices();
        AddCentered(title, 105);
        AddCentered(subtitle, 158);
        Controls.Add(token);
        Controls.Add(open);
        Controls.Add(back);
    }

    private void OpenOfflineTaskbook()
    {
        var downloadsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        using var dialog = new OpenFileDialog
        {
            Filter = "Задачник PVZ (*.pvz)|*.pvz",
            DefaultExt = "pvz",
            CheckFileExists = true,
            Multiselect = false,
            Title = "Выберите оффлайн-задачник",
            InitialDirectory = Directory.Exists(downloadsPath)
                ? downloadsPath
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        TaskbookDocument document;
        try
        {
            document = TaskbookPackageIO.Load(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or NotSupportedException)
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
    }

    private void AddCentered(Control control, int top)
    {
        control.Location = new Point((ClientSize.Width - control.Width) / 2, top);
        Controls.Add(control);
    }

    private Label CreateLabel(string text, float size, FontStyle style, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", size, style),
        ForeColor = color ?? Ink,
    };

    private static Button CreateButton(string text, bool primary) => new()
    {
        Text = text,
        Size = new Size(360, 52),
        FlatStyle = FlatStyle.Flat,
        BackColor = primary ? Accent : RaisedSurface,
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
        Cursor = Cursors.Hand,
        FlatAppearance = { BorderColor = primary ? Accent : Border },
    };
}
