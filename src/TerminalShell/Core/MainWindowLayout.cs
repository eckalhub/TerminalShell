using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace TerminalShell.Core;

public readonly record struct MainWindowBoundsRepairResult(Rect Bounds, bool WasRepaired, string? Reason);

public static class MainWindowLayout
{
    public const double DefaultWindowLeft = 100.0;
    public const double DefaultWindowTop = 100.0;
    public const double DefaultWindowWidth = 2980.0;
    public const double DefaultWindowHeight = 1300.0;

    private const double MinWindowWidth = 100.0;
    private const double MaxWindowWidth = 10000.0;
    private const double MinWindowHeight = 100.0;
    private const double MaxWindowHeight = 10000.0;
    private const double MinimizedSentinelCoordinate = -32000.0;
    private const double ComparisonTolerance = 0.01;

    public static MainWindowBoundsRepairResult NormalizeSavedBounds(
        double left,
        double top,
        double width,
        double height,
        IEnumerable<Rect>? workingAreas,
        Rect? primaryWorkingArea = null)
    {
        double normalizedWidth = ClampDimension(width, DefaultWindowWidth, MinWindowWidth, MaxWindowWidth);
        double normalizedHeight = ClampDimension(height, DefaultWindowHeight, MinWindowHeight, MaxWindowHeight);
        bool dimensionsChanged = !AreClose(width, normalizedWidth) || !AreClose(height, normalizedHeight);

        List<Rect> visibleWorkingAreas = (workingAreas ?? Enumerable.Empty<Rect>())
            .Where(IsValidWorkingArea)
            .ToList();
        Rect effectivePrimaryWorkingArea = ChoosePrimaryWorkingArea(primaryWorkingArea, visibleWorkingAreas);

        string? repairReason = GetRepairReason(left, top, normalizedWidth, normalizedHeight, dimensionsChanged, visibleWorkingAreas);
        if (repairReason == null)
        {
            return new MainWindowBoundsRepairResult(
                new Rect(left, top, normalizedWidth, normalizedHeight),
                false,
                null);
        }

        if (repairReason == "InvalidWindowSize")
        {
            return new MainWindowBoundsRepairResult(
                new Rect(left, top, normalizedWidth, normalizedHeight),
                true,
                repairReason);
        }

        return new MainWindowBoundsRepairResult(
            CreateRepairedBounds(normalizedWidth, normalizedHeight, effectivePrimaryWorkingArea),
            true,
            repairReason);
    }

    private static string? GetRepairReason(
        double left,
        double top,
        double width,
        double height,
        bool dimensionsChanged,
        IReadOnlyCollection<Rect> visibleWorkingAreas)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top))
        {
            return "InvalidWindowCoordinate";
        }

        if (left <= MinimizedSentinelCoordinate || top <= MinimizedSentinelCoordinate)
        {
            return "MinimizedSentinelPosition";
        }

        if (visibleWorkingAreas.Count == 0)
        {
            return "NoWorkingAreaAvailable";
        }

        Rect candidateBounds = new(left, top, width, height);
        if (!visibleWorkingAreas.Any(area => area.IntersectsWith(candidateBounds)))
        {
            return "OffScreenPosition";
        }

        if (dimensionsChanged)
        {
            return "InvalidWindowSize";
        }

        return null;
    }

    private static Rect CreateRepairedBounds(double width, double height, Rect primaryWorkingArea)
    {
        if (!IsValidWorkingArea(primaryWorkingArea))
        {
            return new Rect(DefaultWindowLeft, DefaultWindowTop, width, height);
        }

        double repairedLeft = primaryWorkingArea.Left + ((primaryWorkingArea.Width - width) / 2.0);
        double repairedTop = primaryWorkingArea.Top + ((primaryWorkingArea.Height - height) / 2.0);
        return new Rect(repairedLeft, repairedTop, width, height);
    }

    private static Rect ChoosePrimaryWorkingArea(Rect? primaryWorkingArea, IReadOnlyList<Rect> visibleWorkingAreas)
    {
        if (primaryWorkingArea.HasValue && IsValidWorkingArea(primaryWorkingArea.Value))
        {
            return primaryWorkingArea.Value;
        }

        return visibleWorkingAreas.Count > 0
            ? visibleWorkingAreas[0]
            : Rect.Empty;
    }

    private static bool IsValidWorkingArea(Rect workingArea)
    {
        return double.IsFinite(workingArea.Left)
            && double.IsFinite(workingArea.Top)
            && double.IsFinite(workingArea.Width)
            && double.IsFinite(workingArea.Height)
            && workingArea.Width > 0
            && workingArea.Height > 0;
    }

    private static double ClampDimension(double value, double defaultValue, double minValue, double maxValue)
    {
        if (!double.IsFinite(value))
        {
            return defaultValue;
        }

        return Math.Clamp(value, minValue, maxValue);
    }

    private static bool AreClose(double left, double right)
    {
        if (!double.IsFinite(left) || !double.IsFinite(right))
        {
            return false;
        }

        return Math.Abs(left - right) < ComparisonTolerance;
    }
}
