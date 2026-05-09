using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TerminalShell.Core;

public static class StartupManager
{
    internal const string StartupLaunchArgument = "--startup-launch";
    private static string ShortcutPath => RuntimeAppIdentity.StartupShortcutPath;

    public static bool IsStartupEnabled()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(ShortcutPath))
        {
            return false;
        }

        return TryGetShortcutTargetPath(ShortcutPath, out string? targetPath)
            && ArePathsEquivalent(targetPath, exePath);
    }

    public static void SetStartup(bool enable)
    {
        if (enable)
        {
            if (!IsStartupEnabled())
            {
                CreateShortcut();
            }

            return;
        }

        if (IsStartupEnabled())
        {
            File.Delete(ShortcutPath);
        }
    }

    public static void RefreshStartupShortcut()
    {
        if (!IsStartupEnabled())
        {
            return;
        }

        CreateShortcut();
    }

    internal static bool IsStartupLaunch(IEnumerable<string>? args)
    {
        if (args == null)
        {
            return false;
        }

        foreach (string? arg in args)
        {
            if (string.Equals(arg?.Trim(), StartupLaunchArgument, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void CreateShortcut()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return;
        }

        string workingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
        string escapedShortcutPath = EscapeForSingleQuotedPowerShellString(ShortcutPath);
        string escapedExePath = EscapeForSingleQuotedPowerShellString(exePath);
        string escapedWorkingDirectory = EscapeForSingleQuotedPowerShellString(workingDirectory);
        string escapedArguments = EscapeForSingleQuotedPowerShellString(StartupLaunchArgument);
        string script = $"$s=(New-Object -COM WScript.Shell).CreateShortcut('{escapedShortcutPath}');$s.TargetPath='{escapedExePath}';$s.Arguments='{escapedArguments}';$s.WorkingDirectory='{escapedWorkingDirectory}';$s.Save()";

        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process.Start(psi)?.WaitForExit();
    }

    internal static bool ArePathsEquivalent(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        try
        {
            string normalizedLeft = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedRight = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetShortcutTargetPath(string shortcutPath, out string? targetPath)
    {
        targetPath = null;

        if (string.IsNullOrWhiteSpace(shortcutPath) || !File.Exists(shortcutPath))
        {
            return false;
        }

        string escapedShortcutPath = EscapeForSingleQuotedPowerShellString(shortcutPath);
        string script = $"$s=(New-Object -COM WScript.Shell).CreateShortcut('{escapedShortcutPath}');if($null -ne $s.TargetPath){{Write-Output $s.TargetPath}}";

        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -NonInteractive -Command \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process? process = Process.Start(psi);
        if (process == null)
        {
            return false;
        }

        string output = process.StandardOutput.ReadToEnd().Trim();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
        {
            return false;
        }

        targetPath = output;
        return true;
    }

    private static string EscapeForSingleQuotedPowerShellString(string value)
    {
        return (value ?? string.Empty).Replace("'", "''", StringComparison.Ordinal);
    }
}
