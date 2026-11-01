// Copyright © 2026 Козыро Ярослав Дмитриевич, Захарик Тимур Александрович. Все права защищены, подробности в LICENSE.

using System.Drawing.Text;

namespace PVZ.Player;

public static class PvzFormButtonIcons
{
    public static class Glyphs
    {
        public const char Teacher = '\uE77B';
        public const char Continue = '\uE72A';
        public const char OpenFile = '\uE8E5';
        public const char Cloud = '\uE753';
        public const char Back = '\uE72B';
        public const char Connect = '\uE703';
        public const char Edit = '\uE70F';
        public const char Download = '\uE896';
        public const char Refresh = '\uE72C';
        public const char Globe = '\uE774';
        public const char Copy = '\uE8C8';
    }

    private static readonly Font? Mdl2Font;

    static PvzFormButtonIcons()
    {
        try
        {
            Mdl2Font = new Font("Segoe MDL2 Assets", 18F, FontStyle.Regular, GraphicsUnit.Pixel);
        }
        catch (ArgumentException)
        {
            Mdl2Font = null;
        }
    }

    public static void Apply(Button button, char glyph, Color color)
    {
        var image = CreateGlyphImage(glyph, color);
        if (image is null)
            return;

        button.Image = image;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        button.ImageAlign = ContentAlignment.MiddleLeft;
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.AutoEllipsis = true;
        button.Padding = new Padding(20, 0, 12, 0);
    }

    private static Bitmap? CreateGlyphImage(char glyph, Color color)
    {
        if (Mdl2Font is null)
            return null;

        const int size = 28;
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        var text = glyph.ToString();
        var measured = graphics.MeasureString(text, Mdl2Font);
        graphics.DrawString(
            text,
            Mdl2Font,
            new SolidBrush(color),
            (size - measured.Width) / 2f,
            (size - measured.Height) / 2f);
        return bitmap;
    }
}
