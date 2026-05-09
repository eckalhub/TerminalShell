using TerminalShell.Core;

namespace TerminalShell.Tests.Services;

public class CustomCommandDefaultsTests
{
    [Fact]
    public void GetEffectiveValue_ShouldReturnTemplate_WhenConfiguredValueIsBlank()
    {
        string effective = CustomCommandDefaults.GetEffectiveValue("   ");

        Assert.Equal(CustomCommandDefaults.Template, effective);
    }

    [Fact]
    public void GetEffectiveValue_ShouldKeepConfiguredValue_WhenConfiguredValueExists()
    {
        const string configured = "[F9]\ncustom line\n[/F9]";

        string effective = CustomCommandDefaults.GetEffectiveValue(configured);

        Assert.Equal(configured, effective);
    }

    [Fact]
    public void Template_ShouldMatchBundledStarterCommands()
    {
        Assert.Contains("[F1]\n确认,按你的建议来\n进度到多少了现在,剩下什么没做\n[/F1]", CustomCommandDefaults.Template, StringComparison.Ordinal);
        Assert.Contains("[11]\ngit init", CustomCommandDefaults.Template, StringComparison.Ordinal);
        Assert.DoesNotContain("使用ralph方法", CustomCommandDefaults.Template, StringComparison.Ordinal);
    }
}
