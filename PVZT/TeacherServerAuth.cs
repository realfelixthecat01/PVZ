// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using PVZ.Core;
using PVZ.Player;

namespace PVZ;

internal static class TeacherServerAuth
{
    private static string? _accessToken;

    public static AccountProfileDto? Profile { get; private set; }

    public static bool IsSignedIn => _accessToken is not null;

    public static void Invalidate()
    {
        _accessToken = null;
        Profile = null;
    }

    public static async Task<string?> EnsureAccessTokenAsync(IWin32Window owner, string serverUrl)
    {
        if (_accessToken is not null)
            return _accessToken;

        using var dialog = new TeacherAccountDialog(serverUrl);
        if (dialog.ShowDialog(owner) != DialogResult.OK || dialog.Receipt is null)
            return null;

        _accessToken = dialog.Receipt.AccessToken;
        Profile = dialog.Receipt.Profile;
        return _accessToken;
    }

    /// <summary>Окно входа: два поля и три кнопки. Имя аккаунта помнить не нужно, вход и регистрация
    /// идут по одним и тем же полям: фамилия с именем и пароль.</summary>
    private sealed class TeacherAccountDialog : Form
    {
        private readonly string _serverUrl;
        private readonly RadioButton _loginMode;
        private readonly RadioButton _registerMode;
        private readonly TextBox _fullName;
        private readonly TextBox _password;
        private readonly Label _hint;
        private readonly Button _ok;
        private readonly Button _guest;
        private readonly Button _cancel;
        private bool _busy;

        public AccountAuthReceipt? Receipt { get; private set; }

        public TeacherAccountDialog(string serverUrl)
        {
            _serverUrl = serverUrl;
            PvzFormIcon.TryApply(this);
            Text = "Вход для учителя";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            PvzTheme.ApplyWindow(this);
            PvzTheme.EnableAutoScale(this);

            _loginMode = new RadioButton
            {
                Text = "У меня уже есть аккаунт",
                Checked = true,
                AutoSize = true,
                ForeColor = PvzTheme.Ink,
                Location = new Point(18, 16),
            };
            _registerMode = new RadioButton
            {
                Text = "Я здесь впервые",
                AutoSize = true,
                ForeColor = PvzTheme.Ink,
                Location = new Point(200, 16),
            };

            _fullName = AddField(46, "Как вас зовут? Имя и фамилия", "Мария Иванова");
            _password = AddField(104, "Придумайте пароль (от 6 символов)", "Пароль");
            _password.UseSystemPasswordChar = true;

            _hint = new Label
            {
                AutoSize = false,
                ForeColor = PvzTheme.Subtle,
                Font = PvzTheme.Caption,
            };
            _ok = PvzTheme.PrimaryButton("Войти");
            _ok.Click += OnAccept;
            _guest = PvzTheme.SecondaryButton("Работать как гость");
            _guest.Click += OnGuest;
            _cancel = PvzTheme.GhostButton("Отмена");
            _cancel.DialogResult = DialogResult.Cancel;

            Controls.AddRange([_loginMode, _registerMode, _fullName, _password, _hint, _ok, _guest, _cancel]);
            AcceptButton = _ok;
            CancelButton = _cancel;

            _loginMode.CheckedChanged += (_, _) => ApplyMode();
            ApplyMode();
            Shown += (_, _) => _fullName.Focus();
        }

        private bool Registering => _registerMode.Checked;

        private TextBox AddField(int top, string labelText, string placeholder)
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                ForeColor = PvzTheme.Muted,
                Font = PvzTheme.Caption,
                Bounds = new Rectangle(18, top, 364, 18),
            };
            var input = PvzTheme.InputBox(placeholder);
            input.SetBounds(18, top + 20, 364, 32);
            Controls.Add(label);
            Controls.Add(input);
            return input;
        }

        private void ApplyMode()
        {
            _hint.Text = Registering
                ? "Регистрация бесплатна и состоит из этих двух полей. Почта не нужна."
                : "Можно войти по имени с фамилией или по почте, если вы её указывали.";
            _hint.SetBounds(18, 158, 364, 30);
            _ok.Text = Registering ? "Создать аккаунт" : "Войти";
            _ok.SetBounds(148, 196, 130, 38);
            _guest.SetBounds(18, 248, 260, 34);
            _cancel.SetBounds(288, 248, 94, 34);
            ClientSize = new Size(400, 300);
            _fullName.Focus();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _loginMode.Enabled = !busy;
            _registerMode.Enabled = !busy;
            _fullName.Enabled = !busy;
            _password.Enabled = !busy;
            _ok.Enabled = !busy;
            _guest.Enabled = !busy;
            _ok.Text = busy ? "Подключение…" : Registering ? "Создать аккаунт" : "Войти";
        }

        private async void OnAccept(object? sender, EventArgs e)
        {
            if (_busy)
                return;
            if (string.IsNullOrWhiteSpace(_fullName.Text) || string.IsNullOrEmpty(_password.Text))
            {
                MessageBox.Show(this, "Введите имя с фамилией и пароль.", "Вход на сервер",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetBusy(true);
            try
            {
                Receipt = Registering
                    ? await TaskbookOnlineClient.AccountRegisterAsync(
                        AccountRoles.Teacher,
                        _fullName.Text.Trim(),
                        _password.Text,
                        className: "",
                        _serverUrl)
                    : await TaskbookOnlineClient.AccountLoginAsync(
                        _fullName.Text.Trim(),
                        _password.Text,
                        _serverUrl);
                DialogResult = DialogResult.OK;
            }
            catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException)
            {
                MessageBox.Show(this, exception.Message, "Вход на сервер", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (!IsDisposed)
                    SetBusy(false);
            }
        }

        private async void OnGuest(object? sender, EventArgs e)
        {
            if (_busy)
                return;

            SetBusy(true);
            try
            {
                var secret = LocalIdentityStore.LoadOrCreateGuestSecret();
                Receipt = await TaskbookOnlineClient.GuestLoginAsync(secret, _serverUrl);
                DialogResult = DialogResult.OK;
            }
            catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException)
            {
                MessageBox.Show(this, exception.Message, "Гостевой режим", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                if (!IsDisposed)
                    SetBusy(false);
            }
        }
    }
}
