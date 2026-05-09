using TerminalShell.Services;

namespace TerminalShell.Tests.Services;

public class CustomCommandParserTests
{
    [Fact]
    public void Validate_ShouldPass_WhenBlockTagsAreBalanced()
    {
        string text = """
            [F1]
            alpha
            [/F1]
            [F2]
            beta
            [/F2]
            """;

        CustomCommandValidationResult result = CustomCommandParser.Validate(text);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ShouldFail_WhenOpenTagIsNotClosed()
    {
        string text = """
            [F3]
            alpha
            beta
            """;

        CustomCommandValidationResult result = CustomCommandParser.Validate(text);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("[F3] is not closed", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_ShouldFail_WhenClosingTagDoesNotMatch()
    {
        string text = """
            [F1]
            alpha
            [/F2]
            """;

        CustomCommandValidationResult result = CustomCommandParser.Validate(text);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("expected [/F1]", StringComparison.Ordinal));
    }
}
