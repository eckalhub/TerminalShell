using TerminalShell.Services;

namespace TerminalShell.Tests.Services;

public class UserInputWaitGuardTests
{
    [Fact]
    public void ParseKeywords_ShouldSupportMultiSeparatorInput()
    {
        string raw = "continue\n请确认,请选择；yes/no";

        IReadOnlyList<string> keywords = UserInputWaitGuard.ParseKeywords(raw);

        Assert.Contains("continue", keywords);
        Assert.Contains("请确认", keywords);
        Assert.Contains("请选择", keywords);
        Assert.Contains("yes/no", keywords);
    }

    [Fact]
    public void IsMatch_ShouldDetectConfiguredEnglishKeywordNearPrompt()
    {
        string snapshot = """
            Do you want me to continue with the refactor?
            >
            """;

        bool matched = UserInputWaitGuard.IsMatch(snapshot, UserInputWaitGuard.ParseKeywords("do you want me"));

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_ShouldDetectConfiguredChineseKeywordNearPrompt()
    {
        string snapshot = """
            请确认是否继续执行当前步骤
            >
            """;

        bool matched = UserInputWaitGuard.IsMatch(snapshot, UserInputWaitGuard.ParseKeywords("请确认\n是否继续"));

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_ShouldDetectStructuredChoiceWithoutCustomKeywords()
    {
        string snapshot = """
            Apply these changes? [Y/n]
            >
            """;

        bool matched = UserInputWaitGuard.IsMatch(snapshot, Array.Empty<string>());

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_ShouldIgnoreNonPromptStatusText()
    {
        string snapshot = """
            I will continue with the implementation and prepare the patch next.
            """;

        bool matched = UserInputWaitGuard.IsMatch(snapshot, UserInputWaitGuard.ParseKeywords("continue"));

        Assert.False(matched);
    }

    [Fact]
    public void IsMatch_ShouldDetectChoiceListPromptWithoutExplicitQuestionMark()
    {
        string snapshot = """
            按你给的顺序，图标这里下一步就该做“图标选择器体验增强”了，优先建议先落：
            1. 收藏/置顶
            2. 最近使用里把内置图标也记进去
            3. 排序方式补“默认 / 最近 / 收藏”三种切换
            """;

        bool matched = UserInputWaitGuard.IsMatch(snapshot, UserInputWaitGuard.ParseKeywords("请确认\n请选择"));

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_ShouldDetectEnglishChoiceListPrompt()
    {
        string snapshot = """
            Recommended next steps:
            1. Add favorites
            2. Track recent icons
            3. Add default / recent / favorite sorting
            """;

        bool matched = UserInputWaitGuard.IsMatch(snapshot, Array.Empty<string>());

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_ShouldIgnoreGenericSummaryList()
    {
        string snapshot = """
            Implemented changes:
            1. Added the parser
            2. Updated tests
            3. Improved logging
            """;

        bool matched = UserInputWaitGuard.IsMatch(snapshot, UserInputWaitGuard.ParseKeywords("请确认\nwhich option"));

        Assert.False(matched);
    }

    [Fact]
    public void IsMatch_ShouldDetectChoiceListPromptEvenWhenOneSummaryLineAppearsAfterOptions()
    {
        string snapshot = """
            如果你不想要这种效果，我可以直接给你改成这三种之一：
            1. 只保留细描边，不再填充绿色背景
            2. 改成很淡的透明色
            3. 增加一个开关，允许关闭“预览联动高亮”
            本轮没有改代码，所以也没有触发编译。
            """;

        bool matched = UserInputWaitGuard.IsMatch(snapshot, Array.Empty<string>());

        Assert.True(matched);
    }

    [Fact]
    public void IsMatch_ShouldIgnoreGenericSummaryListEvenWhenOneSummaryLineAppearsAfterOptions()
    {
        string snapshot = """
            Implemented changes:
            1. Added the parser
            2. Updated tests
            3. Improved logging
            No code changes were made in this round.
            """;

        bool matched = UserInputWaitGuard.IsMatch(snapshot, Array.Empty<string>());

        Assert.False(matched);
    }

    [Fact]
    public void IsMatch_ShouldDetectPlanConfirmationPrompt()
    {
        string snapshot = """
            ## Assumptions
            - This round only updates the highlighted command icon entry points.
            Implement this plan?
            1. Yes, implement this plan  Switch to Default and start coding.
            2. No, stay in Plan mode     Continue planning with the model.
            Press enter to confirm or esc to go back
            """;

        bool matched = UserInputWaitGuard.IsMatch(snapshot, Array.Empty<string>());

        Assert.True(matched);
    }
}
