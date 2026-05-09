using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using TerminalShell.Controls;
using TerminalShell.Core;
using TerminalShell.Core.Config;
using TerminalShell.Models;
using TerminalShell.Services;
using TerminalShell.Views;

namespace TerminalShell.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private int _cleanupStarted;
    private readonly object _reloadSyncRoot = new();
    private readonly HashSet<string> _reloadInFlightTerminalNames = [];
    private static readonly TimeSpan ShutdownCleanupTimeout = TimeSpan.FromSeconds(8);
    private static readonly string BaseWindowTitle = BuildBaseWindowTitle();
    private string _remoteWindowTitleSuffix = string.Empty;
    private static readonly System.Windows.Media.SolidColorBrush TransparentBrushValue = CreateFrozenBrush("#00FFFFFF");

    [ObservableProperty]
    private ObservableCollection<TerminalSession> _sessions = new();

    [ObservableProperty]
    private ObservableCollection<ContextMenuItemViewModel> _groupedMenuTerminals = new();

    [ObservableProperty]
    private int _gridRows = 2;

    [ObservableProperty]
    private double _inputFontSize = 14.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParsedInputFontFamily))]
    private string _inputFontFamily = "Arial";

    public System.Windows.Media.FontFamily ParsedInputFontFamily => new System.Windows.Media.FontFamily(InputFontFamily);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompactInputMode))]
    [NotifyPropertyChangedFor(nameof(InputTextBoxPadding))]
    [NotifyPropertyChangedFor(nameof(InputContainerPadding))]
    [NotifyPropertyChangedFor(nameof(InputWatermarkMargin))]
    [NotifyPropertyChangedFor(nameof(InputButtonGapHeight))]
    [NotifyPropertyChangedFor(nameof(HistoryButtonHeight))]
    [NotifyPropertyChangedFor(nameof(HistoryButtonVisibility))]
    private double _inputMinHeight = 100.0;

    [ObservableProperty]
    private double _inputMaxHeightPercent = 66.0;

    [ObservableProperty]
    private string _inputWatermarkFormat = AppConfig.DefaultInputWatermarkFormat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectiveGridRows))]
    private TerminalSession? _maximizedSession;

    [ObservableProperty]
    private bool _isTopmost;

    partial void OnIsTopmostChanged(bool value)
    {
        _configManager.Config.IsTopmost = value;
        _configManager.Save();
    }

    [ObservableProperty]
    private bool _isAllTaskAlertsEnabled = true;

    [ObservableProperty]
    private string _windowTitle = BaseWindowTitle;

    public string InputWatermarkStatusSuffix => BuildWatermarkStatusSuffix(IsAllTaskAlertsEnabled, _remoteWindowTitleSuffix, _configManager.Config.ClipboardOutputFormat);

    partial void OnIsAllTaskAlertsEnabledChanged(bool value)
    {
        _configManager.Config.EnableAllTaskAlerts = value;
        _configManager.Save();
        _taskCompletionMonitor.RefreshConfiguration();
        RefreshWindowTitle();
        OnPropertyChanged(nameof(InputWatermarkStatusSuffix));
    }

    private const double CompactInputModeThreshold = 21.0;

    public bool IsCompactInputMode => InputMinHeight <= CompactInputModeThreshold;
    public Thickness InputTextBoxPadding => IsCompactInputMode ? new Thickness(5, 1, 5, 1) : new Thickness(5, 5, 5, 35);
    public Thickness InputContainerPadding => IsCompactInputMode ? new Thickness(2) : new Thickness(5);
    public Thickness InputWatermarkMargin => IsCompactInputMode ? new Thickness(6, 3, 5, 0) : new Thickness(6, 6, 5, 0);
    public double InputButtonGapHeight => IsCompactInputMode ? 0.0 : 5.0;
    public double HistoryButtonHeight => IsCompactInputMode ? 0.0 : 21.0;
    public Visibility HistoryButtonVisibility => IsCompactInputMode ? Visibility.Collapsed : Visibility.Visible;
    public System.Windows.Media.Brush TransparentBrush => TransparentBrushValue;

    public MainWindowThemeState ThemeState { get; } = new();

    public int EffectiveGridRows => MaximizedSession != null ? 1 : GridRows;

    private readonly IConfigManager _configManager;
    private readonly TaskCompletionMonitorService _taskCompletionMonitor;
    private readonly IDraftStorageService _draftStorageService;
    private readonly RemoteWebConsoleService _remoteWebConsole;

    // Default constructor for design-time or legacy support (uses Singleton)
    public MainViewModel() : this(ConfigManager.Instance)
    {
    }

    // Dependency Injection Constructor
    public MainViewModel(IConfigManager configManager)
    {
        _configManager = configManager;
        _taskCompletionMonitor = new TaskCompletionMonitorService(_configManager);
        _draftStorageService = new DraftStorageService();
        _remoteWebConsole = new RemoteWebConsoleService(_configManager, () => _taskCompletionMonitor.UpdateSessions(Sessions));
        _remoteWebConsole.HostStateChanged += RemoteWebConsole_HostStateChanged;
        ReloadSessions();
    }

    private static string BuildBaseWindowTitle()
    {
        return RuntimeAppIdentity.BaseWindowTitle;
    }

    internal static string BuildWatermarkStatusSuffix(bool isAllTaskAlertsEnabled, string remoteTitleSuffix, string clipboardOutputFormat)
    {
        List<string> parts = [];
        if (isAllTaskAlertsEnabled)
        {
            parts.Add("[sound]");
        }

        string normalizedRemoteTitleSuffix = remoteTitleSuffix?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(normalizedRemoteTitleSuffix))
        {
            parts.Add(normalizedRemoteTitleSuffix);
        }

        string normalizedClipboardOutputFormat = clipboardOutputFormat?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalizedClipboardOutputFormat == "markdown")
        {
            parts.Add("[markdown]");
        }
        else if (normalizedClipboardOutputFormat == "text")
        {
            parts.Add("[raw text]");
        }

        return string.Join(" ", parts);
    }

    internal static string BuildWindowTitle(string baseWindowTitle, bool isAllTaskAlertsEnabled, string remoteTitleSuffix)
    {
        List<string> parts = [baseWindowTitle];
        if (isAllTaskAlertsEnabled)
        {
            parts.Add("[sound]");
        }

        string normalizedRemoteTitleSuffix = remoteTitleSuffix?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(normalizedRemoteTitleSuffix))
        {
            parts.Add(normalizedRemoteTitleSuffix);
        }

        return string.Join(" ", parts);
    }

    private static System.Windows.Media.SolidColorBrush CreateFrozenBrush(string rawColor)
    {
        return UiColorHelper.CreateSolidBrush(rawColor, rawColor);
    }

    private void RefreshWindowTitle()
    {
        WindowTitle = BuildWindowTitle(BaseWindowTitle, IsAllTaskAlertsEnabled, _remoteWindowTitleSuffix);
    }

    private void RemoteWebConsole_HostStateChanged(object? sender, RemoteWebConsoleHostStateChangedEventArgs e)
    {
        void ApplyState()
        {
            _remoteWindowTitleSuffix = e.TitleSuffix ?? string.Empty;
            RefreshWindowTitle();
            OnPropertyChanged(nameof(InputWatermarkStatusSuffix));

            if (!e.ShouldWarnUser || string.IsNullOrWhiteSpace(e.WarningMessage))
            {
                return;
            }

            Window? owner = System.Windows.Application.Current?.MainWindow;
            if (owner != null && owner.IsLoaded)
            {
                System.Windows.MessageBox.Show(
                    owner,
                    e.WarningMessage,
                    "Remote Web Console Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            System.Windows.MessageBox.Show(
                e.WarningMessage,
                "Remote Web Console Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
        {
            ApplyState();
            return;
        }

        _ = System.Windows.Application.Current?.Dispatcher.BeginInvoke((Action)ApplyState);
    }

    public void ReloadSessions()
    {
        var config = _configManager.Config;
        
        // 1. Update Layout Settings
        GridRows = config.GridRows;
        OnPropertyChanged(nameof(EffectiveGridRows)); // Notify change for dependent property
        InputFontSize = config.InputFontSize;
        InputFontFamily = config.InputFontFamily;
        OnPropertyChanged(nameof(ParsedInputFontFamily)); // Explicitly notify just in case
        InputMinHeight = config.InputMinHeight;
        InputMaxHeightPercent = config.InputMaxHeightPercent;
        InputWatermarkFormat = config.InputWatermarkFormat;
        IsTopmost = config.IsTopmost;
        IsAllTaskAlertsEnabled = config.EnableAllTaskAlerts;
        ThemeState.Load(config.MainWindowTheme);
        RefreshWindowTitle();
        OnPropertyChanged(nameof(InputWatermarkStatusSuffix));

        // 1.5 Update Grouped Menu Terminals for Context Menu
        GroupedMenuTerminals.Clear();
        var processedGroups = new HashSet<string>();

        foreach (var groupName in config.GroupNameList)
        {
            var termsInGroup = config.Terminals.Where(t => t.GroupName == groupName).ToList();
            if (termsInGroup.Any() || groupName == "UntitledFolder")
            {
                GroupedMenuTerminals.Add(new ContextMenuItemViewModel { IsHeader = true, Name = $"📁 {groupName}" });
                foreach (var t in termsInGroup)
                {
                    GroupedMenuTerminals.Add(new ContextMenuItemViewModel { IsHeader = false, Name = t.Name, Terminal = t });
                }
                processedGroups.Add(groupName);
            }
        }

        // Catch orphans
        var orphans = config.Terminals.Where(t => !processedGroups.Contains(t.GroupName)).ToList();
        if (orphans.Any())
        {
            GroupedMenuTerminals.Add(new ContextMenuItemViewModel { IsHeader = true, Name = $"📁 UntitledFolder" });
            foreach (var t in orphans)
            {
                GroupedMenuTerminals.Add(new ContextMenuItemViewModel { IsHeader = false, Name = t.Name, Terminal = t });
            }
        }

        // 2. Identify Target State (Terminals that should be visible)
        var targetConfigs = config.Terminals
            .Where(t => t.ShowInMainWindow)
            .ToList();

        var currentVisibleSessionNames = Sessions.Select(s => s.Name).ToList();
        var targetVisibleSessionNames = targetConfigs.Select(t => t.Name).ToList();
        bool hasTopologyChange = !currentVisibleSessionNames.SequenceEqual(targetVisibleSessionNames);

        // 3. Identify Current State
        var currentSessions = Sessions.ToList();

        if (hasTopologyChange)
        {
            ConsoleTopologyTransitionCoordinator.BeginTransition("ReloadSessions.VisibleTopologyChanged");
        }

        var sessionsToInitialize = new List<(TerminalSession Session, TerminalConfig Config)>();

        try
        {
            // 4. Remove sessions that are no longer in Target (Match by Name)
            var toRemove = currentSessions
                .Where(s => !targetConfigs.Any(t => t.Name == s.Name))
                .ToList();

            // Conflict Resolution: If MaximizedSession is being removed/hidden, auto-exit maximize mode
            if (MaximizedSession != null && toRemove.Any(r => r == MaximizedSession))
            {
                MaximizedSession = null;
            }

            foreach (var session in toRemove)
            {
                session.Close();
                Sessions.Remove(session);
            }

            // 5. Add new sessions & Reorder
            // We perform all collection modifications synchronously to ensure consistency.
            // Initialization of new sessions happens asynchronously afterwards.

            // 5.1 Add missing sessions (Safe against duplicates)
            var currentSessionsTemp = Sessions.ToList();
            foreach (var termConfig in targetConfigs)
            {
                var existing = currentSessionsTemp.FirstOrDefault(s => s.Name == termConfig.Name);
                if (existing != null)
                {
                    ApplyTerminalConfig(existing, termConfig);
                    currentSessionsTemp.Remove(existing); // Consume match
                }
                else
                {
                    var newSession = CreateConfiguredSession(termConfig);
                    Sessions.Add(newSession);
                    sessionsToInitialize.Add((newSession, termConfig));
                }
            }

            // 5.2 Reorder to match Target Config (Safe against duplicates)
            var sessionsToOrder = Sessions.ToList();
            for (int i = 0; i < targetConfigs.Count; i++)
            {
                var targetConfig = targetConfigs[i];
                var session = sessionsToOrder.FirstOrDefault(s => s.Name == targetConfig.Name);

                if (session != null)
                {
                    sessionsToOrder.Remove(session); // Consume match
                    int oldIndex = Sessions.IndexOf(session);
                    if (oldIndex != i && i < Sessions.Count)
                    {
                        Sessions.Move(oldIndex, i);
                    }
                }
            }
        }
        finally
        {
            if (hasTopologyChange)
            {
                ConsoleTopologyTransitionCoordinator.ScheduleRelease("ReloadSessions.VisibleTopologyChanged");
            }
        }

        // 6. Async Initialization
        if (sessionsToInitialize.Count > 0)
        {
            _ = InitializeSessionsAsync(sessionsToInitialize);
        }

        _taskCompletionMonitor.RefreshConfiguration();
        _taskCompletionMonitor.UpdateSessions(Sessions);
        _remoteWebConsole.ApplyConfiguration(Sessions);
    }

    private void ApplyTerminalConfig(TerminalSession session, TerminalConfig termConfig)
    {
        session.TerminalVoiceName = termConfig.TerminalVoiceName;
        session.TerminalHistorySaveFolder = termConfig.TerminalHistorySaveFolder;
        session.LastInputSaveDirectory = termConfig.LastInputSaveDirectory;
        session.IsAutoDraftQueueEnabled = termConfig.EnableAutoSubmitDraftQueueOnCompletion;
        session.ConfigureDraftStorage(termConfig.DraftStorageKey, _draftStorageService);
    }

    private TerminalSession CreateConfiguredSession(TerminalConfig termConfig)
    {
        TerminalSession session = new(termConfig.Name, termConfig.WorkingDirectory, termConfig.ShellType);
        ApplyTerminalConfig(session, termConfig);
        return session;
    }

    private async Task InitializeSessionsAsync(List<(TerminalSession Session, TerminalConfig Config)> newSessions)
    {
        await Task.Delay(500); // Slight delay for UI to settle
        foreach (var (session, config) in newSessions)
        {
            await session.ExecuteStartupSequence(config);
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        // Check if SettingsWindow is already open to prevent multiple instances
        var existingSettingsWindow = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
        if (existingSettingsWindow != null)
        {
            existingSettingsWindow.Activate();
            if (existingSettingsWindow.WindowState == WindowState.Minimized)
            {
                existingSettingsWindow.WindowState = WindowState.Normal;
            }
            return;
        }

        var settingsWindow = new SettingsWindow(this);
        settingsWindow.Owner = System.Windows.Application.Current.MainWindow;
        
        // Show non-modal (Show) or modal (ShowDialog)
        // Existing code used ShowDialog. Stick to consistent behavior or requirement.
        // Specs say "Directly open settings window".
        // Tray implementation opens it.
        
        // Let's use ShowDialog for now as it was there, but improved check.
        if (settingsWindow.ShowDialog() == true)
        {
             ReloadSessions();
        }
    }
    
    [RelayCommand]
    private void ShowTerminal(TerminalConfig config)
    {
        if (config == null || config.ShowInMainWindow) return;
        
        config.ShowInMainWindow = true;
        _configManager.Save();
        ReloadSessions();
    }

    [RelayCommand]
    private void HideTerminal(TerminalSession session)
    {
        if (session == null) return;

        var termConfig = _configManager.Config.Terminals.FirstOrDefault(t => t.Name == session.Name);
        if (termConfig != null)
        {
            termConfig.ShowInMainWindow = false;
            _configManager.Save();
            
            // Trigger Hot Reload
            ReloadSessions();
        }
    }

    [RelayCommand]
    private void ReloadTerminal(TerminalSession session)
    {
        if (session == null)
        {
            return;
        }

        TerminalConfig? termConfig = _configManager.Config.Terminals.FirstOrDefault(t => string.Equals(t.Name, session.Name, StringComparison.Ordinal));
        if (termConfig == null)
        {
            SimpleLogger.Log($"[ReloadTerminal] Config not found for session '{session.Name}'.");
            return;
        }

        lock (_reloadSyncRoot)
        {
            if (!_reloadInFlightTerminalNames.Add(session.Name))
            {
                SimpleLogger.Log($"[ReloadTerminal] Reload already in flight for session '{session.Name}'.");
                return;
            }
        }

        bool beganTopologyTransition = false;

        try
        {
            int sessionIndex = Sessions.IndexOf(session);
            if (sessionIndex < 0)
            {
                SimpleLogger.Log($"[ReloadTerminal] Session '{session.Name}' is no longer present in Sessions.");
                return;
            }

            string preservedInputBuffer = session.InputBuffer;
            bool wasMaximized = MaximizedSession == session;

            SimpleLogger.Log($"[ReloadTerminal] Restarting session '{session.Name}' at index {sessionIndex}. WasMaximized={wasMaximized}");
            ConsoleTopologyTransitionCoordinator.BeginTransition("ReloadTerminal.CurrentSessionRestart");
            beganTopologyTransition = true;

            TerminalSession newSession = CreateConfiguredSession(termConfig);
            int insertIndex = Math.Clamp(sessionIndex, 0, Sessions.Count);
            Sessions.Insert(insertIndex, newSession);
            newSession.InputBuffer = preservedInputBuffer;

            if (wasMaximized)
            {
                MaximizedSession = newSession;
            }

            session.Close();
            Sessions.Remove(session);

            _taskCompletionMonitor.UpdateSessions(Sessions);
            _remoteWebConsole.ApplyConfiguration(Sessions);

            _ = InitializeSessionsAsync(new List<(TerminalSession Session, TerminalConfig Config)>
            {
                (newSession, termConfig)
            });
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, $"ReloadTerminal('{session.Name}')");
        }
        finally
        {
            if (beganTopologyTransition)
            {
                ConsoleTopologyTransitionCoordinator.ScheduleRelease("ReloadTerminal.CurrentSessionRestart");
            }

            lock (_reloadSyncRoot)
            {
                _reloadInFlightTerminalNames.Remove(session.Name);
            }
        }
    }

    [RelayCommand]
    private void OpenWorkingDirectory(TerminalSession session)
    {
        if (session == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(session.WorkingDirectory))
        {
            System.Windows.MessageBox.Show(
                $"Terminal '{session.Name}' has no working directory configured.",
                "Working Directory Not Set",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(session.WorkingDirectory);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, $"MainViewModel.OpenWorkingDirectory invalid path ({session.Name})");
            System.Windows.MessageBox.Show(
                $"Terminal '{session.Name}' has an invalid working directory.\n\n{session.WorkingDirectory}",
                "Invalid Working Directory",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!Directory.Exists(fullPath))
        {
            System.Windows.MessageBox.Show(
                $"Working directory does not exist for terminal '{session.Name}'.\n\n{fullPath}",
                "Working Directory Missing",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fullPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, $"MainViewModel.OpenWorkingDirectory failed ({session.Name})");
            System.Windows.MessageBox.Show(
                $"Failed to open working directory for terminal '{session.Name}'.\n\n{fullPath}",
                "Open Working Directory Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public bool IsDoubleEnterMode => _configManager.Config.SubmitTriggerMode == "DoubleEnter";
    public bool IsSingleEnterMode => _configManager.Config.SubmitTriggerMode == "SingleEnter";

    [RelayCommand]
    private void SetSubmitMode(string mode)
    {
        if (mode == "DoubleEnter" || mode == "SingleEnter")
        {
            _configManager.Config.SubmitTriggerMode = mode;
            _configManager.Save();
            OnPropertyChanged(nameof(IsDoubleEnterMode));
            OnPropertyChanged(nameof(IsSingleEnterMode));
        }
    }

    // --- Clipboard Output Format Quick Toggle ---
    public bool IsMarkdownOutputMode => _configManager.Config.ClipboardOutputFormat == "markdown";
    public bool IsTextOutputMode => _configManager.Config.ClipboardOutputFormat == "text";

    [RelayCommand]
    private void SetClipboardOutputFormat(string format)
    {
        if (format == "markdown" || format == "text")
        {
            _configManager.Config.ClipboardOutputFormat = format;
            _configManager.Save();
            OnPropertyChanged(nameof(IsMarkdownOutputMode));
            OnPropertyChanged(nameof(IsTextOutputMode));
            OnPropertyChanged(nameof(InputWatermarkStatusSuffix));
        }
    }

    [RelayCommand]
    private void ToggleAutoDraftQueueForSession(TerminalSession session)
    {
        if (session == null)
        {
            return;
        }

        TerminalConfig? config = _configManager.Config.Terminals.FirstOrDefault(term => string.Equals(term.Name, session.Name, StringComparison.Ordinal));
        if (config == null)
        {
            return;
        }

        bool nextValue = !session.IsAutoDraftQueueEnabled;
        session.IsAutoDraftQueueEnabled = nextValue;
        config.EnableAutoSubmitDraftQueueOnCompletion = nextValue;
        _configManager.Save();
        _taskCompletionMonitor.UpdateSessions(Sessions);
    }

    [RelayCommand]
    private void ToggleFullScreen(TerminalSession session)
    {
        if (MaximizedSession == session)
        {
            // Restore
            MaximizedSession = null;
        }
        else
        {
            // Maximize
            MaximizedSession = session;
        }
    }

    public async Task CleanupAsync()
    {
        if (Interlocked.Exchange(ref _cleanupStarted, 1) != 0)
        {
            return;
        }

        _taskCompletionMonitor.Dispose();
        _remoteWebConsole.HostStateChanged -= RemoteWebConsole_HostStateChanged;
        List<Task> backgroundCleanupTasks = new()
        {
            Task.Run(() => _remoteWebConsole.Dispose())
        };

        foreach (TerminalSession session in Sessions)
        {
            backgroundCleanupTasks.Add(Task.Run(() => session.Close()));
        }

        try
        {
            Task allCleanupTask = Task.WhenAll(backgroundCleanupTasks);
            Task completedTask = await Task.WhenAny(allCleanupTask, Task.Delay(ShutdownCleanupTimeout));
            if (completedTask == allCleanupTask)
            {
                await allCleanupTask;
            }
            else
            {
                SimpleLogger.Log($"[SHUTDOWN] Cleanup timed out after {ShutdownCleanupTimeout.TotalSeconds:F0}s. Forcing application exit.");
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "MainViewModel.CleanupAsync");
        }
    }

    public bool TryBuildShutdownConfirmationMessage(out string message)
    {
        IReadOnlyList<ShutdownGuardTerminalActivity> activities = _taskCompletionMonitor.GetShutdownGuardActivities();
        if (activities.Count == 0)
        {
            message = string.Empty;
            return false;
        }

        message = ShutdownGuardEvaluator.BuildConfirmationMessage(activities);
        return true;
    }
}
