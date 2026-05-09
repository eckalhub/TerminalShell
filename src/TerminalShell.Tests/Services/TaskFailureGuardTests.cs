using TerminalShell.Services;
using TerminalShell.Core;

namespace TerminalShell.Tests.Services;

public class TaskFailureGuardTests
{
    [Fact]
    public void TryMatch_ShouldPreferLongerMoreSpecificKeyword()
    {
        IReadOnlyList<string> keywords = TaskFailureGuard.ParseKeywords("""
usage limit
You've hit your usage limit
try again later
""");

        bool matched = TaskFailureGuard.TryMatch(
            """
            Some earlier line
            You've hit your usage limit. Try again later
            """,
            keywords,
            out string matchedKeyword);

        Assert.True(matched);
        Assert.Equal("You've hit your usage limit", matchedKeyword);
    }

    [Fact]
    public void TryMatch_ShouldIgnoreOldLinesOutsideRecentTailWindow()
    {
        string snapshot = string.Join(
            "\n",
            new[]
            {
                "You've hit your usage limit",
                "line 2",
                "line 3",
                "line 4",
                "line 5",
                "line 6",
                "line 7",
                "line 8",
                "line 9",
                "line 10",
                "line 11",
                "line 12",
                "line 13"
            });

        bool matched = TaskFailureGuard.TryMatch(
            snapshot,
            TaskFailureGuard.ParseKeywords("You've hit your usage limit"),
            out _);

        Assert.False(matched);
    }

    [Fact]
    public void TryMatch_ShouldDetectExceededRetryLimitAsFailure()
    {
        IReadOnlyList<string> keywords = TaskFailureGuard.ParseKeywords("""
too many requests
429 too many requests
exceeded retry limit
""");

        bool matched = TaskFailureGuard.TryMatch(
            """
            ■ exceeded retry limit, last status: 429 Too Many Requests, request id:
            9ebfcdd61d547eb9-LAX
            """,
            keywords,
            out string matchedKeyword);

        Assert.True(matched);
        Assert.Equal("429 too many requests", matchedKeyword);
    }

    [Fact]
    public void TryMatch_ShouldDetectSelectedModelCapacityAsFailure()
    {
        IReadOnlyList<string> keywords = TaskFailureGuard.ParseKeywords(TaskAlertDefaults.FailureKeywords);

        bool matched = TaskFailureGuard.TryMatch(
            """
            Selected model is at capacity. Please try a different model.
            """,
            keywords,
            out string matchedKeyword);

        Assert.True(matched);
        Assert.Equal("Selected model is at capacity", matchedKeyword);
    }

    [Fact]
    public void TryMatch_ShouldDetectTryDifferentModelPhraseAsFailure()
    {
        IReadOnlyList<string> keywords = TaskFailureGuard.ParseKeywords(TaskAlertDefaults.FailureKeywords);

        bool matched = TaskFailureGuard.TryMatch(
            """
            Request rejected. Please try a different model.
            """,
            keywords,
            out string matchedKeyword);

        Assert.True(matched);
        Assert.Equal("Please try a different model", matchedKeyword);
    }
}
