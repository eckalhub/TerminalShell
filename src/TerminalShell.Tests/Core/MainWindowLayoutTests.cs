using System.Windows;
using TerminalShell.Core;

namespace TerminalShell.Tests.Core;

public class MainWindowLayoutTests
{
    [Fact]
    public void NormalizeSavedBounds_ShouldRepairMinimizedSentinelPosition()
    {
        Rect primaryWorkingArea = new(0, 0, 1920, 1080);

        MainWindowBoundsRepairResult result = MainWindowLayout.NormalizeSavedBounds(
            -32000,
            -32000,
            1000,
            800,
            new[] { primaryWorkingArea },
            primaryWorkingArea);

        Assert.True(result.WasRepaired);
        Assert.Equal("MinimizedSentinelPosition", result.Reason);
        Assert.Equal(new Rect(460, 140, 1000, 800), result.Bounds);
    }

    [Fact]
    public void NormalizeSavedBounds_ShouldKeepVisibleNegativeCoordinateOnSecondaryMonitor()
    {
        Rect leftMonitor = new(-1920, 0, 1920, 1080);
        Rect primaryWorkingArea = new(0, 0, 1920, 1080);

        MainWindowBoundsRepairResult result = MainWindowLayout.NormalizeSavedBounds(
            -1500,
            50,
            1200,
            800,
            new[] { leftMonitor, primaryWorkingArea },
            primaryWorkingArea);

        Assert.False(result.WasRepaired);
        Assert.Null(result.Reason);
        Assert.Equal(new Rect(-1500, 50, 1200, 800), result.Bounds);
    }

    [Fact]
    public void NormalizeSavedBounds_ShouldRepairBoundsWhenWindowIsCompletelyOffScreen()
    {
        Rect primaryWorkingArea = new(0, 0, 1920, 1080);

        MainWindowBoundsRepairResult result = MainWindowLayout.NormalizeSavedBounds(
            5000,
            5000,
            1000,
            800,
            new[] { primaryWorkingArea },
            primaryWorkingArea);

        Assert.True(result.WasRepaired);
        Assert.Equal("OffScreenPosition", result.Reason);
        Assert.Equal(new Rect(460, 140, 1000, 800), result.Bounds);
    }

    [Fact]
    public void NormalizeSavedBounds_ShouldRepairInvalidCoordinateToFallbackDefaultWhenNoWorkingAreaExists()
    {
        MainWindowBoundsRepairResult result = MainWindowLayout.NormalizeSavedBounds(
            double.NaN,
            10,
            900,
            700,
            Array.Empty<Rect>());

        Assert.True(result.WasRepaired);
        Assert.Equal("InvalidWindowCoordinate", result.Reason);
        Assert.Equal(new Rect(MainWindowLayout.DefaultWindowLeft, MainWindowLayout.DefaultWindowTop, 900, 700), result.Bounds);
    }

    [Fact]
    public void NormalizeSavedBounds_ShouldClampInvalidWindowSizeWithoutMovingVisibleWindow()
    {
        Rect primaryWorkingArea = new(0, 0, 1920, 1080);

        MainWindowBoundsRepairResult result = MainWindowLayout.NormalizeSavedBounds(
            200,
            100,
            50,
            12000,
            new[] { primaryWorkingArea },
            primaryWorkingArea);

        Assert.True(result.WasRepaired);
        Assert.Equal("InvalidWindowSize", result.Reason);
        Assert.Equal(new Rect(200, 100, 100, 10000), result.Bounds);
    }
}
