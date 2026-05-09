using System.IO;
using System.Net.Sockets;
using TerminalShell.Models;
using TerminalShell.Services;

namespace TerminalShell.Tests.Services;

public class RemoteWebConsoleServiceTests
{
    [Fact]
    public void DetailTailLineCount_ShouldStayAtEightHundred()
    {
        Assert.Equal(800, RemoteWebConsoleService.DetailTailLineCount);
    }

    [Fact]
    public void BuildStartupFailureMessage_ShouldRecognizePortConflict()
    {
        AppConfig config = new()
        {
            RemoteBindAddress = "0.0.0.0",
            RemotePort = 18080
        };

        IOException exception = new(
            "listen failed",
            new SocketException((int)SocketError.AddressAlreadyInUse));

        string message = RemoteWebConsoleService.BuildStartupFailureMessage(config, exception);

        Assert.Contains("port 18080 is already in use", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildStartupFailureMessage_ShouldFallbackToBaseExceptionMessage()
    {
        AppConfig config = new()
        {
            RemoteBindAddress = "127.0.0.1",
            RemotePort = 19090
        };

        InvalidOperationException exception = new("custom startup failure");

        string message = RemoteWebConsoleService.BuildStartupFailureMessage(config, exception);

        Assert.Contains("127.0.0.1:19090", message, StringComparison.Ordinal);
        Assert.Contains("custom startup failure", message, StringComparison.Ordinal);
    }
}
