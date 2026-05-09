using TerminalShell.Models;
using Xunit;

namespace TerminalShell.Tests.Models;

public class AppConfigDefaultsTests
{
    [Fact]
    public void Defaults_ShouldEnableTaskAlertsButKeepRemoteConsoleOff()
    {
        AppConfig config = new();

        Assert.Equal("[{TerminalVoiceName_or_TerminalName}] a beautiful day", config.InputWatermarkFormat);
        Assert.True(config.EnableAllTaskAlerts);
        Assert.True(config.EnableTaskCompletionTts);
        Assert.True(config.EnableTaskFailureTts);
        Assert.True(config.EnableWaitForUserInputGuard);
        Assert.True(config.EnableAllTerminalsIdleVoiceAlert);
        Assert.True(config.EnableAllTerminalsIdlePopupAlert);
        Assert.False(config.EnableRemoteWebConsole);
    }
}
