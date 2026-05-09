using TerminalShell.Core;
using TerminalShell.Core.Config;
using TerminalShell.Models;

namespace TerminalShell.Tests.Models;

public class MainWindowThemeConfigTests
{
    [Fact]
    public void Normalize_ShouldMigrateLegacyWaitHighlightAndDeriveWaitBackgrounds()
    {
        string legacyHighlight = "#123456";

        MainWindowThemeConfig normalized = MainWindowThemeConfig.Normalize(
            new MainWindowThemeConfig
            {
                WaitForUserInputHighlightColor = string.Empty,
                WaitForUserInputContainerBackgroundColor = string.Empty,
                WaitForUserInputTextBoxBackgroundColor = string.Empty
            },
            legacyHighlight);

        string expectedHighlight = UiColorHelper.NormalizeColorString(
            legacyHighlight,
            MainWindowThemeConfig.DefaultWaitForUserInputHighlightColor);

        Assert.Equal(expectedHighlight, normalized.WaitForUserInputHighlightColor);
        Assert.Equal(
            UiColorHelper.CreateScaledColorString(expectedHighlight, MainWindowThemeConfig.DefaultWaitForUserInputHighlightColor, 0.28),
            normalized.WaitForUserInputContainerBackgroundColor);
        Assert.Equal(
            UiColorHelper.CreateScaledColorString(expectedHighlight, MainWindowThemeConfig.DefaultWaitForUserInputHighlightColor, 0.38),
            normalized.WaitForUserInputTextBoxBackgroundColor);
    }

    [Fact]
    public void TryNormalizeMainWindowTheme_ShouldCreateNormalizedTheme_WhenConfigThemeIsMissing()
    {
        AppConfig config = new()
        {
            MainWindowTheme = null!
        };

        bool changed = ConfigManager.TryNormalizeMainWindowTheme(config, "#654321");

        Assert.True(changed);
        Assert.NotNull(config.MainWindowTheme);
        Assert.Equal("#654321", config.MainWindowTheme.WaitForUserInputHighlightColor);
        Assert.Equal(
            UiColorHelper.CreateScaledColorString("#654321", "#654321", 0.28),
            config.MainWindowTheme.WaitForUserInputContainerBackgroundColor);
        Assert.Equal(
            UiColorHelper.CreateScaledColorString("#654321", "#654321", 0.38),
            config.MainWindowTheme.WaitForUserInputTextBoxBackgroundColor);
        Assert.Equal(MainWindowThemeConfig.DefaultContextMenuBackgroundColor, config.MainWindowTheme.ContextMenuGutterBackgroundColor);
    }

    [Fact]
    public void Normalize_ShouldKeepFixedWaitDefaults_WhenCurrentDefaultHighlightHasMissingDerivedColors()
    {
        MainWindowThemeConfig normalized = MainWindowThemeConfig.Normalize(
            new MainWindowThemeConfig
            {
                WaitForUserInputContainerBackgroundColor = string.Empty,
                WaitForUserInputTextBoxBackgroundColor = string.Empty
            });

        Assert.Equal(MainWindowThemeConfig.DefaultWaitForUserInputHighlightColor, normalized.WaitForUserInputHighlightColor);
        Assert.Equal(MainWindowThemeConfig.DefaultWaitForUserInputContainerBackgroundColor, normalized.WaitForUserInputContainerBackgroundColor);
        Assert.Equal(MainWindowThemeConfig.DefaultWaitForUserInputTextBoxBackgroundColor, normalized.WaitForUserInputTextBoxBackgroundColor);
    }

    [Fact]
    public void Normalize_ShouldDeriveWaitBackgrounds_WhenCustomThemeHighlightHasMissingWaitColors()
    {
        MainWindowThemeConfig normalized = MainWindowThemeConfig.Normalize(
            new MainWindowThemeConfig
            {
                WaitForUserInputHighlightColor = "#654321",
                WaitForUserInputContainerBackgroundColor = string.Empty,
                WaitForUserInputTextBoxBackgroundColor = string.Empty
            });

        Assert.Equal("#654321", normalized.WaitForUserInputHighlightColor);
        Assert.Equal(
            UiColorHelper.CreateScaledColorString("#654321", "#654321", 0.28),
            normalized.WaitForUserInputContainerBackgroundColor);
        Assert.Equal(
            UiColorHelper.CreateScaledColorString("#654321", "#654321", 0.38),
            normalized.WaitForUserInputTextBoxBackgroundColor);
    }

    [Fact]
    public void Normalize_ShouldDefaultContextMenuGutterToContextMenuBackground()
    {
        MainWindowThemeConfig normalized = MainWindowThemeConfig.Normalize(
            new MainWindowThemeConfig
            {
                ContextMenuBackgroundColor = "#112233",
                ContextMenuGutterBackgroundColor = string.Empty
            });

        Assert.Equal("#112233", normalized.ContextMenuBackgroundColor);
        Assert.Equal("#112233", normalized.ContextMenuGutterBackgroundColor);
    }

    [Fact]
    public void Normalize_ShouldDefaultWaitWatermarkToInputWatermarkColor()
    {
        MainWindowThemeConfig normalized = MainWindowThemeConfig.Normalize(
            new MainWindowThemeConfig
            {
                InputWatermarkForegroundColor = "#112233",
                WaitForUserInputWatermarkForegroundColor = string.Empty
            });

        Assert.Equal("#112233", normalized.InputWatermarkForegroundColor);
        Assert.Equal("#112233", normalized.WaitForUserInputWatermarkForegroundColor);
    }

    [Fact]
    public void DefaultMenuPalette_ShouldMatchUnifiedDarkTheme()
    {
        Assert.Equal("#2F3035", MainWindowThemeConfig.DefaultContextMenuBackgroundColor);
        Assert.Equal("#2F3035", MainWindowThemeConfig.DefaultContextMenuGutterBackgroundColor);
        Assert.Equal("#F5F5F5", MainWindowThemeConfig.DefaultContextMenuForegroundColor);
        Assert.Equal("#3B3C41", MainWindowThemeConfig.DefaultContextMenuBorderColor);
        Assert.Equal("#45464B", MainWindowThemeConfig.DefaultContextMenuSeparatorColor);
        Assert.Equal("#B6B7BD", MainWindowThemeConfig.DefaultContextMenuShortcutForegroundColor);
        Assert.Equal("#1496F2", MainWindowThemeConfig.DefaultContextMenuAccentForegroundColor);
        Assert.Equal("#757579", MainWindowThemeConfig.DefaultContextMenuHighlightBackgroundColor);
        Assert.Equal("#D2D4DA", MainWindowThemeConfig.DefaultContextMenuGroupHeaderForegroundColor);
        Assert.Equal("#2C2D31", MainWindowThemeConfig.DefaultCommandPopupBackgroundColor);
        Assert.Equal("#3B3C41", MainWindowThemeConfig.DefaultCommandPopupBorderColor);
        Assert.Equal("#313338", MainWindowThemeConfig.DefaultCommandPopupHeaderBackgroundColor);
        Assert.Equal("#AEB0B7", MainWindowThemeConfig.DefaultCommandPopupHeaderForegroundColor);
        Assert.Equal("#F5F5F5", MainWindowThemeConfig.DefaultCommandPopupItemForegroundColor);
        Assert.Equal("#3E4046", MainWindowThemeConfig.DefaultCommandPopupItemHoverBackgroundColor);
        Assert.Equal("#465361", MainWindowThemeConfig.DefaultCommandPopupItemSelectedBackgroundColor);
    }

    [Fact]
    public void DefaultButtonPalette_ShouldMatchThemeReferenceValues()
    {
        Assert.Equal("#2D2D30", MainWindowThemeConfig.DefaultWaitForUserInputHighlightColor);
        Assert.Equal("#2D2D30", MainWindowThemeConfig.DefaultWaitForUserInputContainerBackgroundColor);
        Assert.Equal("#009873", MainWindowThemeConfig.DefaultWaitForUserInputTextBoxBackgroundColor);
        Assert.Equal("#000000", MainWindowThemeConfig.DefaultWaitForUserInputTextBoxForegroundColor);
        Assert.Equal("#48E58F", MainWindowThemeConfig.DefaultWaitForUserInputWatermarkForegroundColor);
        Assert.Equal("#009873", MainWindowThemeConfig.DefaultSendButtonBackgroundColor);
        Assert.Equal("#000000", MainWindowThemeConfig.DefaultSendButtonForegroundColor);
        Assert.Equal("#008867", MainWindowThemeConfig.DefaultSendButtonHoverBackgroundColor);
        Assert.Equal("#BCBCBC", MainWindowThemeConfig.DefaultSendButtonHoverForegroundColor);
        Assert.Equal("#808080", MainWindowThemeConfig.DefaultHistoryButtonBackgroundColor);
        Assert.Equal("#FFFFFF", MainWindowThemeConfig.DefaultHistoryButtonForegroundColor);
        Assert.Equal("#9A9A9A", MainWindowThemeConfig.DefaultHistoryButtonHoverBackgroundColor);
        Assert.Equal("#3C3C3C", MainWindowThemeConfig.DefaultHistoryButtonHoverForegroundColor);
        Assert.Equal("#505050", MainWindowThemeConfig.DefaultDraftButtonBackgroundColor);
        Assert.Equal("#FFFFFF", MainWindowThemeConfig.DefaultDraftButtonForegroundColor);
        Assert.Equal("#009873", MainWindowThemeConfig.DefaultDraftButtonActiveBackgroundColor);
        Assert.Equal("#000000", MainWindowThemeConfig.DefaultDraftButtonActiveForegroundColor);
    }
}
