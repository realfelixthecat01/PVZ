using PVZ.Core;
using PVZ.Player;

namespace PVZ;

internal static class BuilderSettingsDialogs
{
    public static bool TryPickDeliveryMode(IWin32Window owner, out bool online, out StoragePlan plan)
    {
        using var dlg = new Form
        {
            Text = "Способ передачи задачника",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(470, 388),
        };
        PvzFormIcon.TryApply(dlg);
        PvzTheme.ApplyWindow(dlg);
        PvzTheme.EnableAutoScale(dlg);

        var title = new Label
        {
            Text = "Как подготовить задачник?",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = PvzTheme.Ink,
            AutoSize = true,
            Location = new Point(18, 16),
        };
        var offline = new RadioButton
        {
            Text = "Оффлайн — передать файл .pvz ученику",
            AutoSize = true,
            Checked = true,
            ForeColor = PvzTheme.Ink,
            Location = new Point(22, 56),
        };
        var onlineChoice = new RadioButton
        {
            Text = "Онлайн — загрузить в облако и получить токен",
            AutoSize = true,
            ForeColor = PvzTheme.Ink,
            Location = new Point(22, 86),
        };

        var planCard = PvzTheme.Card();
        planCard.SetBounds(22, 122, 426, 148);
        var planTitle = new Label
        {
            Text = "Срок хранения в облаке",
            AutoSize = true,
            Font = PvzTheme.BodyBold,
            ForeColor = PvzTheme.Ink,
            Location = new Point(14, 8),
        };
        var freePlan = new RadioButton
        {
            Text = "Бесплатный — 2 недели",
            AutoSize = true,
            Checked = true,
            ForeColor = PvzTheme.Ink,
            Location = new Point(14, 34),
        };
        var paidPlan = new RadioButton
        {
            Text = "Продлённый — 1 месяц",
            AutoSize = true,
            ForeColor = PvzTheme.Muted,
            Location = new Point(14, 62),
        };
        var planNote = new Label
        {
            Text = "«Продлённый» хранит задачник 30 дней. Подписки и ключи активации —\nв личном кабинете учителя на сайте сервера.",
            AutoSize = false,
            ForeColor = PvzTheme.Subtle,
            Font = PvzTheme.Caption,
            Bounds = new Rectangle(14, 88, 396, 44),
        };
        planCard.Controls.AddRange([planTitle, freePlan, paidPlan, planNote]);

        var serverLabel = TaskbookOnlineClient.ResolveServerUrl().TrimEnd('/');
        var note = new Label
        {
            Text = $"Задачник загрузится на сервер PVZS ({serverLabel}): ученики\nподключатся по 6-символьному токену, а вы увидите таблицу результатов.",
            AutoSize = false,
            Location = new Point(22, 280),
            Size = new Size(426, 46),
            ForeColor = PvzTheme.Subtle,
            Font = PvzTheme.Caption,
        };

        var ok = PvzTheme.PrimaryButton("Продолжить");
        ok.DialogResult = DialogResult.OK;
        ok.SetBounds(268, 338, 110, 38);
        var cancel = PvzTheme.GhostButton("Отмена");
        cancel.DialogResult = DialogResult.Cancel;
        cancel.SetBounds(384, 338, 66, 38);
        dlg.Controls.AddRange([title, offline, onlineChoice, planCard, note, ok, cancel]);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;

        online = false;
        plan = StoragePlan.FreeTwoWeeks;
        if (dlg.ShowDialog(owner) != DialogResult.OK)
            return false;

        online = onlineChoice.Checked;
        plan = paidPlan.Checked ? StoragePlan.PaidOneMonth : StoragePlan.FreeTwoWeeks;
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
            ClientSize = new Size(400, 178),
        };
        PvzFormIcon.TryApply(dlg);
        PvzTheme.ApplyWindow(dlg);
        PvzTheme.EnableAutoScale(dlg);

        var info = new Label
        {
            Text = "Отметьте языки, которые ученик может выбрать в задачнике:",
            AutoSize = false,
            ForeColor = PvzTheme.Muted,
            Location = new Point(16, 14),
            Size = new Size(368, 36),
        };

        var python = new CheckBox
        {
            Text = "Python",
            Location = new Point(20, 60),
            AutoSize = true,
            Checked = allowed.HasFlag(TaskbookCompilerFlags.Python),
            ForeColor = PvzTheme.Ink,
        };
        var pascal = new CheckBox
        {
            Text = "PascalABC.NET",
            Location = new Point(20, 92),
            AutoSize = true,
            Checked = allowed.HasFlag(TaskbookCompilerFlags.PascalAbc),
            ForeColor = PvzTheme.Ink,
        };

        var ok = PvzTheme.PrimaryButton("OK");
        ok.DialogResult = DialogResult.OK;
        ok.SetBounds(206, 130, 84, 36);
        var cancel = PvzTheme.GhostButton("Отмена");
        cancel.DialogResult = DialogResult.Cancel;
        cancel.SetBounds(296, 130, 88, 36);
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
            ClientSize = new Size(420, 220),
        };
        PvzFormIcon.TryApply(dlg);
        PvzTheme.ApplyWindow(dlg);
        PvzTheme.EnableAutoScale(dlg);

        var label = new Label
        {
            Text = "Выберите оформление:",
            AutoSize = false,
            ForeColor = PvzTheme.Muted,
            Location = new Point(16, 14),
            Size = new Size(380, 24),
        };
        var light = new RadioButton
        {
            Text = "Светлая",
            Location = new Point(20, 46),
            AutoSize = true,
            Checked = currentMode == BuilderThemeMode.Light,
            ForeColor = PvzTheme.Ink,
        };
        var dark = new RadioButton
        {
            Text = "Тёмная",
            Location = new Point(130, 46),
            AutoSize = true,
            Checked = currentMode == BuilderThemeMode.Dark,
            ForeColor = PvzTheme.Ink,
        };
        var custom = new RadioButton
        {
            Text = "Пользовательская",
            Location = new Point(236, 46),
            AutoSize = true,
            Checked = currentMode == BuilderThemeMode.Custom,
            ForeColor = PvzTheme.Ink,
        };
        var accentPreview = new Panel
        {
            Location = new Point(20, 92),
            Size = new Size(30, 28),
            BackColor = currentAccent,
            BorderStyle = BorderStyle.FixedSingle,
        };
        var accentLabel = new Label
        {
            Text = "Цвет акцента",
            AutoSize = true,
            ForeColor = PvzTheme.Ink,
            Location = new Point(60, 96),
        };
        var accentHint = new Label
        {
            Text = "Только для кнопок и активной задачи",
            AutoSize = true,
            ForeColor = PvzTheme.Subtle,
            Font = PvzTheme.Caption,
            Location = new Point(60, 116),
        };
        var chooseAccent = PvzTheme.SecondaryButton("Выбрать цвет…");
        chooseAccent.SetBounds(268, 88, 136, 36);
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

        var ok = PvzTheme.PrimaryButton("OK");
        ok.DialogResult = DialogResult.OK;
        ok.SetBounds(242, 172, 84, 36);
        var cancel = PvzTheme.GhostButton("Отмена");
        cancel.DialogResult = DialogResult.Cancel;
        cancel.SetBounds(332, 172, 74, 36);
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
