using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using LangSwitch.Core;

namespace LangSwitch.UI;

static class TrayIconFactory
{
    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    const int TraySize = 32;

    public static Icon Create(LayoutInfo layout)
    {
        using var bitmap = LoadFlagBitmap(layout.FlagCode);
        using var scaled = Scale(bitmap, TraySize, TraySize);
        DrawBorder(scaled);

        IntPtr hIcon = scaled.GetHicon();
        try
        {
            // Clone so we own the icon data, then free the raw HICON
            return (Icon)Icon.FromHandle(hIcon).Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    public static Bitmap LoadFlagBitmap(string flagCode)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "Flags", $"{flagCode}.png"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "Flags", $"{flagCode.ToUpperInvariant()}.png"),
        };

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try { return new Bitmap(path); }
            catch { }
        }

        return CreateFallback(flagCode, 64, 42);
    }

    public static Bitmap Scale(Bitmap source, int width, int height)
    {
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(result);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = SmoothingMode.HighQuality;
        g.DrawImage(source, 0, 0, width, height);
        return result;
    }

    static void DrawBorder(Bitmap bmp)
    {
        using var g = Graphics.FromImage(bmp);
        using var pen = new Pen(Color.FromArgb(80, 0, 0, 0), 1);
        g.DrawRectangle(pen, 0, 0, bmp.Width - 1, bmp.Height - 1);
    }

    static Bitmap CreateFallback(string flagCode, int width, int height)
    {
        var (top, mid, bot) = FallbackColors(flagCode);
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        int h = height / 3;
        g.FillRectangle(new SolidBrush(top), 0, 0, width, h);
        g.FillRectangle(new SolidBrush(mid), 0, h, width, h);
        g.FillRectangle(new SolidBrush(bot), 0, h * 2, width, height - h * 2);
        return bmp;
    }

    static (Color, Color, Color) FallbackColors(string code) => code.ToLowerInvariant() switch
    {
        "us" => (Color.Red, Color.White, Color.Blue),
        "gb" => (Color.Red, Color.White, Color.Blue),
        "ru" => (Color.White, Color.Blue, Color.Red),
        "ua" => (Color.Blue, Color.Blue, Color.Gold),
        "de" => (Color.Black, Color.Red, Color.Gold),
        "fr" => (Color.Blue, Color.White, Color.Red),
        "pl" => (Color.White, Color.Red, Color.Red),
        "it" => (Color.Green, Color.White, Color.Red),
        _ => (Color.Gray, Color.Silver, Color.Gray),
    };
}
