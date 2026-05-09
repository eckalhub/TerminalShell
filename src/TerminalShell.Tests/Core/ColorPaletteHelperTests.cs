using TerminalShell.Core;
using TerminalShell.Core.Config;
using TerminalShell.Models;

namespace TerminalShell.Tests.Core;

public class ColorPaletteHelperTests
{
    [Fact]
    public void NormalizePalette_ShouldReturnThirtyDefaultSlots_WhenInputIsNull()
    {
        List<string> normalized = ColorPaletteHelper.NormalizePalette(null);

        Assert.Equal(ColorPaletteHelper.SlotCount, normalized.Count);
        Assert.All(normalized, color => Assert.Equal(ColorPaletteHelper.DefaultSlotColor, color));
    }

    [Fact]
    public void NormalizePalette_ShouldNormalizeValuesAndPadToThirtySlots()
    {
        List<string> normalized = ColorPaletteHelper.NormalizePalette(
        [
            "#abc123",
            "bad-color",
            "Red"
        ]);

        Assert.Equal(ColorPaletteHelper.SlotCount, normalized.Count);
        Assert.Equal("#ABC123", normalized[0]);
        Assert.Equal(ColorPaletteHelper.DefaultSlotColor, normalized[1]);
        Assert.Equal("#FF0000", normalized[2]);
        Assert.All(normalized.Skip(3), color => Assert.Equal(ColorPaletteHelper.DefaultSlotColor, color));
    }

    [Fact]
    public void TryNormalizeCustomColors_ShouldNormalizeAndReportChanged_WhenPaletteIsDirty()
    {
        AppConfig config = new()
        {
            CustomColors =
            [
                "#abc123",
                "bad-color"
            ]
        };

        bool changed = ConfigManager.TryNormalizeCustomColors(config);

        Assert.True(changed);
        Assert.Equal(ColorPaletteHelper.SlotCount, config.CustomColors.Count);
        Assert.Equal("#ABC123", config.CustomColors[0]);
        Assert.Equal(ColorPaletteHelper.DefaultSlotColor, config.CustomColors[1]);
    }

    [Fact]
    public void TryNormalizeCustomColors_ShouldKeepAlreadyNormalizedPalette()
    {
        AppConfig config = new()
        {
            CustomColors = ColorPaletteHelper.CreateDefaultPalette()
        };

        bool changed = ConfigManager.TryNormalizeCustomColors(config);

        Assert.False(changed);
        Assert.Equal(ColorPaletteHelper.SlotCount, config.CustomColors.Count);
    }
}
