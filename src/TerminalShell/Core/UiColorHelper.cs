using System;
using System.Globalization;

namespace TerminalShell.Core;

public static class UiColorHelper
{
    public static string NormalizeColorString(string? rawValue, string fallbackValue)
    {
        System.Windows.Media.Color color = ParseOrDefault(rawValue, fallbackValue);
        return ToColorString(color);
    }

    public static System.Windows.Media.SolidColorBrush CreateSolidBrush(string? rawValue, string fallbackValue)
    {
        return CreateBrush(ParseOrDefault(rawValue, fallbackValue));
    }

    public static string CreateScaledColorString(string? rawValue, string fallbackValue, double factor)
    {
        return ToColorString(CreateScaledColor(ParseOrDefault(rawValue, fallbackValue), factor));
    }

    public static System.Windows.Media.SolidColorBrush CreateScaledBrush(string? rawValue, string fallbackValue, double factor)
    {
        return CreateBrush(CreateScaledColor(ParseOrDefault(rawValue, fallbackValue), factor));
    }

    private static System.Windows.Media.Color ParseOrDefault(string? rawValue, string fallbackValue)
    {
        if (TryParseColor(rawValue, out System.Windows.Media.Color parsedColor))
        {
            return parsedColor;
        }

        if (TryParseColor(fallbackValue, out parsedColor))
        {
            return parsedColor;
        }

        return System.Windows.Media.Colors.Orange;
    }

    private static bool TryParseColor(string? rawValue, out System.Windows.Media.Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        try
        {
            object? converted = System.Windows.Media.ColorConverter.ConvertFromString(rawValue.Trim());
            if (converted is System.Windows.Media.Color parsedColor)
            {
                color = parsedColor;
                return true;
            }
        }
        catch (FormatException)
        {
        }
        catch (NotSupportedException)
        {
        }

        return false;
    }

    private static System.Windows.Media.SolidColorBrush CreateBrush(System.Windows.Media.Color color)
    {
        System.Windows.Media.SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    private static System.Windows.Media.Color CreateScaledColor(System.Windows.Media.Color baseColor, double factor)
    {
        factor = Math.Clamp(factor, 0.0, 1.0);

        return System.Windows.Media.Color.FromArgb(
            baseColor.A,
            ScaleComponent(baseColor.R, factor),
            ScaleComponent(baseColor.G, factor),
            ScaleComponent(baseColor.B, factor));
    }

    private static string ToColorString(System.Windows.Media.Color color)
    {
        return color.A == byte.MaxValue
            ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
            : $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static byte ScaleComponent(byte component, double factor)
    {
        return (byte)Math.Clamp((int)Math.Round(component * factor, MidpointRounding.AwayFromZero), 0, 255);
    }
}
