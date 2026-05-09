using TerminalShell.Services;

namespace TerminalShell.Tests.Services;

public class SingleInstanceServiceTests
{
    [Fact]
    public void NormalizeInstanceScope_ShouldUseFolderPath_RegardlessOfExeFileName()
    {
        string scopeA = SingleInstanceService.NormalizeInstanceScope(@"D:\apps\terminal-a\TerminalShell.exe", "TerminalShell");
        string scopeB = SingleInstanceService.NormalizeInstanceScope(@"D:\apps\terminal-a\RenamedShell.exe", "TerminalShell");

        Assert.Equal(scopeA, scopeB);
    }

    [Fact]
    public void NormalizeInstanceScope_ShouldDifferentiateDifferentFolders()
    {
        string scopeA = SingleInstanceService.NormalizeInstanceScope(@"D:\apps\terminal-a\TerminalShell.exe", "TerminalShell");
        string scopeB = SingleInstanceService.NormalizeInstanceScope(@"D:\apps\terminal-b\TerminalShell.exe", "TerminalShell");

        Assert.NotEqual(scopeA, scopeB);
    }

    [Fact]
    public void BuildMutexName_ShouldBeStableForSameScope()
    {
        string mutexA = SingleInstanceService.BuildMutexName(@"d:\apps\terminal-a", "TerminalShell");
        string mutexB = SingleInstanceService.BuildMutexName(@"d:\apps\terminal-a", "TerminalShell");

        Assert.Equal(mutexA, mutexB);
    }
}
