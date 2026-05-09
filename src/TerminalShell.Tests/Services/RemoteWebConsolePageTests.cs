using TerminalShell.Services;

namespace TerminalShell.Tests.Services;

public class RemoteWebConsolePageTests
{
    [Fact]
    public void Html_ShouldDisablePullToRefreshAndIncludeFullscreenOutputViewer()
    {
        Assert.Contains("overscroll-behavior-y: none;", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("id=\"outputFullscreen\"", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("id=\"fullscreenTerminalOutput\"", RemoteWebConsolePage.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_ShouldTrackHorizontalSwipesWithTouchMove()
    {
        Assert.Contains("swipeSurface.addEventListener('touchmove', handleSwipeTouchMove", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("function handleSwipeTouchMove(event)", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("openOutputFullscreen()", RemoteWebConsolePage.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_ShouldShowFooterCreditAndConfirmBeforeLogout()
    {
        Assert.Contains(">Power by TerminalShell<", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("Logout from TerminalShell Remote?", RemoteWebConsolePage.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_ShouldUseCssOnlyFullscreenOnPhoneAndKeepBrowserApiForDesktop()
    {
        Assert.Contains("function requestBrowserFullscreen(element)", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("function shouldUseBrowserFullscreen()", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("function isPhoneSizedViewport()", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("function isTouchCapableDevice()", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("if (shouldUseBrowserFullscreen()) {", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("requestBrowserFullscreen(outputFullscreen);", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("height: 100dvh;", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 759px)", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains(".output { min-height: 220px; max-height: 38vh; }", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains(".fullscreen-output {", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("max-height: none;", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("overflow: hidden;", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("padding: max(4px, env(safe-area-inset-top, 0px))", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains(".fullscreen-close {", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("class=\"fullscreen-eyebrow\">Fullscreen Output</div>", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("font-size: 9px;", RemoteWebConsolePage.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_ShouldIncludeFullscreenPageButtonsAndPageStepScrollLogic()
    {
        Assert.Contains("id=\"fullscreenPageUpButton\"", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("id=\"fullscreenPageDownButton\"", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("function scrollFullscreenByPage(direction)", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("fullscreenTerminalOutput.clientHeight * 0.85", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("Math.max(Math.round(fullscreenTerminalOutput.clientHeight * 0.85), 120)", RemoteWebConsolePage.Html, StringComparison.Ordinal);
    }

    [Fact]
    public void Html_ShouldIncludeFooterTerminalSwitchButtons()
    {
        Assert.Contains("id=\"prevTerminalButton\"", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("id=\"nextTerminalButton\"", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("class=\"footer-credit-text\">Power by TerminalShell</div>", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("prevTerminalButton.addEventListener('click', () => selectRelativeSession(-1));", RemoteWebConsolePage.Html, StringComparison.Ordinal);
        Assert.Contains("nextTerminalButton.addEventListener('click', () => selectRelativeSession(1));", RemoteWebConsolePage.Html, StringComparison.Ordinal);

        int switchRowIndex = RemoteWebConsolePage.Html.IndexOf("class=\"footer-credit\"", StringComparison.Ordinal);
        int draftPanelIndex = RemoteWebConsolePage.Html.IndexOf("id=\"draftPanel\"", StringComparison.Ordinal);
        Assert.True(switchRowIndex >= 0 && draftPanelIndex >= 0 && switchRowIndex < draftPanelIndex);
    }
}
