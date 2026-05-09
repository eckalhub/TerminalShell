using System;

namespace TerminalShell.Core;

public static class HistoryWindowLayout
{
    public const double DefaultWindowWidth = 1600.0;
    public const double DefaultWindowHeight = 1200.0;
    public const double DefaultLeftPaneWidth = 797.5;

    public const double MinWindowWidth = 600.0;
    public const double MaxWindowWidth = 5000.0;
    public const double MinWindowHeight = 400.0;
    public const double MaxWindowHeight = 5000.0;
    public const double MinPaneWidth = 150.0;
    public const double MaxPaneWidth = 4500.0;
    public const double DefaultSplitterWidth = 5.0;

    public static double ClampWindowWidth(double width)
    {
        return ClampOrDefault(width, DefaultWindowWidth, MinWindowWidth, MaxWindowWidth);
    }

    public static double ClampWindowHeight(double height)
    {
        return ClampOrDefault(height, DefaultWindowHeight, MinWindowHeight, MaxWindowHeight);
    }

    public static double ClampSavedLeftPaneWidth(double width)
    {
        return ClampOrDefault(width, DefaultLeftPaneWidth, MinPaneWidth, MaxPaneWidth);
    }

    public static double ClampRestoredLeftPaneWidth(double width, double totalAvailableWidth, double splitterWidth = DefaultSplitterWidth)
    {
        double desiredWidth = ClampSavedLeftPaneWidth(width);
        double effectiveSplitterWidth = splitterWidth > 0 ? splitterWidth : DefaultSplitterWidth;
        double maxLeftPaneWidth = Math.Min(MaxPaneWidth, totalAvailableWidth - effectiveSplitterWidth - MinPaneWidth);

        if (double.IsNaN(maxLeftPaneWidth) || double.IsInfinity(maxLeftPaneWidth) || maxLeftPaneWidth < MinPaneWidth)
        {
            return MinPaneWidth;
        }

        return Math.Clamp(desiredWidth, MinPaneWidth, maxLeftPaneWidth);
    }

    private static double ClampOrDefault(double value, double defaultValue, double minValue, double maxValue)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return defaultValue;
        }

        return Math.Clamp(value, minValue, maxValue);
    }
}
