// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

namespace PVZ;

/// <summary>
/// Копия темы из PVZP/PvzTheme.cs. Содержимое должно совпадать (отличается только namespace).
/// </summary>
public static class PvzTheme
{
    public static readonly Color Canvas = Color.FromArgb(13, 17, 27);
    public static readonly Color Surface = Color.FromArgb(21, 27, 42);
    public static readonly Color Elevated = Color.FromArgb(30, 38, 56);
    public static readonly Color RaisedSurface = Color.FromArgb(38, 48, 70);
    public static readonly Color Border = Color.FromArgb(55, 65, 95);
    public static readonly Color Ink = Color.FromArgb(226, 232, 240);
    public static readonly Color Muted = Color.FromArgb(148, 163, 184);
    public static readonly Color Subtle = Color.FromArgb(100, 116, 139);
    public static readonly Color Accent = Color.FromArgb(99, 102, 241);
    public static readonly Color AccentHover = Color.FromArgb(129, 132, 241);
    public static readonly Color Success = Color.FromArgb(34, 197, 94);
    public static readonly Color Warning = Color.FromArgb(234, 179, 8);
    public static readonly Color Danger = Color.FromArgb(239, 68, 68);
    public static readonly Color DangerSoft = Color.FromArgb(185, 28, 28);
    public static readonly Color Gold = Color.FromArgb(234, 179, 8);

    public static readonly Font Display = new("Segoe UI", 26F, FontStyle.Bold);
    public static readonly Font Title = new("Segoe UI Semibold", 16F, FontStyle.Bold);
    public static readonly Font Subtitle = new("Segoe UI", 11.5F, FontStyle.Regular);
    public static readonly Font Body = new("Segoe UI", 10F, FontStyle.Regular);
    public static readonly Font BodyBold = new("Segoe UI Semibold", 10F, FontStyle.Bold);
    public static readonly Font Caption = new("Segoe UI", 9F, FontStyle.Regular);
    public static readonly Font Mono = new("Consolas", 22F, FontStyle.Bold);

    public static void ApplyWindow(Form form)
    {
        form.BackColor = Canvas;
        form.ForeColor = Ink;
        form.Font = Body;
    }

    public static void EnableAutoScale(Form form)
    {
        using var probe = new Form { Font = Body };
        var metrics = probe.CurrentAutoScaleDimensions;
        var dpi = probe.DeviceDpi <= 0 ? 96 : probe.DeviceDpi;
        form.AutoScaleMode = AutoScaleMode.Font;
        form.AutoScaleDimensions = new SizeF(metrics.Width * 96f / dpi, metrics.Height * 96f / dpi);
    }

    public static float ScreenScale(Form form) => form.DeviceDpi <= 0 ? 1f : form.DeviceDpi / 96f;

    public static void FitToScreen(Form form)
    {
        form.Load += (_, _) =>
        {
            if (form.FormBorderStyle == FormBorderStyle.None || form.WindowState == FormWindowState.Maximized)
                return;
            form.PerformLayout();
            var area = Screen.FromControl(form).WorkingArea;
            var min = form.MinimumSize;
            if (min.Width > area.Width || min.Height > area.Height)
                form.MinimumSize = new Size(Math.Min(min.Width, area.Width), Math.Min(min.Height, area.Height));
            if (form.Width > area.Width || form.Height > area.Height)
                form.Size = new Size(Math.Min(form.Width, area.Width), Math.Min(form.Height, area.Height));
            if (form.StartPosition == FormStartPosition.CenterScreen)
                form.Location = new Point(area.Left + (area.Width - form.Width) / 2, area.Top + (area.Height - form.Height) / 2);
        };
    }

    public static Button PrimaryButton(string text) => StyleButton(new Button
    {
        Text = text,
        BackColor = Accent,
        ForeColor = Color.White,
        Font = BodyBold,
    }, Accent);

    public static Button SecondaryButton(string text) => StyleButton(new Button
    {
        Text = text,
        BackColor = RaisedSurface,
        ForeColor = Ink,
        Font = BodyBold,
    }, Border);

    public static Button GhostButton(string text) => StyleButton(new Button
    {
        Text = text,
        BackColor = Surface,
        ForeColor = Muted,
        Font = Body,
    }, Border);

    public static Button DangerButton(string text) => StyleButton(new Button
    {
        Text = text,
        BackColor = DangerSoft,
        ForeColor = Color.White,
        Font = BodyBold,
    }, DangerSoft);

    public static Button StyleButton(Button button, Color borderColor)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = borderColor;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Lighten(button.BackColor, 20);
        button.FlatAppearance.MouseDownBackColor = Darken(button.BackColor, 15);
        button.AutoSize = false;
        button.AutoEllipsis = true;
        button.Height = 42;
        button.Cursor = Cursors.Hand;
        button.TextAlign = ContentAlignment.MiddleCenter;
        return button;
    }

    public static Label TitleLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Ink,
        Font = Title,
    };

    public static Label DisplayLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Ink,
        Font = Display,
    };

    public static Label BodyLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Ink,
        Font = Body,
    };

    public static Label MutedLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Muted,
        Font = Body,
    };

    public static Label CaptionLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Subtle,
        Font = Caption,
    };

    public static TextBox InputBox(string? placeholder = null) => new()
    {
        BackColor = Elevated,
        ForeColor = Ink,
        BorderStyle = BorderStyle.FixedSingle,
        Font = Body,
        Height = 34,
        PlaceholderText = placeholder ?? "",
    };

    public static Panel Card()
    {
        var panel = new Panel
        {
            BackColor = Surface,
            Padding = new Padding(20),
            BorderStyle = BorderStyle.None,
        };
        panel.Paint += (sender, e) =>
        {
            if (sender is Panel panelSender)
            {
                using var pen = new Pen(Border, 1);
                e.Graphics.DrawRectangle(pen, 0, 0, panelSender.Width - 1, panelSender.Height - 1);
            }
        };
        return panel;
    }

    public static Label Badge(string text, Color background, Color foreground) => new()
    {
        Text = "  " + text + "  ",
        AutoSize = true,
        BackColor = background,
        ForeColor = foreground,
        Font = Caption,
        Padding = new Padding(4, 2, 4, 2),
    };

    public static Color Lighten(Color color, int amount) => Color.FromArgb(
        color.A,
        Math.Min(255, color.R + amount),
        Math.Min(255, color.G + amount),
        Math.Min(255, color.B + amount));

    public static Color Darken(Color color, int amount) => Color.FromArgb(
        color.A,
        Math.Max(0, color.R - amount),
        Math.Max(0, color.G - amount),
        Math.Max(0, color.B - amount));
}
