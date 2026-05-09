namespace TerminalShell.Core;

public static class ColorPaletteHelper
{
    public const int SlotCount = 30;
    public const string DefaultSlotColor = "#FFFFFF";

    public static List<string> CreateDefaultPalette()
    {
        return Enumerable.Repeat(DefaultSlotColor, SlotCount).ToList();
    }

    public static List<string> NormalizePalette(IEnumerable<string>? rawColors)
    {
        List<string> normalized = new(SlotCount);

        if (rawColors != null)
        {
            foreach (string color in rawColors.Take(SlotCount))
            {
                normalized.Add(UiColorHelper.NormalizeColorString(color, DefaultSlotColor));
            }
        }

        while (normalized.Count < SlotCount)
        {
            normalized.Add(DefaultSlotColor);
        }

        return normalized;
    }
}
