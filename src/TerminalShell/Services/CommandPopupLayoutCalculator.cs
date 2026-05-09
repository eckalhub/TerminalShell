namespace TerminalShell.Services;

public static class CommandPopupLayoutCalculator
{
    public static double CalculateTopPlacementMaxHeight(
        double anchorTopScreenPixels,
        double workingAreaTopScreenPixels,
        double pixelsPerDipY,
        double topSafetyMarginDip,
        double popupGapDip)
    {
        double safePixelsPerDipY = pixelsPerDipY > 0 ? pixelsPerDipY : 1.0;
        double availablePixels = anchorTopScreenPixels
            - workingAreaTopScreenPixels
            - (topSafetyMarginDip * safePixelsPerDipY)
            - (popupGapDip * safePixelsPerDipY);

        double availableDip = availablePixels / safePixelsPerDipY;
        return Math.Max(1.0, Math.Floor(availableDip));
    }
}
