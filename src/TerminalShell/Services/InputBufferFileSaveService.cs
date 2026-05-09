using System;
using System.IO;

namespace TerminalShell.Services;

public static class InputBufferFileSaveService
{
    public static string BuildDefaultFileName(int timeZoneOffset, DateTime? utcNow = null)
    {
        DateTime currentTime = (utcNow ?? DateTime.UtcNow).AddHours(timeZoneOffset);
        return $"{currentTime:yyyy-MM-dd_HHmmss}.md";
    }

    public static string ResolveInitialDirectory(string? lastInputSaveDirectory, string? workingDirectory, string? desktopDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(lastInputSaveDirectory) && Directory.Exists(lastInputSaveDirectory))
        {
            return lastInputSaveDirectory;
        }

        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
        {
            return workingDirectory;
        }

        string fallbackDesktop = string.IsNullOrWhiteSpace(desktopDirectory)
            ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            : desktopDirectory;

        if (!string.IsNullOrWhiteSpace(fallbackDesktop))
        {
            return fallbackDesktop;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }
}
