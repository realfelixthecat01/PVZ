using PVZ.Core;

namespace PVZ;

internal static class BuilderSettingsDialogs
{
    public static bool TryPickAllowedCompilers(IWin32Window owner, ref TaskbookCompilerFlags allowed)
    {
        using var dlg = new Form
        {
            Text = "Разрешённые компиляторы",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(380, 160),
        };

        var info = new Label
        {
            Text = "Отметьте языки, которые ученик может выбрать в задачнике:",
            AutoSize = false,
            Location = new Point(12, 12),
            Size = new Size(350, 36),
        };

        var python = new CheckBox
        {
            Text = "Python",
            Location = new Point(16, 56),
            AutoSize = true,
            Checked = allowed.HasFlag(TaskbookCompilerFlags.Python),
        };
        var pascal = new CheckBox
        {
            Text = "PascalABC.NET",
            Location = new Point(16, 84),
            AutoSize = true,
            Checked = allowed.HasFlag(TaskbookCompilerFlags.PascalAbc),
        };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(206, 118), Width = 75 };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Location = new Point(287, 118), Width = 75 };
        dlg.Controls.AddRange([info, python, pascal, ok, cancel]);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;

        if (dlg.ShowDialog(owner) != DialogResult.OK)
            return false;

        var next = TaskbookCompilerFlags.None;
        if (python.Checked)
            next |= TaskbookCompilerFlags.Python;
        if (pascal.Checked)
            next |= TaskbookCompilerFlags.PascalAbc;

        if (next == TaskbookCompilerFlags.None)
        {
            MessageBox.Show(owner, "Нужно выбрать хотя бы один компилятор.", dlg.Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        allowed = next;
        return true;
    }

    public static bool TryPickColorScheme(IWin32Window owner, ref bool isDark)
    {
        using var dlg = new Form
        {
            Text = "Цветовая схема конструктора",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(340, 130),
        };

        var label = new Label
        {
            Text = "Оформление только для программы-конструктора:",
            AutoSize = false,
            Location = new Point(12, 12),
            Size = new Size(310, 32),
        };
        var light = new RadioButton
        {
            Text = "Светлая",
            Location = new Point(16, 48),
            AutoSize = true,
            Checked = !isDark,
        };
        var dark = new RadioButton
        {
            Text = "Тёмная",
            Location = new Point(120, 48),
            AutoSize = true,
            Checked = isDark,
        };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(166, 88), Width = 75 };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Location = new Point(247, 88), Width = 75 };
        dlg.Controls.AddRange([label, light, dark, ok, cancel]);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;

        if (dlg.ShowDialog(owner) != DialogResult.OK)
            return false;

        isDark = dark.Checked;
        return true;
    }
}
