using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using TerminalShell.Core;
using TerminalShell.Models;

namespace TerminalShell.Core.Config;

public interface IConfigManager
{
    AppConfig Config { get; }
    Task InitializeAsync();
    void Save();
}

// Skill: config_json_style (Config Manager Hub)
public class ConfigManager : IConfigManager
{
    private static ConfigManager? _instance;
    public static ConfigManager Instance => _instance ??= new ConfigManager();

    public AppConfig Config { get; private set; } = new AppConfig();
    
    private readonly string _configPath;
    private BackupManager? _backupManager;
    private bool _isInitialized = false;

    private ConfigManager()
    {
        _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        _backupManager = new BackupManager(_configPath);
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        await LoadAsync();
        _isInitialized = true;
    }

    // Config Manager Legacy Sync Load (Deprecated but kept for compatibility if needed)
    public void Load()
    {
        // Redirect to async wait if called synchronously (not recommended but fallback)
        InitializeAsync().GetAwaiter().GetResult();
    }

    public async Task LoadAsync()
    {
        try
        {
            // Level 1 Defense: Physical Layer (Async Retry + Encoding)
            string json = await TryReadFileAsync(_configPath);

            if (string.IsNullOrWhiteSpace(json))
            {
                // File missing or empty -> Default
                Config = new AppConfig();
                EnsureDefaultTerminal();
                return;
            }

            // Level 2 Defense: Syntax Layer (Regex Salvage)
            string? legacyWaitForUserInputHighlightColor = null;
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;
                    legacyWaitForUserInputHighlightColor = LoadFieldByField(root);
                }
            }
            catch (JsonException ex)
            {
                SimpleLogger.LogError(ex, "Config JSON broken. Entering Salvage Mode.");
                var salvagedConfig = SalvageWithRegex(json);
                Config = salvagedConfig; // Apply salvaged values
                
                // Force save to fix corruption
                Save();
            }

            EnsureDefaultTerminal();
            bool changed = EnsureDefaultInputWatermarkFormat();
            if (TryNormalizeCustomColors(Config))
            {
                changed = true;
            }

            if (TryNormalizeMainWindowTheme(Config, legacyWaitForUserInputHighlightColor))
            {
                changed = true;
            }

            if (EnsureTerminalDraftStorageKeys())
            {
                changed = true;
            }

            if (EnsureTerminalHistorySaveFolders())
            {
                changed = true;
            }

            if (changed)
            {
                Save();
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ConfigManager.LoadAsync Fatal Error");
            // Final Fallback: Keep default Config
        }
    }

    private async Task<string> TryReadFileAsync(string path)
    {
        if (!File.Exists(path)) return string.Empty;

        for (int i = 0; i < 3; i++)
        {
            try
            {
                // Try UTF-8 first
                return await File.ReadAllTextAsync(path, Encoding.UTF8);
            }
            catch (IOException)
            {
                await Task.Delay(100); // Retry delay
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, $"Read config failed attempt {i+1}");
                break; 
            }
        }
        return string.Empty;
    }

    private AppConfig SalvageWithRegex(string json)
    {
        var config = new AppConfig();
        // Simple regex to extract key-values logic could go here
        // For brevity and robustness, we might just return default if regex fails
        // Implementing basic Key-Value extraction:
        try 
        {
            // Regex for numbers
            // "windowLeft": 123
            ExtractDouble(json, "windowLeft", v => config.WindowLeft = v);
            ExtractDouble(json, "windowTop", v => config.WindowTop = v);
            ExtractDouble(json, "windowWidth", v => config.WindowWidth = v);
            ExtractDouble(json, "windowHeight", v => config.WindowHeight = v);
            ExtractInt(json, "gridRows", v => config.GridRows = v);
            // ... add others as needed
        }
        catch (Exception ex) { SimpleLogger.LogError(ex, "SalvageWithRegex Error"); }
        
        return config;
    }

    private void ExtractDouble(string json, string key, Action<double> setter)
    {
        var match = Regex.Match(json, $"\"{key}\"\\s*:\\s*([0-9\\.]+)");
        if (match.Success && double.TryParse(match.Groups[1].Value, out double val))
        {
            setter(val);
        }
    }

    private void ExtractInt(string json, string key, Action<int> setter)
    {
        var match = Regex.Match(json, $"\"{key}\"\\s*:\\s*([0-9]+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int val))
        {
            setter(val);
        }
    }

    /// <summary>
    /// 当终端列表为空时，自动插入一个默认终端配置。
    /// </summary>
    private void EnsureDefaultTerminal()
    {
        if (Config.Terminals == null || Config.Terminals.Count == 0)
        {
            Config.Terminals ??= new List<TerminalConfig>();
            Config.Terminals.Add(new TerminalConfig
            {
                Name = "Claude Code Example",
                DraftStorageKey = TerminalConfig.CreateDraftStorageKey(),
                TerminalVoiceName = "",
                WorkingDirectory = @"c:\cc_example",
                StartupCommand = "claude",
                ShowInMainWindow = true
            });
            Save();
            // SimpleLogger.Log("No terminals configured. Added default 'Claude Code Example' terminal.");
        }
    }

    private bool EnsureTerminalDraftStorageKeys()
    {
        if (Config.Terminals == null || Config.Terminals.Count == 0)
        {
            return false;
        }

        bool changed = false;
        foreach (TerminalConfig terminal in Config.Terminals)
        {
            if (!string.IsNullOrWhiteSpace(terminal.DraftStorageKey))
            {
                continue;
            }

            terminal.DraftStorageKey = TerminalConfig.CreateDraftStorageKey();
            changed = true;
        }

        return changed;
    }

    private bool EnsureTerminalHistorySaveFolders()
    {
        if (Config.Terminals == null || Config.Terminals.Count == 0)
        {
            return false;
        }

        bool changed = false;
        foreach (TerminalConfig terminal in Config.Terminals)
        {
            if (!string.IsNullOrWhiteSpace(terminal.TerminalHistorySaveFolder))
            {
                continue;
            }

            terminal.TerminalHistorySaveFolder = string.IsNullOrWhiteSpace(terminal.Name) ? "Default" : terminal.Name;
            changed = true;
        }

        return changed;
    }

    public static string NormalizeInputWatermarkFormat(string? format)
    {
        if (string.IsNullOrEmpty(format))
        {
            return format ?? string.Empty;
        }

        return format.Replace("{TerminalName}", "{TerminalVoiceName_or_TerminalName}", StringComparison.OrdinalIgnoreCase)
                     .Replace("{0}", "{TerminalVoiceName_or_TerminalName}", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryMigrateDefaultInputWatermarkFormat(AppConfig config)
    {
        string current = config.InputWatermarkFormat ?? string.Empty;
        string normalized = NormalizeInputWatermarkFormat(current);

        if (string.Equals(current, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        config.InputWatermarkFormat = normalized;
        return true;
    }

    private bool EnsureDefaultInputWatermarkFormat()
    {
        return TryMigrateDefaultInputWatermarkFormat(Config);
    }

    public static bool TryNormalizeCustomColors(AppConfig config)
    {
        List<string> normalized = ColorPaletteHelper.NormalizePalette(config.CustomColors);
        if (config.CustomColors != null && config.CustomColors.SequenceEqual(normalized, StringComparer.Ordinal))
        {
            return false;
        }

        config.CustomColors = normalized;
        return true;
    }

    public static bool TryNormalizeMainWindowTheme(AppConfig config, string? legacyWaitForUserInputHighlightColor = null)
    {
        string before = JsonSerializer.Serialize(config.MainWindowTheme);
        config.MainWindowTheme = MainWindowThemeConfig.Normalize(config.MainWindowTheme, legacyWaitForUserInputHighlightColor);
        string after = JsonSerializer.Serialize(config.MainWindowTheme);
        return !string.Equals(before, after, StringComparison.Ordinal);
    }

    // Level 3 Defense: Semantic Layer (FieldParser with Clamping)
    private string? LoadFieldByField(JsonElement root)
    {
        string? legacyWaitForUserInputHighlightColor = null;

        // Enforce Bounds (Security)
        Config.WindowLeft = FieldParser.ParseDouble(root, "windowLeft", Config.WindowLeft);
        Config.WindowTop = FieldParser.ParseDouble(root, "windowTop", Config.WindowTop);
        Config.WindowWidth = FieldParser.ParseDouble(root, "windowWidth", Config.WindowWidth, 100, 10000);
        Config.WindowHeight = FieldParser.ParseDouble(root, "windowHeight", Config.WindowHeight, 100, 10000);
        
        Config.IsTopmost = FieldParser.ParseBool(root, "isTopmost", Config.IsTopmost);
        Config.MinimizeMainWindowToTray = FieldParser.ParseBool(root, "minimizeMainWindowToTray", Config.MinimizeMainWindowToTray);
        Config.CloseMainWindowToTrayInsteadOfExit = FieldParser.ParseBool(root, "closeMainWindowToTrayInsteadOfExit", Config.CloseMainWindowToTrayInsteadOfExit);
        Config.StartMinimizedWhenLaunchedAtStartup = FieldParser.ParseBool(root, "startMinimizedWhenLaunchedAtStartup", Config.StartMinimizedWhenLaunchedAtStartup);

        Config.GridRows = FieldParser.ParseInt(root, "gridRows", Config.GridRows, 1, 10); // Max 10 rows
        
        Config.InputFontSize = FieldParser.ParseDouble(root, "inputFontSize", Config.InputFontSize, 8, 72);
        Config.InputFontFamily = FieldParser.ParseString(root, "inputFontFamily", Config.InputFontFamily);
        Config.InputMinHeight = FieldParser.ParseDouble(root, "inputMinHeight", Config.InputMinHeight, 21, 500);
        Config.InputMaxHeightPercent = FieldParser.ParseDouble(root, "inputMaxHeightPercent", Config.InputMaxHeightPercent, 10, 90);
        Config.InputWatermarkFormat = FieldParser.ParseString(root, "inputWatermarkFormat", Config.InputWatermarkFormat);
        Config.DefaultStartupCommand = FieldParser.ParseString(root, "defaultStartupCommand", Config.DefaultStartupCommand);
        Config.EnableAllTaskAlerts = FieldParser.ParseBool(root, "enableAllTaskAlerts", Config.EnableAllTaskAlerts);
        Config.EnableRemoteWebConsole = FieldParser.ParseBool(root, "enableRemoteWebConsole", Config.EnableRemoteWebConsole);
        Config.RemoteProtocolMode = FieldParser.ParseString(root, "remoteProtocolMode", Config.RemoteProtocolMode);
        Config.RemoteBindAddress = FieldParser.ParseString(root, "remoteBindAddress", Config.RemoteBindAddress);
        Config.RemotePort = FieldParser.ParseInt(root, "remotePort", Config.RemotePort, 1024, 65535);
        Config.RemotePasswordHash = FieldParser.ParseString(root, "remotePasswordHash", Config.RemotePasswordHash);
        Config.RemoteSessionTimeoutMinutes = FieldParser.ParseInt(root, "remoteSessionTimeoutMinutes", Config.RemoteSessionTimeoutMinutes, 1, 10080);
        Config.RemoteCookieLifetimeDays = FieldParser.ParseInt(root, "remoteCookieLifetimeDays", Config.RemoteCookieLifetimeDays, 1, 3650);
        Config.RemoteMaxLoginAttempts = FieldParser.ParseInt(root, "remoteMaxLoginAttempts", Config.RemoteMaxLoginAttempts, 1, 20);
        Config.RemoteLoginLockoutMinutes = FieldParser.ParseInt(root, "remoteLoginLockoutMinutes", Config.RemoteLoginLockoutMinutes, 1, 1440);
        Config.RemoteCertificatePath = FieldParser.ParseString(root, "remoteCertificatePath", Config.RemoteCertificatePath);
        Config.RemoteCertificatePassword = FieldParser.ParseString(root, "remoteCertificatePassword", Config.RemoteCertificatePassword);
        Config.EnableTaskCompletionTts = FieldParser.ParseBool(root, "enableTaskCompletionTts", Config.EnableTaskCompletionTts);
        Config.TaskCompletionTtsTemplate = FieldParser.ParseString(root, "taskCompletionTtsTemplate", Config.TaskCompletionTtsTemplate);
        Config.EnableTaskFailureTts = FieldParser.ParseBool(root, "enableTaskFailureTts", Config.EnableTaskFailureTts);
        Config.TaskFailureTtsTemplate = FieldParser.ParseString(root, "taskFailureTtsTemplate", Config.TaskFailureTtsTemplate);
        int legacyTaskCompletionCheckIntervalMinutes = FieldParser.ParseInt(root, "taskCompletionCheckIntervalMinutes", -1, 1, 60);
        int defaultTaskCompletionCheckIntervalSeconds = legacyTaskCompletionCheckIntervalMinutes > 0
            ? legacyTaskCompletionCheckIntervalMinutes * 60
            : Config.TaskCompletionCheckIntervalSeconds;
        Config.TaskCompletionCheckIntervalSeconds = FieldParser.ParseInt(root, "taskCompletionCheckIntervalSeconds", defaultTaskCompletionCheckIntervalSeconds, 1, 3600);
        Config.TaskCompletionTailLineCount = FieldParser.ParseInt(root, "taskCompletionTailLineCount", Config.TaskCompletionTailLineCount, 5, 200);
        Config.EnableWaitForUserInputGuard = FieldParser.ParseBool(root, "enableWaitForUserInputGuard", Config.EnableWaitForUserInputGuard);
        legacyWaitForUserInputHighlightColor = FieldParser.ParseString(root, "waitForUserInputHighlightColor", string.Empty);
        if (root.TryGetProperty("mainWindowTheme", out JsonElement mainWindowThemeElement) && mainWindowThemeElement.ValueKind == JsonValueKind.Object)
        {
            try
            {
                MainWindowThemeConfig? mainWindowTheme = JsonSerializer.Deserialize<MainWindowThemeConfig>(mainWindowThemeElement.GetRawText());
                Config.MainWindowTheme = mainWindowTheme ?? null!;
            }
            catch
            {
                Config.MainWindowTheme = null!;
            }
        }
        else
        {
            Config.MainWindowTheme = null!;
        }
        if (root.TryGetProperty("customColors", out JsonElement customColorsElement) && customColorsElement.ValueKind == JsonValueKind.Array)
        {
            try
            {
                List<string>? customColors = JsonSerializer.Deserialize<List<string>>(customColorsElement.GetRawText());
                Config.CustomColors = ColorPaletteHelper.NormalizePalette(customColors);
            }
            catch
            {
                Config.CustomColors = ColorPaletteHelper.CreateDefaultPalette();
            }
        }

        Config.WaitForUserInputKeywords = FieldParser.ParseString(root, "waitForUserInputKeywords", Config.WaitForUserInputKeywords);
        Config.TaskFailureKeywords = FieldParser.ParseString(root, "taskFailureKeywords", Config.TaskFailureKeywords);
        Config.TtsVoiceName = FieldParser.ParseString(root, "ttsVoiceName", Config.TtsVoiceName);
        Config.TtsRate = FieldParser.ParseInt(root, "ttsRate", Config.TtsRate, -10, 10);
        Config.TtsVolume = FieldParser.ParseInt(root, "ttsVolume", Config.TtsVolume, 0, 100);
        Config.EnableAllTerminalsIdleVoiceAlert = FieldParser.ParseBool(root, "enableAllTerminalsIdleVoiceAlert", Config.EnableAllTerminalsIdleVoiceAlert);
        Config.EnableAllTerminalsIdlePopupAlert = FieldParser.ParseBool(root, "enableAllTerminalsIdlePopupAlert", Config.EnableAllTerminalsIdlePopupAlert);
        Config.AllTerminalsIdleTemplate = FieldParser.ParseString(root, "allTerminalsIdleTemplate", Config.AllTerminalsIdleTemplate);
        Config.AllTerminalsIdleThresholdMinutes = FieldParser.ParseInt(root, "allTerminalsIdleThresholdMinutes", Config.AllTerminalsIdleThresholdMinutes, 1, 1440);
        Config.AllTerminalsIdleVoiceRepeatMinutes = FieldParser.ParseInt(root, "allTerminalsIdleVoiceRepeatMinutes", Config.AllTerminalsIdleVoiceRepeatMinutes, 1, 1440);
        
        Config.SettingsWindowWidth = FieldParser.ParseDouble(root, "settingsWindowWidth", Config.SettingsWindowWidth, 400, 5000);
        Config.SettingsWindowHeight = FieldParser.ParseDouble(root, "settingsWindowHeight", Config.SettingsWindowHeight, 300, 5000);
        Config.SettingsNavWidth = FieldParser.ParseDouble(root, "settingsNavWidth", Config.SettingsNavWidth, 150, 500);
        Config.HistoryWindowWidth = FieldParser.ParseDouble(root, "historyWindowWidth", Config.HistoryWindowWidth, HistoryWindowLayout.MinWindowWidth, HistoryWindowLayout.MaxWindowWidth);
        Config.HistoryWindowHeight = FieldParser.ParseDouble(root, "historyWindowHeight", Config.HistoryWindowHeight, HistoryWindowLayout.MinWindowHeight, HistoryWindowLayout.MaxWindowHeight);
        Config.HistoryWindowLeftPaneWidth = FieldParser.ParseDouble(root, "historyWindowLeftPaneWidth", Config.HistoryWindowLeftPaneWidth, HistoryWindowLayout.MinPaneWidth, HistoryWindowLayout.MaxPaneWidth);

        Config.ClipboardConversionEnabled = FieldParser.ParseBool(root, "clipboardConversionEnabled", Config.ClipboardConversionEnabled);
        Config.ClipboardOutputFormat = FieldParser.ParseString(root, "clipboardOutputFormat", Config.ClipboardOutputFormat);
        Config.ClipboardImageDirectory = FieldParser.ParseString(root, "clipboardImageDirectory", Config.ClipboardImageDirectory);
        Config.ClipboardImageFormat = FieldParser.ParseString(root, "clipboardImageFormat", Config.ClipboardImageFormat);

        Config.TerminalInputDelayMs = FieldParser.ParseInt(root, "terminalInputDelayMs", Config.TerminalInputDelayMs, 0, 5000);
        Config.SubmitTriggerMode = FieldParser.ParseString(root, "submitTriggerMode", Config.SubmitTriggerMode);

        // Auto Backup System
        Config.EnableAutoBackup = FieldParser.ParseBool(root, "enableAutoBackup", Config.EnableAutoBackup);
        Config.MaxBackupsRetained = FieldParser.ParseInt(root, "maxBackupsRetained", Config.MaxBackupsRetained, 1, 1000);
        Config.BackupIntervalMinutes = FieldParser.ParseInt(root, "backupIntervalMinutes", Config.BackupIntervalMinutes, 1, 60);

        // Terminal History System
        Config.HistoryRetentionCount = FieldParser.ParseInt(root, "historyRetentionCount", Config.HistoryRetentionCount, 10, 100000);
        Config.TimeZoneOffset = FieldParser.ParseInt(root, "timeZoneOffset", Config.TimeZoneOffset, -12, 14);
        Config.HistoryFileNameMaxLength = FieldParser.ParseInt(root, "historyFileNameMaxLength", Config.HistoryFileNameMaxLength, 10, 200);

        // Custom Commands
        Config.CustomCommandMaxDisplayLength = FieldParser.ParseInt(root, "customCommandMaxDisplayLength", Config.CustomCommandMaxDisplayLength, 10, 5000);
        string rawCustomCommands = FieldParser.ParseString(root, "customCommandsString", Config.CustomCommandsString);
        Config.CustomCommandsString = CustomCommandDefaults.GetEffectiveValue(rawCustomCommands);

        // Load Terminals List (Deep Clone / New List)
        if (root.TryGetProperty("terminals", out var terminalsElement) && terminalsElement.ValueKind == JsonValueKind.Array)
        {
            try 
            {
                var list = JsonSerializer.Deserialize<List<TerminalConfig>>(terminalsElement.GetRawText());
                if (list != null)
                {
                    Config.Terminals = list;
                }
            }
            catch { /* Ignore list corruption, keep defaults */ }
        }

        // Load GroupNameList
        if (root.TryGetProperty("groupNameList", out var groupNameListElement) && groupNameListElement.ValueKind == JsonValueKind.Array)
        {
            try
            {
                var groups = JsonSerializer.Deserialize<List<string>>(groupNameListElement.GetRawText());
                if (groups != null)
                {
                    Config.GroupNameList = groups;
                }
            }
            catch { /* Ignore */ }
        }

        // Load CollapsedGroups
        if (root.TryGetProperty("collapsedGroups", out var collapsedGroupsElement) && collapsedGroupsElement.ValueKind == JsonValueKind.Array)
        {
            try
            {
                var collapsed = JsonSerializer.Deserialize<List<string>>(collapsedGroupsElement.GetRawText());
                if (collapsed != null)
                {
                    Config.CollapsedGroups = collapsed;
                }
            }
            catch { /* Ignore */ }
        }

        // Ensure at least 4 default terminals if empty (Legacy logic, maybe redundant with EnsureDefaultTerminal but kept)
        // EnsureDefaultTerminal logic handles main default.
        return legacyWaitForUserInputHighlightColor;
    }

    public void Save()
    {
        try
        {
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                // Allow Chinese/Unicode chars to be written as-is (not escaped as \uXXXX)
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            // Level 4 Defense: Runtime Isolation (Serialize current state)
            string json = JsonSerializer.Serialize(Config, options);
            
            // Auto Backup Trigger
            if (Config.EnableAutoBackup && _backupManager != null)
            {
                _backupManager.CheckAndBackup(json, Config.MaxBackupsRetained, Config.BackupIntervalMinutes);
            }

            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ConfigManager.Save");
        }
    }
}
