using TerminalShell.Services;

namespace TerminalShell.Tests.Services;

public class CommandPopupLayoutCalculatorTests
{
    [Fact]
    public void CalculateTopPlacementMaxHeight_ShouldUseFullAvailableSpaceAboveInput()
    {
        double result = CommandPopupLayoutCalculator.CalculateTopPlacementMaxHeight(
            anchorTopScreenPixels: 900,
            workingAreaTopScreenPixels: 0,
            pixelsPerDipY: 1,
            topSafetyMarginDip: 4,
            popupGapDip: 5);

        Assert.Equal(891, result);
    }

    [Fact]
    public void CalculateTopPlacementMaxHeight_ShouldClampToPositiveHeight_WhenSpaceIsTiny()
    {
        double result = CommandPopupLayoutCalculator.CalculateTopPlacementMaxHeight(
            anchorTopScreenPixels: 8,
            workingAreaTopScreenPixels: 0,
            pixelsPerDipY: 1,
            topSafetyMarginDip: 4,
            popupGapDip: 5);

        Assert.Equal(1, result);
    }

    [Fact]
    public void CalculateTopPlacementMaxHeight_ShouldFallback_WhenPixelsPerDipIsInvalid()
    {
        double result = CommandPopupLayoutCalculator.CalculateTopPlacementMaxHeight(
            anchorTopScreenPixels: 500,
            workingAreaTopScreenPixels: 100,
            pixelsPerDipY: 0,
            topSafetyMarginDip: 4,
            popupGapDip: 5);

        Assert.Equal(391, result);
    }
}
