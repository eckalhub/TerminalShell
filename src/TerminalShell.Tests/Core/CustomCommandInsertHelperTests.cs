using TerminalShell.Core;

namespace TerminalShell.Tests.Core;

public class CustomCommandInsertHelperTests
{
    [Fact]
    public void InsertMacroAtCaret_ShouldInsertReturnTokenAtCaret()
    {
        CustomCommandInsertResult result = CustomCommandInsertHelper.InsertMacroAtCaret(
            "abc",
            1,
            "[return]",
            "\n");

        Assert.Equal("a[return]bc", result.Text);
        Assert.Equal(9, result.CaretIndex);
    }

    [Fact]
    public void InsertMacroAtCaret_ShouldInsertFBlockAndPlaceCaretInsideBlock()
    {
        CustomCommandInsertResult result = CustomCommandInsertHelper.InsertMacroAtCaret(
            "abc",
            1,
            "F3",
            "\n");

        Assert.Equal("a[F3]\n\n[/F3]bc", result.Text);
        Assert.Equal(6, result.CaretIndex);
    }

    [Fact]
    public void InsertMacroAtCaret_ShouldAcceptBracketedFKeyLabels()
    {
        CustomCommandInsertResult result = CustomCommandInsertHelper.InsertMacroAtCaret(
            string.Empty,
            0,
            "[F12]",
            "\n");

        Assert.Equal("[F12]\n\n[/F12]", result.Text);
        Assert.Equal(6, result.CaretIndex);
    }

    [Fact]
    public void InsertMacroAtCaret_ShouldClampCaretIndexToTextBounds()
    {
        CustomCommandInsertResult result = CustomCommandInsertHelper.InsertMacroAtCaret(
            "abc",
            99,
            "F1",
            "\n");

        Assert.Equal("abc[F1]\n\n[/F1]", result.Text);
        Assert.Equal(8, result.CaretIndex);
    }
}
