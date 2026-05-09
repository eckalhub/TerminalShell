using TerminalShell.Interop;
using TerminalShell.Models;

namespace TerminalShell.Tests.Models;

public class TerminalSessionSnapshotTests
{
    [Fact]
    public void GetSnapshotContentBottom_ShouldPreferCursorRow_WhenCursorIsBelowWindowBottom()
    {
        NativeMethods.CONSOLE_SCREEN_BUFFER_INFO csbi = new()
        {
            dwCursorPosition = new NativeMethods.COORD(0, 120),
            srWindow = new NativeMethods.SMALL_RECT { Top = 80, Bottom = 99 }
        };

        short bottom = TerminalSession.GetSnapshotContentBottom(csbi, 500);

        Assert.Equal(120, bottom);
    }

    [Fact]
    public void GetSnapshotContentBottom_ShouldPreferWindowBottom_WhenWindowIsBelowCursor()
    {
        NativeMethods.CONSOLE_SCREEN_BUFFER_INFO csbi = new()
        {
            dwCursorPosition = new NativeMethods.COORD(0, 35),
            srWindow = new NativeMethods.SMALL_RECT { Top = 40, Bottom = 64 }
        };

        short bottom = TerminalSession.GetSnapshotContentBottom(csbi, 500);

        Assert.Equal(64, bottom);
    }
}
