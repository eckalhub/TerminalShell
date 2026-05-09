using System.IO;
using TerminalShell.Services;

namespace TerminalShell.Tests.Services;

public class InputBufferFileSaveServiceTests
{
    [Fact]
    public void BuildDefaultFileName_ShouldUseConfiguredTimezoneOffset()
    {
        DateTime utcNow = new(2026, 4, 20, 4, 15, 4, DateTimeKind.Utc);

        string fileName = InputBufferFileSaveService.BuildDefaultFileName(8, utcNow);

        Assert.Equal("2026-04-20_121504.md", fileName);
    }

    [Fact]
    public void ResolveInitialDirectory_ShouldPreferLastSaveDirectory_WhenItExists()
    {
        string lastSaveDirectory = Path.Combine(Path.GetTempPath(), $"terminalshell-last-{Guid.NewGuid():N}");
        string workingDirectory = Path.Combine(Path.GetTempPath(), $"terminalshell-work-{Guid.NewGuid():N}");
        Directory.CreateDirectory(lastSaveDirectory);
        Directory.CreateDirectory(workingDirectory);

        try
        {
            string resolved = InputBufferFileSaveService.ResolveInitialDirectory(lastSaveDirectory, workingDirectory, @"C:\DesktopFallback");

            Assert.Equal(lastSaveDirectory, resolved);
        }
        finally
        {
            Directory.Delete(lastSaveDirectory, true);
            Directory.Delete(workingDirectory, true);
        }
    }

    [Fact]
    public void ResolveInitialDirectory_ShouldFallBackToWorkingDirectory_WhenLastSaveDirectoryIsMissing()
    {
        string workingDirectory = Path.Combine(Path.GetTempPath(), $"terminalshell-work-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workingDirectory);

        try
        {
            string resolved = InputBufferFileSaveService.ResolveInitialDirectory(@"Z:\missing-dir", workingDirectory, @"C:\DesktopFallback");

            Assert.Equal(workingDirectory, resolved);
        }
        finally
        {
            Directory.Delete(workingDirectory, true);
        }
    }

    [Fact]
    public void ResolveInitialDirectory_ShouldFallBackToDesktop_WhenOtherDirectoriesAreInvalid()
    {
        string resolved = InputBufferFileSaveService.ResolveInitialDirectory(" ", " ", @"C:\DesktopFallback");

        Assert.Equal(@"C:\DesktopFallback", resolved);
    }
}
