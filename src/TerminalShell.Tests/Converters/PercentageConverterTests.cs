using Xunit;
using TerminalShell.Converters;
using System.Globalization;
using System.Windows.Data;
using TerminalShell.ViewModels;
using TerminalShell.Models;
using TerminalShell.Core.Config;

namespace TerminalShell.Tests.Converters;

public class PercentageConverterTests
{
    private readonly PercentageConverter _converter;
    private readonly DynamicPercentageConverter _dynamicConverter;

    public PercentageConverterTests()
    {
        _converter = new PercentageConverter();
        _dynamicConverter = new DynamicPercentageConverter();
    }

    [Fact]
    public void Convert_ShouldReturnCorrectPercentage_WhenInputIsValid()
    {
        // Arrange
        double parentSize = 1000.0;
        string percentageStr = "50";

        // Act
        var result = _converter.Convert(parentSize, typeof(double), percentageStr, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(500.0, (double)result);
    }

    [Fact]
    public void DynamicConvert_ShouldReturnCorrectPercentage_WhenInputsAreValid()
    {
        // Arrange
        object[] values = new object[] { 200.0, 25.0 }; // 200px height, 25%

        // Act
        var result = _dynamicConverter.Convert(values, typeof(double), null!, CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal(50.0, (double)result);
    }

    [Theory]
    [InlineData("[{TerminalName}] Auto-resizing input.", "Alpha", "", "", "[Alpha] Auto-resizing input.")]
    [InlineData("[{TerminalName}] Auto-resizing input.", "Alpha", "Voice Alpha", "[sound]", "[Voice Alpha] Auto-resizing input. [sound]")]
    [InlineData("[{0}] Auto-resizing input.", "Alpha", "Voice Alpha", "[Remote:18080]", "[Voice Alpha] Auto-resizing input. [Remote:18080]")]
    [InlineData("[{TerminalVoiceName_or_TerminalName}] Auto-resizing input.", "Alpha", "Voice Alpha", "[sound] [Remote:18080]", "[Voice Alpha] Auto-resizing input. [sound] [Remote:18080]")]
    [InlineData("[{TerminalVoiceName_or_TerminalName}] Auto-resizing input.", "Alpha", "   ", "[markdown]", "[Alpha] Auto-resizing input. [markdown]")]
    [InlineData("[{RawTerminalName}] Auto-resizing input.", "Alpha", "Voice Alpha", "[markdown]", "[Alpha] Auto-resizing input. [markdown]")]
    public void DynamicStringFormatConverter_ShouldAppendStatusSuffix_WhenProvided(
        string format,
        string terminalName,
        string terminalVoiceName,
        string statusSuffix,
        string expected)
    {
        DynamicStringFormatConverter converter = new();

        var result = converter.Convert([format, terminalName, terminalVoiceName, statusSuffix], typeof(string), null!, CultureInfo.InvariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(false, "", "markdown", "[markdown]")]
    [InlineData(true, "", "markdown", "[sound] [markdown]")]
    [InlineData(false, "[Remote:18080]", "text", "[Remote:18080] [raw text]")]
    [InlineData(true, "[Remote:18080]", "markdown", "[sound] [Remote:18080] [markdown]")]
    [InlineData(true, "[Remote Error:18080]", "text", "[sound] [Remote Error:18080] [raw text]")]
    public void BuildWatermarkStatusSuffix_ShouldComposeSoundRemoteAndFormatSuffix(
        bool isAllTaskAlertsEnabled,
        string remoteTitleSuffix,
        string clipboardOutputFormat,
        string expected)
    {
        string actual = MainViewModel.BuildWatermarkStatusSuffix(isAllTaskAlertsEnabled, remoteTitleSuffix, clipboardOutputFormat);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryMigrateDefaultInputWatermarkFormat_ShouldUpgradeOldDefaultOnly()
    {
        AppConfig config = new()
        {
            InputWatermarkFormat = AppConfig.OldDefaultInputWatermarkFormat
        };

        bool changed = ConfigManager.TryMigrateDefaultInputWatermarkFormat(config);

        Assert.True(changed);
        Assert.Equal(AppConfig.DefaultInputWatermarkFormat, config.InputWatermarkFormat);
    }

    [Fact]
    public void TryMigrateDefaultInputWatermarkFormat_ShouldUpgradePreviousNormalizedDefaultOnly()
    {
        AppConfig config = new()
        {
            InputWatermarkFormat = AppConfig.PreviousDefaultInputWatermarkFormat
        };

        bool changed = ConfigManager.TryMigrateDefaultInputWatermarkFormat(config);

        Assert.True(changed);
        Assert.Equal(AppConfig.DefaultInputWatermarkFormat, config.InputWatermarkFormat);
    }

    [Fact]
    public void TryMigrateDefaultInputWatermarkFormat_ShouldNormalizeLegacyTerminalNameTokenInsideCustomValue()
    {
        AppConfig config = new()
        {
            InputWatermarkFormat = "[{TerminalName}] Custom"
        };

        bool changed = ConfigManager.TryMigrateDefaultInputWatermarkFormat(config);

        Assert.True(changed);
        Assert.Equal("[{TerminalVoiceName_or_TerminalName}] Custom", config.InputWatermarkFormat);
    }

    [Fact]
    public void TryMigrateDefaultInputWatermarkFormat_ShouldNormalizeLegacyIndexTokenInsideCustomValue()
    {
        AppConfig config = new()
        {
            InputWatermarkFormat = "[{0}] Custom"
        };

        bool changed = ConfigManager.TryMigrateDefaultInputWatermarkFormat(config);

        Assert.True(changed);
        Assert.Equal("[{TerminalVoiceName_or_TerminalName}] Custom", config.InputWatermarkFormat);
    }

    [Fact]
    public void TryMigrateDefaultInputWatermarkFormat_ShouldKeepAlreadyNormalizedCustomValue()
    {
        AppConfig config = new()
        {
            InputWatermarkFormat = "[{TerminalVoiceName_or_TerminalName}] Custom"
        };

        bool changed = ConfigManager.TryMigrateDefaultInputWatermarkFormat(config);

        Assert.False(changed);
        Assert.Equal("[{TerminalVoiceName_or_TerminalName}] Custom", config.InputWatermarkFormat);
    }

}
