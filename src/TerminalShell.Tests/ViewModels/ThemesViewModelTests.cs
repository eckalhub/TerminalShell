using System.Linq;
using System.Reflection;
using TerminalShell.Models;
using TerminalShell.ViewModels;

namespace TerminalShell.Tests.ViewModels;

public class ThemesViewModelTests
{
    [Fact]
    public void Load_ShouldPopulateLivePreviewThemeState()
    {
        ThemesViewModel viewModel = new();
        viewModel.Load(new MainWindowThemeConfig
        {
            WindowBackgroundColor = "#112233",
            InputTextBoxForegroundColor = "#445566",
            ContextMenuBackgroundColor = "#778899",
            WaitForUserInputWatermarkForegroundColor = "#AABBCC"
        });

        Assert.Equal("#112233", viewModel.LivePreviewThemeState.WindowBackgroundColor);
        Assert.Equal("#445566", viewModel.LivePreviewThemeState.InputTextBoxForegroundColor);
        Assert.Equal("#778899", viewModel.LivePreviewThemeState.ContextMenuBackgroundColor);
        Assert.Equal("#AABBCC", viewModel.LivePreviewThemeState.WaitForUserInputWatermarkForegroundColor);
    }

    [Fact]
    public void ChangingThemeItemValue_ShouldRefreshLivePreviewThemeState()
    {
        ThemesViewModel viewModel = new();
        ThemeColorItemViewModel item = FindItem(viewModel, nameof(MainWindowThemeConfig.SendButtonHoverBackgroundColor));

        item.Value = "#123456";

        Assert.Equal("#123456", viewModel.LivePreviewThemeState.SendButtonHoverBackgroundColor);
    }

    [Fact]
    public void ChangingWaitWatermarkThemeItem_ShouldRefreshLivePreviewThemeState()
    {
        ThemesViewModel viewModel = new();
        ThemeColorItemViewModel item = FindItem(viewModel, nameof(MainWindowThemeConfig.WaitForUserInputWatermarkForegroundColor));

        item.Value = "#ABCDEF";

        Assert.Equal("#ABCDEF", viewModel.LivePreviewThemeState.WaitForUserInputWatermarkForegroundColor);
    }

    [Fact]
    public void ResetAllToDefaultsCommand_ShouldRestoreEveryThemeColorToDefault()
    {
        ThemesViewModel viewModel = new();
        viewModel.Load(new MainWindowThemeConfig
        {
            WindowBackgroundColor = "#112233",
            InputTextBoxForegroundColor = "#445566",
            SendButtonHoverBackgroundColor = "#778899",
            ContextMenuGutterBackgroundColor = "#AABBCC",
            CommandPopupItemSelectedBackgroundColor = "#DDEEFF"
        });

        viewModel.ResetAllToDefaultsCommand.Execute(null);

        MainWindowThemeConfig actual = viewModel.BuildThemeConfig();
        MainWindowThemeConfig expected = MainWindowThemeConfig.Normalize(new MainWindowThemeConfig());

        PropertyInfo[] themeProperties = typeof(MainWindowThemeConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string) && property.CanRead && property.CanWrite)
            .ToArray();

        foreach (PropertyInfo property in themeProperties)
        {
            Assert.Equal(property.GetValue(expected), property.GetValue(actual));
        }

        Assert.Equal(expected.ContextMenuBackgroundColor, viewModel.LivePreviewThemeState.ContextMenuBackgroundColor);
        Assert.Equal(expected.CommandPopupItemSelectedBackgroundColor, viewModel.LivePreviewThemeState.CommandPopupItemSelectedBackgroundColor);
    }

    private static ThemeColorItemViewModel FindItem(ThemesViewModel viewModel, string key)
    {
        return viewModel.Groups
            .SelectMany(group => group.Items)
            .Single(item => item.Key == key);
    }
}
