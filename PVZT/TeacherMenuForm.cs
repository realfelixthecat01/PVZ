using System.Diagnostics;
using PVZ.Core;
using PVZ.Player;

namespace PVZ;

internal sealed class TeacherMenuForm : Form
{
    private readonly Label _accountLabel;
    private readonly Button _accountButton;
    private bool _accountBusy;

    public TeacherMenuForm()
    {
        PvzFormIcon.TryApply(this);
        Text = "Пункт Выдачи Задачек — учитель";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(600, 520);
        ClientSize = new Size(660, 600);
        PvzTheme.ApplyWindow(this);
        PvzTheme.EnableAutoScale(this);
        PvzTheme.FitToScreen(this);
        AutoScroll = true;

        var title = new Label
        {
            Text = "Кабинет учителя",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = PvzTheme.Ink,
            Bounds = new Rectangle(40, 42, 580, 44),
        };
        var subtitle = new Label
        {
            Text = "Создайте задачник, скачайте готовый с сервера\nили следите за прогрессом учеников вживую.",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = PvzTheme.Muted,
            Bounds = new Rectangle(40, 92, 580, 48),
        };

        var create = CreateMenuButton("1. Создать задачник", primary: true, PvzFormButtonIcons.Glyphs.Edit);
        create.SetBounds(90, 168, 480, 58);
        create.Click += (_, _) =>
        {
            using var builder = new Form1();
            builder.ShowDialog(this);
        };

        var download = CreateMenuButton("2. Скачать готовый задачник", primary: false, PvzFormButtonIcons.Glyphs.Download);
        download.SetBounds(90, 240, 480, 58);
        download.Click += (_, _) =>
        {
            using var catalog = new TeacherCatalogForm();
            catalog.ShowDialog(this);
        };

        var observe = CreateMenuButton("3. Смотреть прогресс активных сессий", primary: false, PvzFormButtonIcons.Glyphs.Refresh);
        observe.SetBounds(90, 312, 480, 58);
        observe.Click += (_, _) =>
        {
            using var monitor = new TeacherSessionMonitorForm();
            monitor.ShowDialog(this);
        };

        var web = CreateMenuButton("4. Открыть веб-панель в браузере", primary: false, PvzFormButtonIcons.Glyphs.Globe);
        web.SetBounds(90, 384, 480, 58);
        web.Click += async (_, _) => await OpenWebCabinetAsync(web);

        _accountLabel = new Label
        {
            Text = "Учётная запись: вход не выполнен",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = PvzTheme.Muted,
            Bounds = new Rectangle(90, 462, 336, 44),
        };
        _accountButton = PvzTheme.SecondaryButton("Войти");
        _accountButton.SetBounds(440, 462, 130, 44);
        _accountButton.Click += async (_, _) =>
            await PromptForAccountAsync(switchAccount: TeacherServerAuth.IsSignedIn);

        var footer = new Label
        {
            Text = "Онлайн-режим использует PVZ Cloud: токен для учеников, хранение по тарифу, автоудаление.",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = PvzTheme.Subtle,
            Font = PvzTheme.Caption,
            Bounds = new Rectangle(40, 520, 580, 40),
        };

        Controls.AddRange([title, subtitle, create, download, observe, web, _accountLabel, _accountButton, footer]);

        Shown += async (_, _) => await PromptForAccountAsync();
    }

    private async Task PromptForAccountAsync(bool switchAccount = false)
    {
        if (_accountBusy || IsDisposed)
            return;

        _accountBusy = true;
        _accountButton.Enabled = false;
        _accountLabel.Text = "Учётная запись: подключение к серверу…";
        try
        {
            if (switchAccount)
                TeacherServerAuth.Invalidate();

            var serverUrl = await TaskbookOnlineClient.ResolveServerUrlAsync();
            var token = await TeacherServerAuth.EnsureAccessTokenAsync(this, serverUrl);
            _accountLabel.Text = $"Учётная запись: {DescribeProfile(token is null ? null : TeacherServerAuth.Profile)}";
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException)
        {
            _accountLabel.Text = "Учётная запись: сервер недоступен";
            if (!IsDisposed)
                MessageBox.Show(this, exception.Message, "Вход на сервер", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _accountBusy = false;
            if (!IsDisposed)
            {
                _accountButton.Enabled = true;
                _accountButton.Text = TeacherServerAuth.IsSignedIn ? "Сменить" : "Войти";
            }
        }
    }

    private static string DescribeProfile(AccountProfileDto? profile)
    {
        if (profile is null)
            return "вход не выполнен";
        var name = string.IsNullOrWhiteSpace(profile.FullName) ? profile.Username : profile.FullName;
        return string.IsNullOrWhiteSpace(name) || name == profile.Username
            ? profile.Username
            : $"{name} ({profile.Username})";
    }

    private async Task OpenWebCabinetAsync(Button button)
    {
        button.Enabled = false;
        try
        {
            var serverUrl = await TaskbookOnlineClient.ResolveServerUrlAsync();
            Process.Start(new ProcessStartInfo(new Uri(new Uri(serverUrl), "admin").ToString())
            {
                UseShellExecute = true,
            });
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or TaskCanceledException)
        {
            MessageBox.Show(this, exception.Message, "Веб-панель", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!IsDisposed)
                button.Enabled = true;
        }
    }

    private static Button CreateMenuButton(string text, bool primary, char iconGlyph)
    {
        var button = primary
            ? PvzTheme.PrimaryButton(text)
            : PvzTheme.SecondaryButton(text);
        button.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        button.TextAlign = ContentAlignment.MiddleLeft;
        PvzFormButtonIcons.Apply(button, iconGlyph, Color.White);
        return button;
    }
}
