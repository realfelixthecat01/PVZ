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
        var teacher = CreateRoleButton("Учитель", isPrimary: true);
        teacher.Click += (_, _) => OpenTeacherBuilder();

        var student = CreateRoleButton("Ученик", isPrimary: false);
        student.Click += (_, _) => StartStudentApp();

        teacher.Location = new Point(46, 112);
        student.Location = new Point(46, 194);
        Controls.AddRange([teacher, student]);
    }

    private static Button CreateRoleButton(string title, bool isPrimary)
    {
        var button = new Button
        {
            Text = title,
            Size = new Size(468, 64),
            FlatStyle = FlatStyle.Flat,
            BackColor = isPrimary ? Accent : RaisedSurface,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 0, 0),
            Cursor = Cursors.Hand,
        };
        button.FlatAppearance.BorderColor = isPrimary ? Accent : Color.FromArgb(71, 85, 105);
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
