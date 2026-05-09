using TerminalShell.Core;

namespace TerminalShell.Tests.Core;

public class HistoryWindowLayoutTests
{
    [Fact]
    public void ClampWindowWidth_ShouldClampToMinimum()
    {
        Assert.Equal(HistoryWindowLayout.MinWindowWidth, HistoryWindowLayout.ClampWindowWidth(100));
    }

    [Fact]
    public void ClampWindowHeight_ShouldReturnDefaultForNaN()
    {
        Assert.Equal(HistoryWindowLayout.DefaultWindowHeight, HistoryWindowLayout.ClampWindowHeight(double.NaN));
    }

    [Fact]
    public void ClampSavedLeftPaneWidth_ShouldClampToConfiguredMaximum()
    {
        Assert.Equal(HistoryWindowLayout.MaxPaneWidth, HistoryWindowLayout.ClampSavedLeftPaneWidth(6000));
    }

    [Fact]
    public void ClampRestoredLeftPaneWidth_ShouldPreserveMinimumRightPaneWidth()
    {
        double actual = HistoryWindowLayout.ClampRestoredLeftPaneWidth(900, 1000, 5);

        Assert.Equal(845, actual);
    }
}
