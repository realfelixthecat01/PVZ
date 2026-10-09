using PVZ.Core;
using PVZ.Player;

namespace PVZ;

internal sealed class ServerAddressDialog : Form
{
    public ServerAddressDialog()
    {
        PvzFormIcon.TryApply(this);
        Text = "Адрес сервера";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(460, 248);
        PvzTheme.ApplyWindow(this);
        PvzTheme.EnableAutoScale(this);

        var title = new Label
        {
            Text = "Адрес сервера PVZS",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 12.5F, FontStyle.Bold),
            ForeColor = PvzTheme.Ink,
            Location = new Point(20, 18),
        };
        var currentAddress = TaskbookOnlineClient.TryResolveServerUrl();
        var current = PvzTheme.CaptionLabel($"Сейчас: {currentAddress ?? "ссылка ещё не задана"}");
        current.Location = new Point(22, 48);
        var hint = new Label
        {
            Text = "Вставьте ссылку из программы «Сервер и ссылка» —\nона доступна из любой сети и нужна для входа.",
            AutoSize = false,
            Size = new Size(420, 62),
            Location = new Point(22, 74),
            ForeColor = PvzTheme.Muted,
            Font = PvzTheme.Caption,
        };
        var box = PvzTheme.InputBox("https://…");
        box.SetBounds(22, 146, 416, 34);
        box.Text = ServerAddressStore.Load() ?? string.Empty;

        var ok = PvzTheme.PrimaryButton("Сохранить");
        ok.SetBounds(268, 196, 108, 38);
        var cancel = PvzTheme.GhostButton("Отмена");
        cancel.SetBounds(384, 196, 66, 38);

        ok.Click += (_, _) =>
        {
            var raw = box.Text.Trim();
            if (TaskbookOnlineClient.RequireExplicitServerUrl && raw.Length == 0)
            {
                MessageBox.Show(
                    this,
                    "Введите ссылку на сервер.",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            if (raw.Length == 0 || raw == TaskbookOnlineClient.DefaultServerUrl)
            {
                ServerAddressStore.Save(null);
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            try
            {
                ServerAddressStore.Save(TaskbookOnlineClient.NormalizeServerUrl(raw));
            }
            catch (InvalidOperationException exception)
            {
                MessageBox.Show(this, exception.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.AddRange([title, current, hint, box, ok, cancel]);
        AcceptButton = ok;
        CancelButton = cancel;
    }
}
