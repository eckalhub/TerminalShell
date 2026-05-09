using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TerminalShell.Core;
using TerminalShell.Core.Security;
using TerminalShell.Models;
using TerminalShell.Services;

namespace TerminalShell.ViewModels;

public partial class GlobalSettingsViewModel : ObservableObject
{
    private const string PreviewTerminalName = "Preview Terminal";
    private const string PreviewTerminalVoiceName = "Preview Voice";
    private const string PreviewFailureKeyword = "You've hit your usage limit";

    public string Name => "Global Settings";
    public string GroupName => "";

    [ObservableProperty]
    private int _gridRows = 1;
    public List<int> AvailableGridRows { get; } = new() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

    [ObservableProperty]
    private double _inputFontSize = 21.0;

    [ObservableProperty]
    private string _inputFontFamily = "Arial";

    public ICollection<System.Windows.Media.FontFamily> AvailableFonts => System.Windows.Media.Fonts.SystemFontFamilies;

    [ObservableProperty]
    private double _inputMinHeight = 100.0;

    [ObservableProperty]
    private double _inputMaxHeightPercent = 66.0;

    [ObservableProperty]
    private string _inputWatermarkFormat = AppConfig.DefaultInputWatermarkFormat;

    [ObservableProperty]
    private string _defaultStartupCommand = "";

    [ObservableProperty]
    private bool _enableAllTaskAlerts = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemotePasswordStatusText))]
    private bool _enableRemoteWebConsole;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemoteAccessUrl))]
    [NotifyPropertyChangedFor(nameof(RemoteAccessSourceText))]
    [NotifyPropertyChangedFor(nameof(RemoteAccessQrCodeImage))]
    private string _remoteProtocolMode = "HTTP";

    public List<string> AvailableRemoteProtocolModes { get; } = new() { "HTTP", "HTTPS" };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemoteAccessUrl))]
    [NotifyPropertyChangedFor(nameof(RemoteAccessSourceText))]
    [NotifyPropertyChangedFor(nameof(RemoteAccessQrCodeImage))]
    private string _remoteBindAddress = "0.0.0.0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemoteAccessUrl))]
    [NotifyPropertyChangedFor(nameof(RemoteAccessSourceText))]
    [NotifyPropertyChangedFor(nameof(RemoteAccessQrCodeImage))]
    private int _remotePort = 18080;

    [ObservableProperty]
    private int _remoteSessionTimeoutMinutes = 720;

    [ObservableProperty]
    private int _remoteCookieLifetimeDays = 30;

    [ObservableProperty]
    private int _remoteMaxLoginAttempts = 5;

    [ObservableProperty]
    private int _remoteLoginLockoutMinutes = 15;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RemotePasswordStatusText))]
    private bool _isRemotePasswordConfigured;

    public string RemotePasswordStatusText => IsRemotePasswordConfigured
        ? "Password status: configured"
        : "Password status: not configured";

    public string RemoteAccessUrl => RemoteAccessUrlResolver.BuildDisplayUrl(RemoteProtocolMode, RemoteBindAddress, RemotePort);
    public string RemoteAccessSourceText => RemoteAccessUrlResolver.GetDisplaySourceText(RemoteBindAddress);
    public BitmapImage? RemoteAccessQrCodeImage => RemoteAccessQrCodeRenderer.Render(RemoteAccessUrl);

    public string RemotePasswordInput { get; private set; } = string.Empty;
    public string RemotePasswordConfirmInput { get; private set; } = string.Empty;

    [ObservableProperty]
    private bool _enableTaskCompletionTts = true;

    [ObservableProperty]
    private string _taskCompletionTtsTemplate = AppConfig.DefaultTaskCompletionTtsTemplate;

    [ObservableProperty]
    private bool _enableTaskFailureTts = true;

    [ObservableProperty]
    private string _taskFailureTtsTemplate = AppConfig.DefaultTaskFailureTtsTemplate;

    [ObservableProperty]
    private int _taskCompletionCheckIntervalSeconds = 60;

    [ObservableProperty]
    private int _taskCompletionStableThresholdMinutes = 2;

    [ObservableProperty]
    private int _taskCompletionTailLineCount = 20;

    [ObservableProperty]
    private bool _enableWaitForUserInputGuard = true;

    [ObservableProperty]
    private string _waitForUserInputKeywords = TaskAlertDefaults.WaitForUserInputKeywords;

    [ObservableProperty]
    private string _taskFailureKeywords = TaskAlertDefaults.FailureKeywords;

    [ObservableProperty]
    private string _ttsVoiceName = "";

    [ObservableProperty]
    private int _ttsRate = 0;

    [ObservableProperty]
    private int _ttsVolume = 100;

    [ObservableProperty]
    private bool _enableAllTerminalsIdleVoiceAlert = true;

    [ObservableProperty]
    private bool _enableAllTerminalsIdlePopupAlert = true;

    [ObservableProperty]
    private string _allTerminalsIdleTemplate = AppConfig.DefaultAllTerminalsIdleTemplate;

    [ObservableProperty]
    private int _allTerminalsIdleThresholdMinutes = 5;

    [ObservableProperty]
    private int _allTerminalsIdleVoiceRepeatMinutes = 30;

    [ObservableProperty]
    private bool _runAtStartup;

    [ObservableProperty]
    private bool _minimizeMainWindowToTray;

    [ObservableProperty]
    private bool _closeMainWindowToTrayInsteadOfExit;

    [ObservableProperty]
    private bool _startMinimizedWhenLaunchedAtStartup;

    public List<SpeechVoiceOption> AvailableSpeechVoices { get; } = TaskCompletionSpeechService.GetInstalledVoices();

    [RelayCommand]
    private void ResetTaskCompletionTtsTemplate()
    {
        TaskCompletionTtsTemplate = AppConfig.DefaultTaskCompletionTtsTemplate;
    }

    [RelayCommand]
    private void ResetTaskFailureTtsTemplate()
    {
        TaskFailureTtsTemplate = AppConfig.DefaultTaskFailureTtsTemplate;
    }

    [RelayCommand]
    private void ResetAllTerminalsIdleTemplate()
    {
        AllTerminalsIdleTemplate = AppConfig.DefaultAllTerminalsIdleTemplate;
    }

    [RelayCommand]
    private void ResetWaitForUserInputKeywords()
    {
        WaitForUserInputKeywords = TaskAlertDefaults.WaitForUserInputKeywords;
    }

    [RelayCommand]
    private void ResetTaskFailureKeywords()
    {
        TaskFailureKeywords = TaskAlertDefaults.FailureKeywords;
    }

    [RelayCommand]
    private async Task PreviewTaskCompletionTtsAsync()
    {
        string previewText = RenderTaskCompletionPreview(TaskCompletionTtsTemplate);
        await PreviewSpeechAsync(previewText);
    }

    [RelayCommand]
    private async Task PreviewTaskFailureTtsAsync()
    {
        string previewText = RenderTaskFailurePreview(TaskFailureTtsTemplate);
        await PreviewSpeechAsync(previewText);
    }

    [RelayCommand]
    private async Task PreviewAllTerminalsIdleTemplateAsync()
    {
        string previewText = string.IsNullOrWhiteSpace(AllTerminalsIdleTemplate)
            ? AppConfig.DefaultAllTerminalsIdleTemplate
            : AllTerminalsIdleTemplate;

        await PreviewSpeechAsync(previewText);
    }

    [ObservableProperty]
    private int _terminalInputDelayMs = 500;

    [ObservableProperty]
    private string _submitTriggerMode = "SingleEnter";

    public List<string> AvailableSubmitModes { get; } = new List<string> { "DoubleEnter", "SingleEnter" };

    // Auto Backup
    [ObservableProperty]
    private bool _enableAutoBackup = true;

    [ObservableProperty]
    private int _maxBackupsRetained = 100;

    [ObservableProperty]
    private int _backupIntervalMinutes = 1;

    // Terminal History
    [ObservableProperty]
    private int _historyRetentionCount = 1000;

    [ObservableProperty]
    private int _timeZoneOffset = 8;
    
    public List<TimezoneOption> AvailableTimeZones { get; } = new List<TimezoneOption>
    {
        new TimezoneOption { Offset = -12, DisplayName = "UTC-12:00 Baker Island" },
        new TimezoneOption { Offset = -11, DisplayName = "UTC-11:00 Niue, Samoa" },
        new TimezoneOption { Offset = -10, DisplayName = "UTC-10:00 Hawaii, Honolulu" },
        new TimezoneOption { Offset = -9, DisplayName = "UTC-09:00 Alaska" },
        new TimezoneOption { Offset = -8, DisplayName = "UTC-08:00 Pacific Time (US & Canada)" },
        new TimezoneOption { Offset = -7, DisplayName = "UTC-07:00 Mountain Time (US & Canada)" },
        new TimezoneOption { Offset = -6, DisplayName = "UTC-06:00 Central Time (US & Canada)" },
        new TimezoneOption { Offset = -5, DisplayName = "UTC-05:00 Eastern Time (US & Canada), Bogota" },
        new TimezoneOption { Offset = -4, DisplayName = "UTC-04:00 Caracas, La Paz, Santiago" },
        new TimezoneOption { Offset = -3, DisplayName = "UTC-03:00 Brasilia, Buenos Aires" },
        new TimezoneOption { Offset = -2, DisplayName = "UTC-02:00 Mid-Atlantic" },
        new TimezoneOption { Offset = -1, DisplayName = "UTC-01:00 Azores, Cape Verde Is." },
        new TimezoneOption { Offset = 0, DisplayName = "UTC±00:00 London, Dublin, Lisbon (GMT)" },
        new TimezoneOption { Offset = 1, DisplayName = "UTC+01:00 Paris, Berlin, Rome, Madrid" },
        new TimezoneOption { Offset = 2, DisplayName = "UTC+02:00 Cairo, Athens, Jerusalem" },
        new TimezoneOption { Offset = 3, DisplayName = "UTC+03:00 Moscow, Istanbul, Riyadh" },
        new TimezoneOption { Offset = 4, DisplayName = "UTC+04:00 Dubai, Baku, Tbilisi" },
        new TimezoneOption { Offset = 5, DisplayName = "UTC+05:00 Karachi, Tashkent" },
        new TimezoneOption { Offset = 6, DisplayName = "UTC+06:00 Dhaka, Almaty" },
        new TimezoneOption { Offset = 7, DisplayName = "UTC+07:00 Bangkok, Hanoi, Jakarta" },
        new TimezoneOption { Offset = 8, DisplayName = "UTC+08:00 Beijing, Singapore, Hong Kong" },
        new TimezoneOption { Offset = 9, DisplayName = "UTC+09:00 Tokyo, Seoul" },
        new TimezoneOption { Offset = 10, DisplayName = "UTC+10:00 Sydney, Melbourne, Guam" },
        new TimezoneOption { Offset = 11, DisplayName = "UTC+11:00 Solomon Is., New Caledonia" },
        new TimezoneOption { Offset = 12, DisplayName = "UTC+12:00 Fiji, Auckland" },
        new TimezoneOption { Offset = 13, DisplayName = "UTC+13:00 Nuku'alofa, Samoa" },
        new TimezoneOption { Offset = 14, DisplayName = "UTC+14:00 Kiritimati (Line Islands)" }
    };

    [ObservableProperty]
    private int _historyFileNameMaxLength = 39;

    // Clipboard Conversion
    [ObservableProperty]
    private bool _clipboardConversionEnabled = true;

    [ObservableProperty]
    private string _clipboardOutputFormat = "markdown";

    public List<string> AvailableOutputFormats { get; } = new() { "text", "markdown" };

    [ObservableProperty]
    private string _clipboardImageDirectory = @"c:\img";

    [ObservableProperty]
    private string _clipboardImageFormat = "Image attached [{fullpath}{time}{-XXX}.{ext}]";

    public GlobalSettingsViewModel()
    {
        _runAtStartup = StartupManager.IsStartupEnabled();
    }

    public void SetRemotePasswordInput(string value, bool isConfirm)
    {
        if (isConfirm)
        {
            RemotePasswordConfirmInput = value ?? string.Empty;
            return;
        }

        RemotePasswordInput = value ?? string.Empty;
    }

    public void ClearRemotePasswordInputs()
    {
        RemotePasswordInput = string.Empty;
        RemotePasswordConfirmInput = string.Empty;
    }

    public void SetRemotePasswordConfigured(bool value)
    {
        IsRemotePasswordConfigured = value;
    }

    public bool TryPrepareRemotePasswordHash(string? existingHash, out string? remotePasswordHashOverride, out string errorMessage)
    {
        errorMessage = string.Empty;
        remotePasswordHashOverride = null;

        bool hasPrimaryInput = !string.IsNullOrWhiteSpace(RemotePasswordInput);
        bool hasConfirmInput = !string.IsNullOrWhiteSpace(RemotePasswordConfirmInput);
        if (hasPrimaryInput || hasConfirmInput)
        {
            if (!string.Equals(RemotePasswordInput, RemotePasswordConfirmInput, StringComparison.Ordinal))
            {
                errorMessage = "Remote Web Console password and confirmation do not match.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(RemotePasswordInput))
            {
                errorMessage = "Remote Web Console password cannot be empty when confirmation is filled.";
                return false;
            }

            remotePasswordHashOverride = PasswordHashUtility.HashPassword(RemotePasswordInput);
        }

        string effectiveHash = remotePasswordHashOverride ?? existingHash ?? string.Empty;
        if (EnableRemoteWebConsole && string.IsNullOrWhiteSpace(effectiveHash))
        {
            errorMessage = "Remote Web Console requires a password before it can be enabled.";
            return false;
        }

        return true;
    }

    partial void OnRunAtStartupChanged(bool value)
    {
        StartupManager.SetStartup(value);
    }

    [RelayCommand]
    private void ResetInputWatermarkFormat()
    {
        InputWatermarkFormat = AppConfig.DefaultInputWatermarkFormat;
    }

    [RelayCommand]
    private void ResetClipboardImageFormat()
    {
        ClipboardImageFormat = "Image attached [{fullpath}{time}{-XXX}.{ext}]";
    }

    [RelayCommand]
    private void BrowseClipboardImageDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Image Save Directory",
            InitialDirectory = System.IO.Directory.Exists(ClipboardImageDirectory) ? ClipboardImageDirectory : null
        };

        if (dialog.ShowDialog() == true)
        {
            ClipboardImageDirectory = dialog.FolderName;
        }
    }

    private async Task PreviewSpeechAsync(string text)
    {
        using var speechService = new TaskCompletionSpeechService();
        await speechService.SpeakAsync(text, new SpeechPlaybackOptions
        {
            VoiceName = TtsVoiceName,
            Rate = TtsRate,
            Volume = TtsVolume
        });
    }

    private static string RenderTaskCompletionPreview(string template)
    {
        string rendered = (template ?? string.Empty)
            .Replace("{TerminalVoiceName_or_TerminalName}", PreviewTerminalVoiceName, StringComparison.OrdinalIgnoreCase)
            .Replace("{TerminalName}", PreviewTerminalName, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(rendered))
        {
            return $"Mission completed. {PreviewTerminalVoiceName}";
        }

        return rendered;
    }

    private static string RenderTaskFailurePreview(string template)
    {
        string rendered = (template ?? string.Empty)
            .Replace("{TerminalVoiceName_or_TerminalName}", PreviewTerminalVoiceName, StringComparison.OrdinalIgnoreCase)
            .Replace("{TerminalName}", PreviewTerminalName, StringComparison.OrdinalIgnoreCase)
            .Replace("{FailureKeyword}", PreviewFailureKeyword, StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(rendered))
        {
            rendered = $"Hi,Master,Mission failed. {PreviewTerminalVoiceName}. {PreviewFailureKeyword}";
        }

        return rendered;
    }
}

public class TimezoneOption
{
    public int Offset { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}
