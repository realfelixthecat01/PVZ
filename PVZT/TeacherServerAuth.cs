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

    private sealed class TeacherAccountDialog : Form
    {
        private readonly string _serverUrl;
        private readonly RadioButton _loginMode;
        private readonly RadioButton _registerMode;
        private readonly Panel _loginPanel;
        private readonly Panel _registerPanel;
        private readonly TextBox _identifier;
        private readonly TextBox _loginPassword;
        private readonly TextBox _username;
        private readonly TextBox _fullName;
        private readonly TextBox _school;
        private readonly TextBox _email;
        private readonly TextBox _registerPassword;
        private readonly Label _hint;
        private readonly Button _ok;
        private readonly Button _cancel;
        private bool _busy;

        public AccountAuthReceipt? Receipt { get; private set; }

        public TeacherAccountDialog(string serverUrl)
        {
            _serverUrl = serverUrl;
            PvzFormIcon.TryApply(this);
            Text = "Учётная запись учителя";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            PvzTheme.ApplyWindow(this);
            PvzTheme.EnableAutoScale(this);

            _loginMode = new RadioButton
            {
                Text = "Вход",
                Checked = true,
                AutoSize = true,
                ForeColor = PvzTheme.Ink,
                Location = new Point(18, 16),
            };
            _registerMode = new RadioButton
            {
                Text = "Создать аккаунт учителя",
                AutoSize = true,
                ForeColor = PvzTheme.Ink,
                Location = new Point(90, 16),
            };

            _loginPanel = new Panel { Location = new Point(18, 46), Size = new Size(364, 116) };
            _identifier = AddField(_loginPanel, 0, "Имя аккаунта или email", "masha.ivanova или teacher@school.by");
            _loginPassword = AddField(_loginPanel, 58, "Пароль", "Пароль");
            _loginPassword.UseSystemPasswordChar = true;

            _registerPanel = new Panel { Location = new Point(18, 46), Size = new Size(364, 290), Visible = false };
            _username = AddField(_registerPanel, 0, "Имя аккаунта", "masha.ivanova");
            _fullName = AddField(_registerPanel, 58, "ФИО", "Мария Петрова");
            _school = AddField(_registerPanel, 116, "Школа или гимназия", "Гимназия №1, Минск");
            _email = AddField(_registerPanel, 174, "Email", "teacher@school.by");
            _registerPassword = AddField(_registerPanel, 232, "Пароль", "От 6 символов");
            _registerPassword.UseSystemPasswordChar = true;

            _hint = new Label
            {
                AutoSize = false,
                ForeColor = PvzTheme.Subtle,
                Font = PvzTheme.Caption,
            };
            _ok = PvzTheme.PrimaryButton("Войти");
            _ok.Click += OnAccept;
            _cancel = PvzTheme.GhostButton("Отмена");
            _cancel.DialogResult = DialogResult.Cancel;

            Controls.AddRange([_loginMode, _registerMode, _loginPanel, _registerPanel, _hint, _ok, _cancel]);
            AcceptButton = _ok;
            CancelButton = _cancel;

            _loginMode.CheckedChanged += (_, _) => ApplyMode();
            ApplyMode();
            Shown += (_, _) => FirstField.Focus();
        }

        private bool Registering => _registerMode.Checked;

        private Control FirstField => Registering ? _username : _identifier;

        private static TextBox AddField(Panel panel, int top, string labelText, string placeholder)
        {
            var label = new Label
            {
                Text = labelText,
                AutoSize = true,
                ForeColor = PvzTheme.Muted,
                Font = PvzTheme.Caption,
                Location = new Point(0, top),
            };
            var input = PvzTheme.InputBox(placeholder);
            input.SetBounds(0, top + 20, 364, 32);
            panel.Controls.AddRange([label, input]);
            return input;
        }

        private void ApplyMode()
        {
            var registering = Registering;
            _loginPanel.Visible = !registering;
            _registerPanel.Visible = registering;

            var fieldBottom = 46 + (registering ? _registerPanel.Height : _loginPanel.Height);
            _hint.Text = registering
                ? "Регистрация бесплатна. Имя аккаунта — латинские буквы, цифры, точка, дефис; пароль от 6 символов."
                : "Пароль проверяет сервер — на этом компьютере он не сохраняется.";
            _hint.SetBounds(18, fieldBottom + 8, 364, 30);

            _ok.Text = registering ? "Создать аккаунт" : "Войти";
            var buttonsTop = fieldBottom + 8 + 30 + 12;
            _ok.SetBounds(148, buttonsTop, 130, 38);
            _cancel.SetBounds(288, buttonsTop, 94, 38);
            ClientSize = new Size(400, buttonsTop + 38 + 18);
            FirstField.Focus();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _loginMode.Enabled = !busy;
            _registerMode.Enabled = !busy;
            _loginPanel.Enabled = !busy;
            _registerPanel.Enabled = !busy;
            _ok.Enabled = !busy;
            _ok.Text = busy ? "Подключение…" : Registering ? "Создать аккаунт" : "Войти";
        }

        private async void OnAccept(object? sender, EventArgs e)
        {
            if (_busy)
                return;

            string? missing = null;
            if (Registering)
            {
                if (string.IsNullOrWhiteSpace(_username.Text) ||
                    string.IsNullOrWhiteSpace(_fullName.Text) ||
                    string.IsNullOrWhiteSpace(_school.Text) ||
                    string.IsNullOrWhiteSpace(_email.Text) ||
                    string.IsNullOrEmpty(_registerPassword.Text))
                    missing = "Заполните все поля регистрации.";
            }
            else if (string.IsNullOrWhiteSpace(_identifier.Text) || string.IsNullOrEmpty(_loginPassword.Text))
            {
                missing = "Введите имя аккаунта и пароль.";
            }

            if (missing is not null)
            {
                MessageBox.Show(this, missing, "Вход на сервер", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetBusy(true);
            try
            {
                Receipt = Registering
                    ? await TaskbookOnlineClient.AccountRegisterAsync(
                        _username.Text.Trim(),
                        _fullName.Text.Trim(),
                        _school.Text.Trim(),
                        _email.Text.Trim(),
                        _registerPassword.Text,
                        _serverUrl)
                    : await TaskbookOnlineClient.AccountLoginAsync(
                        _identifier.Text.Trim(),
                        _loginPassword.Text,
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
    }
}
