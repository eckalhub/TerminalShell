using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace TerminalShell.Models;

public readonly struct HsvColor
{
    public double H { get; init; }
    public double S { get; init; }
    public double V { get; init; }

    public HsvColor(double h, double s, double v)
    {
        H = h;
        S = s;
        V = v;
    }
}

public static class ColorHelper
{
    public static Color HsvToRgb(double h, double s, double v)
    {
        int hi = Convert.ToInt32(Math.Floor(h / 60)) % 6;
        double f = h / 60 - Math.Floor(h / 60);

        v *= 255;
        byte vByte = Convert.ToByte(Math.Clamp(v, 0, 255));
        byte p = Convert.ToByte(Math.Clamp(v * (1 - s), 0, 255));
        byte q = Convert.ToByte(Math.Clamp(v * (1 - f * s), 0, 255));
        byte t = Convert.ToByte(Math.Clamp(v * (1 - (1 - f) * s), 0, 255));

        return hi switch
        {
            0 => Color.FromRgb(vByte, t, p),
            1 => Color.FromRgb(q, vByte, p),
            2 => Color.FromRgb(p, vByte, t),
            3 => Color.FromRgb(p, q, vByte),
            4 => Color.FromRgb(t, p, vByte),
            _ => Color.FromRgb(vByte, p, q),
        };
    }

    public static HsvColor RgbToHsv(Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0;
        if (delta > 0)
        {
            if (max == r)
            {
                h = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                h = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                h = 60 * (((r - g) / delta) + 4);
            }
        }

        if (h < 0)
        {
            h += 360;
        }

        double s = max == 0 ? 0 : delta / max;
        double v = max;

        return new HsvColor(h, s, v);
    }

    public static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    public static Color? HexToColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        try
        {
            string normalized = hex.StartsWith('#') ? hex : $"#{hex}";
            return (Color)ColorConverter.ConvertFromString(normalized);
        }
        catch
        {
            return null;
        }
    }
}
