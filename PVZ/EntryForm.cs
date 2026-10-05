using PVZ.Core;
using PVZ.Player;

namespace PVZ;

internal sealed class EntryForm : Form
{
    private static readonly Color Canvas = Color.FromArgb(17, 24, 39);
    private static readonly Color RaisedSurface = Color.FromArgb(38, 50, 68);
    private static readonly Color Accent = Color.FromArgb(37, 99, 235);

    public EntryForm()
    {
        PvzFormIcon.TryApply(this);
        Text = "";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(420, 280);
        ClientSize = new Size(560, 360);
        BackColor = Canvas;
        ForeColor = Color.FromArgb(226, 232, 240);

        var welcome = new Label
        {
            Text = "Добро пожаловать!",
            AutoSize = false,
            Size = new Size(468, 42),
            Location = new Point(46, 36),
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = Color.FromArgb(226, 232, 240),
        };
        var subtitle = new Label
        {
            Text = "Пункт Выдачи Задачек\r\nВыберите, как вы хотите войти.",
            AutoSize = false,
            Size = new Size(468, 52),
            Location = new Point(46, 82),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
        };

        var teacher = CreateRoleButton("Учитель", PvzFormButtonIcons.Glyphs.Teacher, isPrimary: true);
        teacher.Click += (_, _) => OpenTeacherBuilder();

        var student = CreateRoleButton("Ученик", PvzFormButtonIcons.Glyphs.Student, isPrimary: false);
        student.Click += (_, _) => StartStudentApp();

        teacher.Location = new Point(46, 148);
        student.Location = new Point(46, 230);
        Controls.AddRange([welcome, subtitle, teacher, student]);
    }

    private static Button CreateRoleButton(string title, char iconGlyph, bool isPrimary)
    {
        var button = new Button
        {
            Text = title,
            Size = new Size(468, 64),
            FlatStyle = FlatStyle.Flat,
            BackColor = isPrimary ? Accent : RaisedSurface,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderColor = isPrimary ? Accent : Color.FromArgb(71, 85, 105);
        PvzFormButtonIcons.Apply(button, iconGlyph, Color.White);
        return button;
    }

    private void StartStudentApp()
    {
        Hide();
        try
        {
            using var student = new StudentStartForm();
            student.ShowDialog();
        }
        finally
        {
            Close();
        }
    }

    private void OpenTeacherBuilder()
    {
        Hide();
        try
        {
            using var builder = new Form1();
            builder.ShowDialog();
        }
        finally
        {
            Close();
        }
    }
}
