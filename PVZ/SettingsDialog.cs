using PVZ.Core;

namespace PVZ;

internal sealed class ThemePalette
{
    private ThemePalette(AppTheme theme, Color accent)
    {
        IsDark = theme == AppTheme.Dark;
        Accent = accent;
        Canvas = IsDark ? Color.FromArgb(20, 27, 38) : Color.FromArgb(244, 246, 249);
        Surface = IsDark ? Color.FromArgb(30, 40, 54) : Color.White;
        SubtleSurface = IsDark ? Color.FromArgb(38, 50, 66) : Color.FromArgb(248, 250, 252);
        Ink = IsDark ? Color.FromArgb(231, 237, 245) : Color.FromArgb(30, 41, 59);
        Muted = IsDark ? Color.FromArgb(160, 174, 192) : Color.FromArgb(100, 116, 139);
        Border = IsDark ? Color.FromArgb(62, 76, 94) : Color.FromArgb(218, 226, 235);
        Code = Color.FromArgb(24, 34, 49);
        CodeText = Color.FromArgb(226, 232, 240);
    }

    public bool IsDark { get; }
    public Color Accent { get; }
    public Color Canvas { get; }
    public Color Surface { get; }
    public Color SubtleSurface { get; }
    public Color Ink { get; }
    public Color Muted { get; }
    public Color Border { get; }
    public Color Code { get; }
    public Color CodeText { get; }
    public Color AccentText => ContrastText(Accent);

    public static ThemePalette From(AppSettings settings) =>
        new(settings.Theme, ColorTranslator.FromHtml(settings.AccentColor));

    public static Color ContrastText(Color color)
    {
        var luminance = (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255;
        return luminance > 0.58 ? Color.FromArgb(23, 33, 48) : Color.White;
    }
}

internal static class ThemeStyler
{
    public static void Apply(Control root, AppSettings settings)
    {
        var palette = ThemePalette.From(settings);
        ApplyControl(root, palette, isRoot: true);
    }

    private static void ApplyControl(Control control, ThemePalette palette, bool isRoot = false)
    {
        if (isRoot && control is Form)
        {
            control.BackColor = palette.Canvas;
            control.ForeColor = palette.Ink;
        }
        else
        {
            switch (control)
            {
                case TabControl tabs:
                    tabs.Tag = palette;
                    tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                    tabs.ItemSize = new Size(140, 32);
                    if (tabs.AccessibleDescription != "pvz-theme-tabs")
                    {
                        tabs.AccessibleDescription = "pvz-theme-tabs";
                        tabs.DrawItem += DrawTab;
                    }
                    control.BackColor = palette.Canvas;
                    control.ForeColor = palette.Ink;
                    tabs.Invalidate();
                    break;
                case TabPage:
                    control.BackColor = palette.Surface;
                    control.ForeColor = palette.Ink;
                    break;
                case TextBox textBox:
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    textBox.BackColor = textBox.Name is "codeEditor" or "outputBox"
                        ? palette.Code
                        : palette.Surface;
                    textBox.ForeColor = textBox.Name is "codeEditor" or "outputBox"
                        ? palette.CodeText
                        : palette.Ink;
                    break;
                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    var buttonRole = button.Tag as string;
                    button.FlatAppearance.BorderColor = buttonRole == "primary"
                        ? palette.Accent
                        : palette.Border;
                    button.BackColor = buttonRole switch
                    {
                        "primary" => palette.Accent,
                        "nav" => palette.IsDark ? Color.FromArgb(16, 22, 32) : Color.FromArgb(30, 41, 59),
                        "selected-nav" => palette.Accent,
                        _ => palette.SubtleSurface,
                    };
                    button.ForeColor = buttonRole switch
                    {
                        "primary" or "selected-nav" => palette.AccentText,
                        "nav" => Color.White,
                        _ => palette.Ink,
                    };
                    button.Cursor = Cursors.Hand;
                    break;
                case Label label:
                    if (label.Tag as string == "muted" || label.Name == "headerSubtitle")
                        label.ForeColor = palette.Muted;
                    else if (label.Name == "headerEyebrow")
                        label.ForeColor = palette.Accent;
                    else
                        label.ForeColor = palette.Ink;
                    if (label.Tag as string == "heading" && !label.Font.Bold)
                        label.Font = new Font(label.Font, label.Font.Style | FontStyle.Bold);
                    break;
                case CheckBox:
                    control.ForeColor = palette.Ink;
                    break;
                case GroupBox group:
                    group.BackColor = palette.Surface;
                    group.ForeColor = palette.Ink;
                    break;
                case ListView list:
                    list.BackColor = palette.Surface;
                    list.ForeColor = palette.Ink;
                    list.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case CheckedListBox list:
                    list.BackColor = palette.Surface;
                    list.ForeColor = palette.Ink;
                    list.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case Panel:
                    var panelRole = control.Tag as string ?? control.AccessibleDescription;
                    control.BackColor = panelRole switch
                    {
                        "accent" => palette.Accent,
                        "nav" => palette.IsDark ? Color.FromArgb(16, 22, 32) : Color.FromArgb(30, 41, 59),
                        "surface" or "card" => palette.Surface,
                        "subtle" => palette.SubtleSurface,
                        _ => palette.Canvas,
                    };
                    break;
                case TrackBar:
                    control.BackColor = palette.Surface;
                    break;
                default:
                    control.BackColor = palette.Surface;
                    control.ForeColor = palette.Ink;
                    break;
            }
        }

        foreach (Control child in control.Controls)
            ApplyControl(child, palette);
    }

    private static void DrawTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs || e.Index < 0 || e.Index >= tabs.TabPages.Count ||
            tabs.Tag is not ThemePalette palette)
            return;

        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var background = selected ? palette.Accent : palette.SubtleSurface;
        var foreground = selected ? palette.AccentText : palette.Muted;
        using var brush = new SolidBrush(background);
        e.Graphics.FillRectangle(brush, e.Bounds);
        using var border = new Pen(palette.Border);
        e.Graphics.DrawRectangle(border, e.Bounds);
        TextRenderer.DrawText(
            e.Graphics,
            tabs.TabPages[e.Index].Text,
            tabs.Font,
            e.Bounds,
            foreground,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class SettingsDialog : Form
{
    private readonly ComboBox _theme = new();
    private readonly Button _accent = new();
    private readonly CheckBox _musicEnabled = new();
    private readonly TextBox _musicPath = new();
    private readonly TrackBar _volume = new();
    private readonly Label _volumeValue = new();
    private readonly Label _musicStatus = new();
    public AppSettings ResultSettings { get; private set; }

    public SettingsDialog(AppSettings settings)
    {
        ResultSettings = Clone(settings);
        Text = "Настройки PVZ";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(600, 535);
        Font = new Font("Segoe UI", 10F);

        var title = new Label
        {
            Text = "Настройки оформления и звука",
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(20, 14, 0, 0),
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            Name = "settingsTitle",
        };

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 8, 20, 8),
            ColumnCount = 2,
            RowCount = 7,
            AutoSize = false,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        _theme.DropDownStyle = ComboBoxStyle.DropDownList;
        _theme.Items.AddRange(["Светлая", "Тёмная"]);
        _theme.SelectedIndex = settings.Theme == AppTheme.Dark ? 1 : 0;
        _theme.Dock = DockStyle.Fill;

        _accent.Text = $"Выбрать цвет  {settings.AccentColor}";
        _accent.Dock = DockStyle.Left;
        _accent.Width = 220;
        _accent.Tag = "accent";
        _accent.Click += ChooseAccentColor;

        _musicEnabled.Text = "Включить фоновую музыку";
        _musicEnabled.AutoSize = true;
        _musicEnabled.CheckedChanged += (_, _) =>
        {
            if (!_musicEnabled.Checked)
                BackgroundMusicPlayer.Stop();
        };
        _musicPath.ReadOnly = true;
        _musicPath.Width = 220;
        _musicPath.Text = settings.MusicFilePath;

        var musicPathRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = new Padding(0),
        };
        musicPathRow.Controls.Add(_musicPath);
        var browse = new Button { Text = "Обзор...", AutoSize = true };
        browse.Click += BrowseForMusic;
        musicPathRow.Controls.Add(browse);

        _volume.Minimum = 0;
        _volume.Maximum = 100;
        _volume.TickFrequency = 10;
        _volume.Value = Math.Clamp(settings.MusicVolume, 0, 100);
        _volume.Dock = DockStyle.Fill;
        _volume.ValueChanged += (_, _) =>
        {
            _volumeValue.Text = $"{_volume.Value}%";
            if (!BackgroundMusicPlayer.TrySetVolume(_volume.Value, out var error))
            {
                _musicStatus.Text = error;
                _musicStatus.ForeColor = Color.Firebrick;
            }
        };
        _volumeValue.Text = $"{_volume.Value}%";
        _volumeValue.TextAlign = ContentAlignment.MiddleRight;
        _volumeValue.Dock = DockStyle.Fill;

        var volumeRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        volumeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 82));
        volumeRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        volumeRow.Controls.Add(_volume, 0, 0);
        volumeRow.Controls.Add(_volumeValue, 1, 0);

        var audioActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = new Padding(0),
        };
        var preview = new Button { Text = "Прослушать", AutoSize = true };
        preview.Click += (_, _) => PreviewMusic();
        audioActions.Controls.Add(preview);
        var stop = new Button { Text = "Остановить", AutoSize = true };
        stop.Click += (_, _) =>
        {
            BackgroundMusicPlayer.Stop();
            _musicEnabled.Checked = false;
            _musicStatus.Text = "Воспроизведение остановлено.";
            _musicStatus.ForeColor = ThemePalette.From(settings).Muted;
        };
        audioActions.Controls.Add(stop);
        _musicStatus.Text = "MP3 по умолчанию: " + SettingsStore.DefaultMusicPath;
        _musicStatus.AutoSize = true;
        _musicStatus.MaximumSize = new Size(520, 0);
        _musicStatus.Tag = "muted";

        AddRow(content, 0, "Тема", _theme);
        AddRow(content, 1, "Акцентный цвет", _accent);
        AddRow(content, 2, "Звук", _musicEnabled);
        AddRow(content, 3, "MP3-файл", musicPathRow);
        AddRow(content, 4, "Громкость", volumeRow);
        AddRow(content, 5, "Фоновая музыка", audioActions);
        content.Controls.Add(_musicStatus, 0, 6);
        content.SetColumnSpan(_musicStatus, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 8, 16, 8),
            WrapContents = false,
        };
        var save = new Button { Text = "Сохранить", AutoSize = true, Tag = "primary" };
        save.Click += SaveSettings;
        var cancel = new Button { Text = "Отмена", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);

        Controls.Add(content);
        Controls.Add(buttons);
        Controls.Add(title);
        AcceptButton = save;
        CancelButton = cancel;
        ThemeStyler.Apply(this, settings);
        _accent.BackColor = ColorTranslator.FromHtml(settings.AccentColor);
        _accent.ForeColor = ThemePalette.ContrastText(_accent.BackColor);
        _accent.FlatAppearance.BorderColor = _accent.BackColor;
        _musicEnabled.Checked = settings.MusicEnabled;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        if (DialogResult != DialogResult.OK)
            BackgroundMusicPlayer.Stop();
        base.OnFormClosed(e);
    }

    private static void AddRow(TableLayoutPanel layout, int row, string caption, Control editor)
    {
        var label = new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Tag = "muted",
        };
        editor.Margin = new Padding(0, 5, 0, 5);
        layout.Controls.Add(label, 0, row);
        layout.Controls.Add(editor, 1, row);
    }

    private void ChooseAccentColor(object? sender, EventArgs e)
    {
        using var dialog = new ColorDialog
        {
            FullOpen = true,
            Color = _accent.BackColor,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _accent.BackColor = dialog.Color;
        _accent.ForeColor = ThemePalette.ContrastText(dialog.Color);
        _accent.FlatAppearance.BorderColor = dialog.Color;
        _accent.Text = $"Выбрать цвет  {ColorTranslator.ToHtml(dialog.Color)}";
    }

    private void BrowseForMusic(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Выберите фоновую музыку",
            Filter = "Аудиофайлы MP3 (*.mp3)|*.mp3",
            CheckFileExists = true,
            Multiselect = false,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _musicPath.Text = dialog.FileName;
            _musicStatus.Text = "Выбранный файл: " + dialog.FileName;
            _musicStatus.ForeColor = ThemePalette.From(ResultSettings).Muted;
        }
    }

    private void PreviewMusic()
    {
        if (!BackgroundMusicPlayer.TryPlay(_musicPath.Text, _volume.Value, out var error))
        {
            _musicStatus.Text = error;
            _musicStatus.ForeColor = Color.Firebrick;
            return;
        }

        _musicStatus.Text = "Музыка воспроизводится.";
        _musicStatus.ForeColor = Color.ForestGreen;
    }

    private void SaveSettings(object? sender, EventArgs e)
    {
        ResultSettings.Theme = _theme.SelectedIndex == 1 ? AppTheme.Dark : AppTheme.Light;
        ResultSettings.AccentColor = ColorTranslator.ToHtml(_accent.BackColor);
        ResultSettings.MusicFilePath = _musicPath.Text;
        ResultSettings.MusicEnabled = _musicEnabled.Checked;
        ResultSettings.MusicVolume = _volume.Value;

        try
        {
            SettingsStore.Save(ResultSettings);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(
                this,
                $"Не удалось сохранить настройки.\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static AppSettings Clone(AppSettings settings) => new()
    {
        Theme = settings.Theme,
        AccentColor = settings.AccentColor,
        MusicFilePath = settings.MusicFilePath,
        MusicEnabled = settings.MusicEnabled,
        MusicVolume = settings.MusicVolume,
    };
}
