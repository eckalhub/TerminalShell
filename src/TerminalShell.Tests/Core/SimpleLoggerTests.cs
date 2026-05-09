using System;
using TerminalShell.Core;

namespace TerminalShell.Tests.Core;

public class SimpleLoggerTests
{
    [Fact]
    public void BuildLogEntry_ShouldIncludeMessage()
    {
        string entry = SimpleLogger.BuildLogEntry("hello");

        Assert.Contains("hello", entry, StringComparison.Ordinal);
        Assert.EndsWith(Environment.NewLine, entry, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildErrorEntry_ShouldIncludeContextAndException()
    {
        InvalidOperationException exception = new("boom");

        string entry = SimpleLogger.BuildErrorEntry(exception, "DispatcherUnhandledException");

        Assert.Contains("DispatcherUnhandledException", entry, StringComparison.Ordinal);
        Assert.Contains("boom", entry, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", entry, StringComparison.Ordinal);
        Assert.Contains("[ERROR]", entry, StringComparison.Ordinal);
    }
}
