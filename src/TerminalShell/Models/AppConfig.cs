using System.Text.Json.Serialization;
using TerminalShell.Core;

namespace TerminalShell.Models;

public class AppConfig
{
    public const string DefaultWaitForUserInputHighlightColor = "#2D2D30";
    public const string OldDefaultInputWatermarkFormat = "[{TerminalName}] Auto-resizing input. Double enter to submit!";
    public const string PreviousDefaultInputWatermarkFormat = "[{TerminalVoiceName_or_TerminalName}] Auto-resizing input. Double enter to submit!";
    public const string DefaultInputWatermarkFormat = "[{TerminalVoiceName_or_TerminalName}] a beautiful day";
    public const string DefaultTaskCompletionTtsTemplate = "Mission completed. {TerminalVoiceName_or_TerminalName}";
    public const string DefaultTaskFailureTtsTemplate = "Hi,Master,Mission failed. {TerminalVoiceName_or_TerminalName}. {FailureKeyword}";
    public const string DefaultAllTerminalsIdleTemplate = "Hi brother , All terminal tasks have been completed.";

    // Window Position
    [JsonPropertyName("windowLeft")]
    public double WindowLeft { get; set; } = 100;

    [JsonPropertyName("windowTop")]
    public double WindowTop { get; set; } = 100;

    // Window Size (Default: 2980 * 1300)
    [JsonPropertyName("windowWidth")]
    public double WindowWidth { get; set; } = 2980;

    [JsonPropertyName("windowHeight")]
    public double WindowHeight { get; set; } = 1300;

    // Window Topmost State
    [JsonPropertyName("isTopmost")]
    public bool IsTopmost { get; set; } = false;

    [JsonPropertyName("minimizeMainWindowToTray")]
    public bool MinimizeMainWindowToTray { get; set; } = false;

    [JsonPropertyName("closeMainWindowToTrayInsteadOfExit")]
    public bool CloseMainWindowToTrayInsteadOfExit { get; set; } = false;

    [JsonPropertyName("startMinimizedWhenLaunchedAtStartup")]
    public bool StartMinimizedWhenLaunchedAtStartup { get; set; } = false;

    [JsonPropertyName("gridRows")]
    public int GridRows { get; set; } = 1;

    [JsonPropertyName("inputFontSize")]
    public double InputFontSize { get; set; } = 21.0;

    [JsonPropertyName("inputFontFamily")]
    public string InputFontFamily { get; set; } = "Arial";

    [JsonPropertyName("inputMinHeight")]
    public double InputMinHeight { get; set; } = 100.0;

    [JsonPropertyName("inputMaxHeightPercent")]
    public double InputMaxHeightPercent { get; set; } = 66.0;

    [JsonPropertyName("settingsWindowWidth")]
    public double SettingsWindowWidth { get; set; } = 1800.0;

    [JsonPropertyName("settingsWindowHeight")]
    public double SettingsWindowHeight { get; set; } = 1300.0;

    [JsonPropertyName("settingsNavWidth")]
    public double SettingsNavWidth { get; set; } = 250.0;

    [JsonPropertyName("historyWindowWidth")]
    public double HistoryWindowWidth { get; set; } = HistoryWindowLayout.DefaultWindowWidth;

    [JsonPropertyName("historyWindowHeight")]
    public double HistoryWindowHeight { get; set; } = HistoryWindowLayout.DefaultWindowHeight;

    [JsonPropertyName("historyWindowLeftPaneWidth")]
    public double HistoryWindowLeftPaneWidth { get; set; } = HistoryWindowLayout.DefaultLeftPaneWidth;

    [JsonPropertyName("inputWatermarkFormat")]
    public string InputWatermarkFormat { get; set; } = DefaultInputWatermarkFormat;

    // Clipboard Conversion
    [JsonPropertyName("clipboardConversionEnabled")]
    public bool ClipboardConversionEnabled { get; set; } = true;

    [JsonPropertyName("clipboardOutputFormat")]
    public string ClipboardOutputFormat { get; set; } = "markdown";

    [JsonPropertyName("clipboardImageDirectory")]
    public string ClipboardImageDirectory { get; set; } = @"c:\img";

    [JsonPropertyName("clipboardImageFormat")]
    public string ClipboardImageFormat { get; set; } = "Image attached [{fullpath}{time}{-XXX}.{ext}]";

    // Terminal History System
    [JsonPropertyName("historyRetentionCount")]
    public int HistoryRetentionCount { get; set; } = 1000;

    [JsonPropertyName("timeZoneOffset")]
    public int TimeZoneOffset { get; set; } = 8;

    [JsonPropertyName("historyFileNameMaxLength")]
    public int HistoryFileNameMaxLength { get; set; } = 39;

    [JsonPropertyName("terminalInputDelayMs")]
    public int TerminalInputDelayMs { get; set; } = 500;

    [JsonPropertyName("submitTriggerMode")]
    public string SubmitTriggerMode { get; set; } = "SingleEnter";

    // Custom Commands
    [JsonPropertyName("customCommandMaxDisplayLength")]
    public int CustomCommandMaxDisplayLength { get; set; } = 100;

    [JsonPropertyName("customCommandsString")]
    public string CustomCommandsString { get; set; } = CustomCommandDefaults.Template;

    [JsonPropertyName("defaultStartupCommand")]
    public string DefaultStartupCommand { get; set; } = "";

    [JsonPropertyName("enableAllTaskAlerts")]
    public bool EnableAllTaskAlerts { get; set; } = true;

    [JsonPropertyName("enableRemoteWebConsole")]
    public bool EnableRemoteWebConsole { get; set; } = false;

    [JsonPropertyName("remoteProtocolMode")]
    public string RemoteProtocolMode { get; set; } = "HTTP";

    [JsonPropertyName("remoteBindAddress")]
    public string RemoteBindAddress { get; set; } = "0.0.0.0";

    [JsonPropertyName("remotePort")]
    public int RemotePort { get; set; } = 18080;

    [JsonPropertyName("remotePasswordHash")]
    public string RemotePasswordHash { get; set; } = string.Empty;

    [JsonPropertyName("remoteSessionTimeoutMinutes")]
    public int RemoteSessionTimeoutMinutes { get; set; } = 720;

    [JsonPropertyName("remoteCookieLifetimeDays")]
    public int RemoteCookieLifetimeDays { get; set; } = 30;

    [JsonPropertyName("remoteMaxLoginAttempts")]
    public int RemoteMaxLoginAttempts { get; set; } = 5;

    [JsonPropertyName("remoteLoginLockoutMinutes")]
    public int RemoteLoginLockoutMinutes { get; set; } = 15;

    [JsonPropertyName("remoteCertificatePath")]
    public string RemoteCertificatePath { get; set; } = string.Empty;

    [JsonPropertyName("remoteCertificatePassword")]
    public string RemoteCertificatePassword { get; set; } = string.Empty;

    [JsonPropertyName("enableTaskCompletionTts")]
    public bool EnableTaskCompletionTts { get; set; } = true;

    [JsonPropertyName("taskCompletionTtsTemplate")]
    public string TaskCompletionTtsTemplate { get; set; } = DefaultTaskCompletionTtsTemplate;

    [JsonPropertyName("enableTaskFailureTts")]
    public bool EnableTaskFailureTts { get; set; } = true;

    [JsonPropertyName("taskFailureTtsTemplate")]
    public string TaskFailureTtsTemplate { get; set; } = DefaultTaskFailureTtsTemplate;

    [JsonPropertyName("taskCompletionCheckIntervalSeconds")]
    public int TaskCompletionCheckIntervalSeconds { get; set; } = 60;

    [JsonPropertyName("taskCompletionStableThresholdMinutes")]
    public int TaskCompletionStableThresholdMinutes { get; set; } = 2;

    [JsonPropertyName("taskCompletionTailLineCount")]
    public int TaskCompletionTailLineCount { get; set; } = 20;

    [JsonPropertyName("enableWaitForUserInputGuard")]
    public bool EnableWaitForUserInputGuard { get; set; } = true;

    [JsonPropertyName("mainWindowTheme")]
    public MainWindowThemeConfig MainWindowTheme { get; set; } = new();

    [JsonPropertyName("customColors")]
    public List<string> CustomColors { get; set; } = ColorPaletteHelper.CreateDefaultPalette();

    [JsonPropertyName("waitForUserInputKeywords")]
    public string WaitForUserInputKeywords { get; set; } = TaskAlertDefaults.WaitForUserInputKeywords;

    [JsonPropertyName("taskFailureKeywords")]
    public string TaskFailureKeywords { get; set; } = TaskAlertDefaults.FailureKeywords;

    [JsonPropertyName("ttsVoiceName")]
    public string TtsVoiceName { get; set; } = "";

    [JsonPropertyName("ttsRate")]
    public int TtsRate { get; set; } = 0;

    [JsonPropertyName("ttsVolume")]
    public int TtsVolume { get; set; } = 100;

    [JsonPropertyName("enableAllTerminalsIdleVoiceAlert")]
    public bool EnableAllTerminalsIdleVoiceAlert { get; set; } = true;

    [JsonPropertyName("enableAllTerminalsIdlePopupAlert")]
    public bool EnableAllTerminalsIdlePopupAlert { get; set; } = true;

    [JsonPropertyName("allTerminalsIdleTemplate")]
    public string AllTerminalsIdleTemplate { get; set; } = DefaultAllTerminalsIdleTemplate;

    [JsonPropertyName("allTerminalsIdleThresholdMinutes")]
    public int AllTerminalsIdleThresholdMinutes { get; set; } = 5;

    [JsonPropertyName("allTerminalsIdleVoiceRepeatMinutes")]
    public int AllTerminalsIdleVoiceRepeatMinutes { get; set; } = 30;

    // Auto Backup System
    [JsonPropertyName("enableAutoBackup")]
    public bool EnableAutoBackup { get; set; } = true;

    [JsonPropertyName("maxBackupsRetained")]
    public int MaxBackupsRetained { get; set; } = 100;

    [JsonPropertyName("backupIntervalMinutes")]
    public int BackupIntervalMinutes { get; set; } = 1;

    // Terminal Configurations
    [JsonPropertyName("terminals")]
    public List<TerminalConfig> Terminals { get; set; } = new();

    // Group Directories Order
    [JsonPropertyName("groupNameList")]
    public List<string> GroupNameList { get; set; } = new();

    // Group Folded State 
    [JsonPropertyName("collapsedGroups")]
    public List<string> CollapsedGroups { get; set; } = new();
}
