using PVZ.Core;

namespace PVZ;

internal static class BuilderSettingsDialogs
{
    public static bool TryPickDeliveryMode(IWin32Window owner, out bool online)
    {
        using var dlg = new Form
        {
            Text = "Способ передачи задачника",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(410, 205),
        };

        var title = new Label
        {
            Text = "Как подготовить задачник?",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 16),
        };
        var offline = new RadioButton
        {
            Text = "Оффлайн — передать файл .pvz ученику",
            AutoSize = true,
            Checked = true,
            Location = new Point(20, 58),
        };
        var onlineChoice = new RadioButton
        {
            Text = "Онлайн — подготовить файл .pvz для загрузки",
            AutoSize = true,
            Location = new Point(20, 90),
        };
        var note = new Label
        {
            Text = "Онлайн-сервер и выдача токенов пока не подключены.\nФайл упакован и проверяется на целостность, но не зашифрован.",
            AutoSize = false,
            Location = new Point(20, 120),
            Size = new Size(370, 38),
            ForeColor = Color.FromArgb(100, 116, 139),
        };
        var ok = new Button { Text = "Продолжить", DialogResult = DialogResult.OK, Location = new Point(220, 165), Width = 90 };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Location = new Point(316, 165), Width = 74 };
        dlg.Controls.AddRange([title, offline, onlineChoice, note, ok, cancel]);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;

        online = false;
        if (dlg.ShowDialog(owner) != DialogResult.OK)
            return false;

        online = onlineChoice.Checked;
        return true;
    }

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

    public static bool TryPickColorScheme(
        IWin32Window owner,
        BuilderThemeMode currentMode,
        Color currentAccent,
        out BuilderThemeMode mode,
        out Color accent)
    {
        using var dlg = new Form
        {
            Text = "Цветовая схема конструктора",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(386, 190),
        };

        var label = new Label
        {
            Text = "Выберите оформление:",
            AutoSize = false,
            Location = new Point(12, 12),
            Size = new Size(350, 24),
        };
        var light = new RadioButton
        {
            Text = "Светлая",
            Location = new Point(16, 42),
            AutoSize = true,
            Checked = currentMode == BuilderThemeMode.Light,
        };
        var dark = new RadioButton
        {
            Text = "Тёмная",
            Location = new Point(120, 42),
            AutoSize = true,
            Checked = currentMode == BuilderThemeMode.Dark,
        };
        var custom = new RadioButton
        {
            Text = "Пользовательская",
            Location = new Point(220, 42),
            AutoSize = true,
            Checked = currentMode == BuilderThemeMode.Custom,
        };
        var accentPreview = new Panel
        {
            Location = new Point(16, 82),
            Size = new Size(28, 24),
            BackColor = currentAccent,
            BorderStyle = BorderStyle.FixedSingle,
        };
        var accentLabel = new Label
        {
            Text = "Цвет акцента",
            AutoSize = true,
            Location = new Point(52, 86),
        };
        var accentHint = new Label
        {
            Text = "Только для кнопок и активной задачи",
            AutoSize = true,
            Location = new Point(52, 106),
        };
        var chooseAccent = new Button
        {
            Text = "Выбрать цвет…",
            Location = new Point(240, 78),
            Size = new Size(130, 32),
        };
        chooseAccent.Click += (_, _) =>
        {
            using var colorDialog = new ColorDialog
            {
                Color = accentPreview.BackColor,
                FullOpen = true,
            };
            if (colorDialog.ShowDialog(dlg) != DialogResult.OK)
                return;

            accentPreview.BackColor = colorDialog.Color;
            custom.Checked = true;
        };

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(208, 144), Width = 75 };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Location = new Point(289, 144), Width = 81 };
        dlg.Controls.AddRange([label, light, dark, custom, accentPreview, accentLabel, accentHint, chooseAccent, ok, cancel]);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;

        if (dlg.ShowDialog(owner) != DialogResult.OK)
        {
            mode = currentMode;
            accent = currentAccent;
            return false;
        }

        mode = light.Checked
            ? BuilderThemeMode.Light
            : custom.Checked
                ? BuilderThemeMode.Custom
                : BuilderThemeMode.Dark;
        accent = accentPreview.BackColor;
        return true;
    }
}
