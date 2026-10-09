using System.Diagnostics;
using PVZ.Player;

namespace PVZ.Launcher;

internal sealed class LauncherForm : Form
{
    private readonly Label _status;
    private readonly TextBox _linkBox;
    private readonly Button _startStop;
    private readonly TextBox _logBox;
    private readonly ServerRunner _server = new();
    private readonly TunnelRunner _tunnel = new();
    private string _serverStatus = "остановлен";
    private string _tunnelStatus = "остановлен";
    private bool _busy;
    private bool _started;
    private int _logLines;

    public LauncherForm()
    {
        PvzFormIcon.TryApply(this);
        Text = "Пункт Выдачи Задачек — сервер и ссылка";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(620, 560);
        ClientSize = new Size(680, 530);
        PvzTheme.ApplyWindow(this);
        PvzTheme.EnableAutoScale(this);
        PvzTheme.FitToScreen(this);
        AutoScroll = true;

        var title = new Label
        {
            Text = "Сервер и ссылка ПВЗ",
            AutoSize = true,
            Font = PvzTheme.Title,
            ForeColor = PvzTheme.Ink,
            Location = new Point(20, 16),
        };
        var hint = new Label
        {
            Text = "Одна кнопка запускает сервер PVZS и защищённый туннель Cloudflare — ссылка доступна "
                + "из любой сети. Не закрывайте это окно, пока ссылка нужна.",
            AutoSize = false,
            Size = new Size(640, 40),
            Location = new Point(20, 50),
            ForeColor = PvzTheme.Muted,
            Font = PvzTheme.Caption,
        };
        _status = new Label
        {
            AutoSize = true,
            ForeColor = PvzTheme.Ink,
            Font = PvzTheme.BodyBold,
            Location = new Point(20, 96),
        };
        var linkCaption = PvzTheme.CaptionLabel("Ссылка для учеников и учителя:");
        linkCaption.Location = new Point(20, 128);

        _linkBox = PvzTheme.InputBox();
        _linkBox.SetBounds(20, 152, 430, 36);
        _linkBox.ReadOnly = true;

        var copy = PvzTheme.SecondaryButton("Копировать");
        copy.SetBounds(458, 152, 108, 36);
        copy.Click += (_, _) => CopyLink();

        var open = PvzTheme.GhostButton("Открыть");
        open.SetBounds(574, 152, 86, 36);
        open.Click += (_, _) => OpenLink();

        _startStop = PvzTheme.PrimaryButton("Запустить");
        _startStop.SetBounds(20, 210, 200, 48);
        _startStop.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _startStop.Click += async (_, _) =>
        {
            if (_started)
                StopAll(log: true);
            else
                await StartAsync();
        };

        _logBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            BackColor = PvzTheme.Elevated,
            ForeColor = PvzTheme.Muted,
            Font = new Font("Consolas", 9F),
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Location = new Point(20, 276),
            Size = new Size(640, 180),
        };

        var footer = new Label
        {
            Text = "Туннель Cloudflare (без проброса портов) · сервер PVZS на 127.0.0.1:1729 · "
                + "данные и задачники — в папках sessions и zadachniki рядом с этим exe.",
            AutoSize = false,
            Size = new Size(640, 40),
            Location = new Point(20, 470),
            ForeColor = PvzTheme.Subtle,
            Font = PvzTheme.Caption,
        };

        _server.Exited += () => Ui(() =>
        {
            if (!_started)
                return;
            _serverStatus = "остановлен";
            UpdateStatus();
            Log("Сервер остановился.");
        });
        _tunnel.Exited += () => Ui(() =>
        {
            if (!_started)
                return;
            _tunnelStatus = "прерван";
            UpdateStatus();
            Log("Туннель прервался — нажмите «Запустить», чтобы получить новую ссылку.");
        });
        FormClosing += (_, _) => StopAll(log: false);

        Controls.AddRange([title, hint, _status, linkCaption, _linkBox, copy, open, _startStop, _logBox, footer]);
        UpdateStatus();
        Log("Нажмите «Запустить», чтобы поднять сервер и получить ссылку.");
    }

    public void AutoStart() =>
        Shown += async (_, _) =>
        {
            await Task.Delay(250);
            await StartAsync();
        };

    private async Task StartAsync()
    {
        if (_busy)
            return;
        _busy = true;
        _startStop.Enabled = false;
        _startStop.Text = "Запуск…";
        try
        {
            _linkBox.Clear();
            Log("Проверяю сервер…");
            var serverError = await _server.EnsureAsync(Log);
            if (serverError is not null)
            {
                Log("Ошибка: " + serverError);
                _serverStatus = "ошибка";
                UpdateStatus();
                return;
            }

            _serverStatus = "работает";
            UpdateStatus();

            var cloudflaredError = await _tunnel.EnsureCloudflaredAsync(Log);
            if (cloudflaredError is not null)
                Log("Предупреждение: " + cloudflaredError + " Пробую запасной туннель.");

            var url = await _tunnel.StartAsync(Log);
            if (url is null)
            {
                _tunnelStatus = "ошибка";
                UpdateStatus();
                return;
            }

            _linkBox.Text = url;
            _tunnelStatus = "работает";
            _started = true;
            UpdateStatus();
            Log($"Ссылка готова: {url}");
        }
        finally
        {
            _busy = false;
            if (!_started)
            {
                _startStop.Text = "Запустить";
                _startStop.Enabled = true;
            }
            else
            {
                _startStop.Text = "Остановить";
                _startStop.Enabled = true;
            }
        }
    }

    private void StopAll(bool log)
    {
        var wasStarted = _started;
        _started = false;
        _tunnel.Stop();
        _server.Stop();
        _linkBox.Clear();
        _serverStatus = "остановлен";
        _tunnelStatus = "остановлен";
        UpdateStatus();
        _startStop.Text = "Запустить";
        if (log && wasStarted)
            Log("Сервер и туннель остановлены.");
    }

    private void CopyLink()
    {
        if (string.IsNullOrWhiteSpace(_linkBox.Text))
        {
            Log("Ссылки пока нет — сначала нажмите «Запустить».");
            return;
        }

        try
        {
            Clipboard.SetText(_linkBox.Text);
            Log("Ссылка скопирована в буфер обмена.");
        }
        catch (Exception exception) when (
            exception is System.Runtime.InteropServices.ExternalException
                or System.Runtime.InteropServices.COMException)
        {
            Log("Не удалось скопировать: " + exception.Message);
        }
    }

    private void OpenLink()
    {
        if (string.IsNullOrWhiteSpace(_linkBox.Text))
        {
            Log("Ссылки пока нет — сначала нажмите «Запустить».");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_linkBox.Text) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Log("Не удалось открыть ссылку: " + exception.Message);
        }
    }

    private void UpdateStatus() =>
        _status.Text = $"Сервер: {_serverStatus} · Туннель: {_tunnelStatus}";

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        if (InvokeRequired)
        {
            try
            {
                BeginInvoke(Log, message);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        _logBox.AppendText(line + Environment.NewLine);
        _logLines++;
        if (_logLines <= 600)
            return;
        var lines = _logBox.Lines;
        _logBox.Lines = lines[^(lines.Length - 100)..];
        _logLines -= 100;
    }

    private void Ui(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
            return;
        try
        {
            BeginInvoke(action);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
