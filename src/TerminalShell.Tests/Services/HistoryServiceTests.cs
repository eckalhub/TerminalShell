using TerminalShell.Services;

namespace TerminalShell.Tests.Services;

public class HistoryServiceTests
{
    [Fact]
    public void GetSafeHistoryFolderName_ShouldPreferExplicitHistoryFolder()
    {
        string folder = HistoryService.GetSafeHistoryFolderName("生财有术", "project_生财有术");

        Assert.Equal("生财有术", folder);
    }

    [Fact]
    public void GetSafeHistoryFolderName_ShouldFallBackToTerminalName_WhenHistoryFolderIsBlank()
    {
        string folder = HistoryService.GetSafeHistoryFolderName("   ", "project_生财有术");

        Assert.Equal("project_生财有术", folder);
    }

    [Fact]
    public void GetSafeHistoryFolderName_ShouldCleanInvalidPathCharacters()
    {
        string folder = HistoryService.GetSafeHistoryFolderName(@"bad:name/path", "fallback");

        Assert.Equal("bad_name_path", folder);
    }
}
