using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfApplication = System.Windows.Application;

namespace TerminalShell.Core;

public static class RuntimeAppIdentity
{
    private static readonly Lazy<string> DisplayNameBaseValue = new(ResolveDisplayNameBase);
    private static readonly Lazy<string> VersionTextValue = new(ResolveVersionText);
    private static readonly Lazy<string> ExternalIconPathValue = new(ResolveExternalIconPath);
    private static readonly Lazy<string> StartupShortcutNameValue = new(ResolveStartupShortcutName);
    private static readonly Lazy<string> StartupShortcutPathValue = new(ResolveStartupShortcutPath);
    private static readonly Lazy<ImageSource?> WindowIconValue = new(LoadWindowIconCore);
    private static readonly Lazy<Icon> TrayIconValue = new(LoadTrayIconCore);

    public static string DisplayNameBase => DisplayNameBaseValue.Value;
    public static string VersionText => VersionTextValue.Value;
    public static string BaseWindowTitle => BuildBaseWindowTitle(DisplayNameBase, VersionText);
    public static string TrayTooltipText => BuildTrayTooltipText(BaseWindowTitle);
    public static string TrayRestartMenuText => BuildTrayRestartMenuText(DisplayNameBase);
    public static string ExternalIconPath => ExternalIconPathValue.Value;
    public static string StartupShortcutName => StartupShortcutNameValue.Value;
    public static string StartupShortcutPath => StartupShortcutPathValue.Value;
    public static ImageSource? WindowIcon => WindowIconValue.Value;

    public static Icon CreateTrayIcon()
    {
        return (Icon)TrayIconValue.Value.Clone();
    }

    public static void ApplyWindowIcon(Window window)
    {
        if (window == null || WindowIcon == null)
        {
            return;
        }

        window.Icon = WindowIcon;
    }

    internal static string BuildBaseWindowTitle(string displayNameBase, string versionText)
    {
        string normalizedDisplayName = string.IsNullOrWhiteSpace(displayNameBase)
            ? "TerminalShell"
            : displayNameBase.Trim();
        string normalizedVersion = string.IsNullOrWhiteSpace(versionText)
            ? "1.00"
            : versionText.Trim();
        return $"{normalizedDisplayName} v{normalizedVersion}";
    }

    internal static string BuildTrayTooltipText(string baseWindowTitle)
    {
        return string.IsNullOrWhiteSpace(baseWindowTitle)
            ? BuildBaseWindowTitle("TerminalShell", VersionText)
            : baseWindowTitle.Trim();
    }

    internal static string BuildTrayRestartMenuText(string displayNameBase)
    {
        string normalizedDisplayName = string.IsNullOrWhiteSpace(displayNameBase)
            ? "TerminalShell"
            : displayNameBase.Trim();
        return $"Restart {normalizedDisplayName}";
    }

    internal static string BuildExternalIconPath(string processPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return string.Empty;
        }

        string? directory = Path.GetDirectoryName(processPath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(processPath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return string.Empty;
        }

        return Path.Combine(directory, $"{fileNameWithoutExtension}.ico");
    }

    internal static string BuildStartupShortcutName(string displayNameBase)
    {
        string normalizedDisplayName = string.IsNullOrWhiteSpace(displayNameBase)
            ? "TerminalShell"
            : displayNameBase.Trim();
        return $"{normalizedDisplayName}.lnk";
    }

    internal static string BuildStartupShortcutPath(string startupFolder, string displayNameBase)
    {
        if (string.IsNullOrWhiteSpace(startupFolder))
        {
            return BuildStartupShortcutName(displayNameBase);
        }

        return Path.Combine(startupFolder, BuildStartupShortcutName(displayNameBase));
    }

    private static string ResolveDisplayNameBase()
    {
        string processPath = GetProcessPath();
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(processPath);
        if (!string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return fileNameWithoutExtension;
        }

        return Assembly.GetExecutingAssembly().GetName().Name ?? "TerminalShell";
    }

    private static string ResolveVersionText()
    {
        return FormatVersionText(Assembly.GetExecutingAssembly().GetName().Version);
    }

    internal static string FormatVersionText(Version? version)
    {
        if (version == null || version.Major < 0 || version.Minor < 0)
        {
            return "1.00";
        }

        return $"{version.Major}.{version.Minor:00}";
    }

    private static string ResolveExternalIconPath()
    {
        return BuildExternalIconPath(GetProcessPath());
    }

    private static string ResolveStartupShortcutName()
    {
        return BuildStartupShortcutName(DisplayNameBase);
    }

    private static string ResolveStartupShortcutPath()
    {
        string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        return BuildStartupShortcutPath(startupFolder, DisplayNameBase);
    }

    private static ImageSource? LoadWindowIconCore()
    {
        if (TryLoadExternalWindowIcon(out ImageSource? externalIcon))
        {
            return externalIcon;
        }

        return LoadEmbeddedWindowIcon();
    }

    private static Icon LoadTrayIconCore()
    {
        if (TryLoadExternalTrayIcon(out Icon? externalIcon))
        {
            return externalIcon!;
        }

        return LoadEmbeddedTrayIcon();
    }

    private static bool TryLoadExternalWindowIcon(out ImageSource? icon)
    {
        icon = null;

        string externalIconPath = ExternalIconPath;
        if (string.IsNullOrWhiteSpace(externalIconPath) || !File.Exists(externalIconPath))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(externalIconPath);
            BitmapFrame frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            frame.Freeze();
            icon = frame;
            return true;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, $"[APP_ICON] Failed to load external window icon: {externalIconPath}");
            return false;
        }
    }

    private static bool TryLoadExternalTrayIcon(out Icon? icon)
    {
        icon = null;

        string externalIconPath = ExternalIconPath;
        if (string.IsNullOrWhiteSpace(externalIconPath) || !File.Exists(externalIconPath))
        {
            return false;
        }

        try
        {
            using Icon sourceIcon = new(externalIconPath);
            icon = (Icon)sourceIcon.Clone();
            return true;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, $"[APP_ICON] Failed to load external tray icon: {externalIconPath}");
            return false;
        }
    }

    private static ImageSource? LoadEmbeddedWindowIcon()
    {
        try
        {
            Uri resourceUri = new("pack://application:,,,/logo.ico", UriKind.Absolute);
            var streamInfo = WpfApplication.GetResourceStream(resourceUri);
            if (streamInfo == null)
            {
                return null;
            }

            using Stream stream = streamInfo.Stream;
            BitmapFrame frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            frame.Freeze();
            return frame;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "[APP_ICON] Failed to load embedded window icon.");
            return null;
        }
    }

    private static Icon LoadEmbeddedTrayIcon()
    {
        try
        {
            Uri resourceUri = new("pack://application:,,,/logo.ico", UriKind.Absolute);
            var streamInfo = WpfApplication.GetResourceStream(resourceUri);
            if (streamInfo != null)
            {
                using Stream stream = streamInfo.Stream;
                using Icon sourceIcon = new(stream);
                return (Icon)sourceIcon.Clone();
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "[APP_ICON] Failed to load embedded tray icon.");
        }

        return SystemIcons.Application;
    }

    private static string GetProcessPath()
    {
        try
        {
            return Process.GetCurrentProcess().MainModule?.FileName
                ?? Environment.ProcessPath
                ?? string.Empty;
        }
        catch
        {
            return Environment.ProcessPath ?? string.Empty;
        }
    }
}
