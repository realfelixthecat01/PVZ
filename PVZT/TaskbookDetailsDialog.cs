using PVZ.Core;
using PVZ.Player;

namespace PVZ;

internal sealed class TaskbookDetailsDialog : Form
{
    private readonly ComboBox _subject = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 240,
    };
    private readonly ComboBox _classNumber = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 100,
    };

    private TaskbookDetailsDialog(string subject, int classNumber)
    {
        PvzFormIcon.TryApply(this);
        Text = "Предмет и класс";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 220);
        PvzTheme.ApplyWindow(this);
        PvzTheme.EnableAutoScale(this);

        var title = new Label
        {
            Text = "Выберите предмет и класс задачника",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold),
            ForeColor = PvzTheme.Ink,
            Location = new Point(20, 20),
        };
        var subjectLabel = new Label
        {
            Text = "Предмет",
            AutoSize = true,
            ForeColor = PvzTheme.Muted,
            Location = new Point(22, 72),
        };
        var classLabel = new Label
        {
            Text = "Класс",
            AutoSize = true,
            ForeColor = PvzTheme.Muted,
            Location = new Point(282, 72),
        };

        foreach (var item in TaskbookCatalogSubjects.All)
            _subject.Items.Add(item);
        _subject.DisplayMember = nameof(TaskbookCatalogSubject.DisplayName);
        _subject.Location = new Point(20, 98);
        _subject.BackColor = PvzTheme.RaisedSurface;
        _subject.ForeColor = PvzTheme.Ink;
        var selectedSubject = TaskbookCatalogSubjects.All.FirstOrDefault(item => item.Id == subject)
            ?? TaskbookCatalogSubjects.All[0];
        _classNumber.Location = new Point(280, 98);
        _classNumber.BackColor = PvzTheme.RaisedSurface;
        _classNumber.ForeColor = PvzTheme.Ink;
        _subject.SelectedItem = selectedSubject;
        PopulateClasses(selectedSubject.Id, classNumber);
        _subject.SelectedIndexChanged += (_, _) =>
        {
            if (_subject.SelectedItem is TaskbookCatalogSubject changedSubject)
                PopulateClasses(changedSubject.Id, _classNumber.SelectedItem as int? ?? 6);
        };

        var ok = PvzTheme.PrimaryButton("Продолжить");
        ok.Size = new Size(140, 38);
        ok.Location = new Point(150, 160);
        ok.DialogResult = DialogResult.OK;
        var cancel = PvzTheme.GhostButton("Отмена");
        cancel.Size = new Size(100, 38);
        cancel.Location = new Point(300, 160);
        cancel.DialogResult = DialogResult.Cancel;
        Controls.AddRange([title, subjectLabel, classLabel, _subject, _classNumber, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void PopulateClasses(string subject, int preferredClass)
    {
        var firstClass = TaskbookCatalogSubjects.MinimumClassNumber(subject);
        _classNumber.Items.Clear();
        for (var grade = firstClass; grade <= 11; grade++)
            _classNumber.Items.Add(grade);
        _classNumber.SelectedItem = Math.Clamp(preferredClass, firstClass, 11);
    }

    public static bool TryChoose(
        IWin32Window owner,
        string currentSubject,
        int currentClassNumber,
        out string subject,
        out int classNumber)
    {
        using var dialog = new TaskbookDetailsDialog(currentSubject, currentClassNumber);
        if (dialog.ShowDialog(owner) != DialogResult.OK ||
            dialog._subject.SelectedItem is not TaskbookCatalogSubject selectedSubject ||
            dialog._classNumber.SelectedItem is not int selectedClass)
        {
            subject = "";
            classNumber = 0;
            return false;
        }

        subject = selectedSubject.Id;
        classNumber = selectedClass;
        return true;
    }
}
