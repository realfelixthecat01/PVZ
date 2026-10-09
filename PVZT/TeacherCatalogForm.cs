using PVZ.Core;
using PVZ.Player;

namespace PVZ;

internal sealed class TeacherCatalogForm : Form
{
    private readonly ComboBox _subject = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly ComboBox _classNumber = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly ListView _items = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = false,
        HideSelection = false,
        MultiSelect = false,
        BorderStyle = BorderStyle.None,
        BackColor = PvzTheme.Surface,
        ForeColor = PvzTheme.Ink,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
    };
    private readonly Label _status = new() { AutoSize = true, ForeColor = PvzTheme.Muted };
    private readonly Button _load = PvzTheme.SecondaryButton("Показать задачники");
    private readonly Button _download = PvzTheme.PrimaryButton("Скачать выбранный");
    private string? _serverUrl;

    public TeacherCatalogForm()
    {
        PvzFormIcon.TryApply(this);
        Text = "Готовые задачники";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 500);
        ClientSize = new Size(900, 590);
        PvzTheme.ApplyWindow(this);
        PvzTheme.EnableAutoScale(this);
        PvzTheme.FitToScreen(this);

        BuildLayout();
        _load.Click += async (_, _) => await LoadCatalogAsync();
        _download.Click += async (_, _) => await DownloadSelectedAsync();
        Shown += async (_, _) => await LoadCatalogAsync();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = PvzTheme.Canvas,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(new Label
        {
            Text = "Каталог задачников на сервере",
            AutoSize = true,
            Font = PvzTheme.Title,
            ForeColor = PvzTheme.Ink,
            Margin = new Padding(0, 0, 0, 12),
        }, 0, 0);

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            BackColor = PvzTheme.Surface,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 8),
        };
        foreach (var subject in TaskbookCatalogSubjects.All)
            _subject.Items.Add(subject);
        _subject.DisplayMember = nameof(TaskbookCatalogSubject.DisplayName);
        _subject.SelectedItem = TaskbookCatalogSubjects.All[0];
        PopulateClasses(TaskbookCatalogSubjects.All[0].Id, preferredClass: 9);
        _subject.SelectedIndexChanged += (_, _) =>
        {
            if (_subject.SelectedItem is TaskbookCatalogSubject subject)
                PopulateClasses(subject.Id, _classNumber.SelectedItem as int? ?? 9);
            _items.Items.Clear();
        };
        filters.Controls.Add(new Label { Text = "Предмет:", AutoSize = true, ForeColor = PvzTheme.Ink, Padding = new Padding(0, 10, 0, 0) });
        filters.Controls.Add(_subject);
        filters.Controls.Add(new Label { Text = "Класс:", AutoSize = true, ForeColor = PvzTheme.Ink, Padding = new Padding(8, 10, 0, 0) });
        filters.Controls.Add(_classNumber);

        _load.AutoSize = false;
        _load.Size = new Size(190, 34);
        _load.Margin = new Padding(12, 4, 0, 0);
        filters.Controls.Add(_load);

        _items.Columns.Add("Файл", 570);
        _items.Columns.Add("Размер", 140);

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0),
        };
        _download.AutoSize = false;
        _download.Size = new Size(190, 38);
        footer.Controls.Add(_download);
        _status.Margin = new Padding(12, 10, 0, 0);
        footer.Controls.Add(_status);

        root.Controls.Add(filters, 0, 1);
        root.Controls.Add(_items, 0, 2);
        root.Controls.Add(footer, 0, 3);
        Controls.Add(root);

        foreach (var combo in new[] { _subject, _classNumber })
        {
            combo.BackColor = PvzTheme.RaisedSurface;
            combo.ForeColor = PvzTheme.Ink;
        }
    }

    private void PopulateClasses(string subject, int preferredClass)
    {
        var firstClass = TaskbookCatalogSubjects.MinimumClassNumber(subject);
        _classNumber.Items.Clear();
        for (var grade = firstClass; grade <= 11; grade++)
            _classNumber.Items.Add(grade);
        _classNumber.SelectedItem = Math.Clamp(preferredClass, firstClass, 11);
    }

    private async Task LoadCatalogAsync()
    {
        if (_subject.SelectedItem is not TaskbookCatalogSubject subject ||
            _classNumber.SelectedItem is not int classNumber)
            return;

        SetBusy(true);
        try
        {
            _serverUrl ??= await TaskbookOnlineClient.ResolveServerUrlAsync();
            var taskbooks = await TaskbookOnlineClient.ListCatalogTaskbooksAsync(subject.Id, classNumber, _serverUrl);
            _items.BeginUpdate();
            _items.Items.Clear();
            foreach (var taskbook in taskbooks)
            {
                var item = new ListViewItem(taskbook.FileName) { Tag = taskbook };
                item.SubItems.Add($"{taskbook.SizeBytes / 1024.0:F0} КБ");
                _items.Items.Add(item);
            }
            _items.EndUpdate();
            _status.Text = taskbooks.Count == 0
                ? "В этой папке пока нет .pvz файлов."
                : $"Найдено задачников: {taskbooks.Count}.";
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or InvalidOperationException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or NotSupportedException)
        {
            _status.Text = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task DownloadSelectedAsync()
    {
        if (_items.SelectedItems.Count == 0 ||
            _items.SelectedItems[0].Tag is not TaskbookCatalogItemDto selected ||
            _subject.SelectedItem is not TaskbookCatalogSubject subject ||
            _classNumber.SelectedItem is not int classNumber)
        {
            _status.Text = "Выберите задачник из списка.";
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "Задачник PVZ (*.pvz)|*.pvz",
            DefaultExt = "pvz",
            AddExtension = true,
            FileName = selected.FileName,
            InitialDirectory = TaskbookUserFolders.DownloadsPath,
            Title = "Сохранить готовый задачник",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        SetBusy(true);
        try
        {
            _serverUrl ??= await TaskbookOnlineClient.ResolveServerUrlAsync();
            var accessToken = await TeacherServerAuth.EnsureAccessTokenAsync(this, _serverUrl);
            if (accessToken is null)
            {
                _status.Text = "Вход отменён.";
                return;
            }

            byte[] package;
            try
            {
                package = await TaskbookOnlineClient.DownloadCatalogTaskbookAsync(
                    subject.Id,
                    classNumber,
                    selected.FileName,
                    _serverUrl,
                    accessToken);
            }
            catch (ServerUnauthorizedException)
            {
                TeacherServerAuth.Invalidate();
                accessToken = await TeacherServerAuth.EnsureAccessTokenAsync(this, _serverUrl);
                if (accessToken is null)
                {
                    _status.Text = "Вход отменён.";
                    return;
                }

                package = await TaskbookOnlineClient.DownloadCatalogTaskbookAsync(
                    subject.Id,
                    classNumber,
                    selected.FileName,
                    _serverUrl,
                    accessToken);
            }

            await File.WriteAllBytesAsync(dialog.FileName, package);
            _status.Text = $"Задачник скачан: {dialog.FileName}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or NotSupportedException)
        {
            _status.Text = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _load.Enabled = !busy;
        _download.Enabled = !busy;
        _subject.Enabled = !busy;
        _classNumber.Enabled = !busy;
        UseWaitCursor = busy;
    }
}
