using System;
using System.IO;
using TerminalShell.Core;
using Xunit;

namespace TerminalShell.Tests.Core;

public class StartupManagerTests
{
    [Fact]
    public void BuildStartupShortcutName_ShouldFollowExeName()
    {
        Assert.Equal("ServiceShell.lnk", RuntimeAppIdentity.BuildStartupShortcutName("ServiceShell"));
    }

    [Fact]
    public void BuildStartupShortcutPath_ShouldUseStartupFolderAndExeName()
    {
        string startupFolder = @"C:\Users\TestUser\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup";
        string expected = Path.Combine(startupFolder, "ServiceShell.lnk");

        string actual = RuntimeAppIdentity.BuildStartupShortcutPath(startupFolder, "ServiceShell");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ArePathsEquivalent_ShouldIgnoreCaseAndNormalizeSegments()
    {
        bool actual = StartupManager.ArePathsEquivalent(
            @"D:\Apps\ServiceShell.exe",
            @"d:\apps\.\ServiceShell.exe");

        Assert.True(actual);
    }

    [Fact]
    public void ArePathsEquivalent_ShouldReturnFalseForBlankPaths()
    {
        Assert.False(StartupManager.ArePathsEquivalent("", @"D:\Apps\ServiceShell.exe"));
        Assert.False(StartupManager.ArePathsEquivalent(@"D:\Apps\ServiceShell.exe", null));
    }

    [Fact]
    public void IsStartupLaunch_ShouldDetectStartupLaunchArgument()
    {
        bool actual = StartupManager.IsStartupLaunch(new[] { "--verbose", "--STARTUP-LAUNCH" });

        Assert.True(actual);
    }

    [Fact]
    public void IsStartupLaunch_ShouldReturnFalseWhenArgumentMissing()
    {
        Assert.False(StartupManager.IsStartupLaunch(new[] { "--manual" }));
        Assert.False(StartupManager.IsStartupLaunch(null));
    }

    [Fact]
    public void FormatVersionText_ShouldPadMinorToTwoDigits()
    {
        string actual = RuntimeAppIdentity.FormatVersionText(new Version(3, 6));

        Assert.Equal("3.06", actual);
    }

    [Fact]
    public void FormatVersionText_ShouldPreserveTwoDigitMinor()
    {
        string actual = RuntimeAppIdentity.FormatVersionText(new Version(3, 16));

        Assert.Equal("3.16", actual);
    }

    [Fact]
    public void TerminalShellProjectVersion_ShouldUseStrictXDotXXFormat()
    {
        string projectFilePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "TerminalShell",
            "TerminalShell.csproj"));

        string projectFileText = File.ReadAllText(projectFilePath);

        Assert.Matches("<Version>\\d+\\.\\d{2}</Version>", projectFileText);
    }
}
