using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerminalShell.Core.Config;
using TerminalShell.Models;

using System.Linq;

namespace TerminalShell.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const int FixedNavigationItemCount = 3;

    [ObservableProperty]
    private ObservableCollection<object> _navigationItems = new();

    [ObservableProperty]
    private object? _selectedItem;

    [ObservableProperty]
    private double _windowWidth;

    [ObservableProperty]
    private double _windowHeight;

    [ObservableProperty]
    private double _navWidth = 250.0;
    
    // Hold reference to GlobalVM to sync with Config
    public GlobalSettingsViewModel GlobalSettings { get; } = new();

    // Custom Commands VM
    public CustomCommandsViewModel CustomCommands { get; } = new();

    public ThemesViewModel Themes { get; } = new();

    // Helper property to access Terminals for list operations
    public ObservableCollection<TerminalConfig> Terminals { get; private set; } = new();

    public SettingsViewModel()
    {
        Load();
    }

    private void Load()
    {
        var config = ConfigManager.Instance.Config;
        
        WindowWidth = config.SettingsWindowWidth;
        WindowHeight = config.SettingsWindowHeight;
        NavWidth = config.SettingsNavWidth;
        
        // Setup Global Settings
        GlobalSettings.GridRows = config.GridRows;
        GlobalSettings.InputFontSize = config.InputFontSize;
        GlobalSettings.InputFontFamily = config.InputFontFamily;
        GlobalSettings.InputMinHeight = config.InputMinHeight;
        GlobalSettings.InputMaxHeightPercent = config.InputMaxHeightPercent;
        GlobalSettings.InputWatermarkFormat = config.InputWatermarkFormat;
        GlobalSettings.DefaultStartupCommand = config.DefaultStartupCommand;
        GlobalSettings.MinimizeMainWindowToTray = config.MinimizeMainWindowToTray;
        GlobalSettings.CloseMainWindowToTrayInsteadOfExit = config.CloseMainWindowToTrayInsteadOfExit;
        GlobalSettings.StartMinimizedWhenLaunchedAtStartup = config.StartMinimizedWhenLaunchedAtStartup;
        GlobalSettings.EnableAllTaskAlerts = config.EnableAllTaskAlerts;
        GlobalSettings.EnableRemoteWebConsole = config.EnableRemoteWebConsole;
        GlobalSettings.RemoteProtocolMode = string.IsNullOrWhiteSpace(config.RemoteProtocolMode) ? "HTTP" : config.RemoteProtocolMode;
        GlobalSettings.RemoteBindAddress = string.IsNullOrWhiteSpace(config.RemoteBindAddress) ? "0.0.0.0" : config.RemoteBindAddress;
        GlobalSettings.RemotePort = config.RemotePort;
        GlobalSettings.RemoteSessionTimeoutMinutes = config.RemoteSessionTimeoutMinutes;
        GlobalSettings.RemoteCookieLifetimeDays = config.RemoteCookieLifetimeDays;
        GlobalSettings.RemoteMaxLoginAttempts = config.RemoteMaxLoginAttempts;
        GlobalSettings.RemoteLoginLockoutMinutes = config.RemoteLoginLockoutMinutes;
        GlobalSettings.SetRemotePasswordConfigured(!string.IsNullOrWhiteSpace(config.RemotePasswordHash));
        GlobalSettings.EnableTaskCompletionTts = config.EnableTaskCompletionTts;
        GlobalSettings.TaskCompletionTtsTemplate = config.TaskCompletionTtsTemplate;
        GlobalSettings.EnableTaskFailureTts = config.EnableTaskFailureTts;
        GlobalSettings.TaskFailureTtsTemplate = config.TaskFailureTtsTemplate;
        GlobalSettings.TaskCompletionCheckIntervalSeconds = config.TaskCompletionCheckIntervalSeconds;
        GlobalSettings.TaskCompletionStableThresholdMinutes = config.TaskCompletionStableThresholdMinutes;
        GlobalSettings.TaskCompletionTailLineCount = config.TaskCompletionTailLineCount;
        GlobalSettings.EnableWaitForUserInputGuard = config.EnableWaitForUserInputGuard;
        GlobalSettings.WaitForUserInputKeywords = config.WaitForUserInputKeywords;
        GlobalSettings.TaskFailureKeywords = config.TaskFailureKeywords;
        GlobalSettings.TtsVoiceName = config.TtsVoiceName;
        GlobalSettings.TtsRate = config.TtsRate;
        GlobalSettings.TtsVolume = config.TtsVolume;
        GlobalSettings.EnableAllTerminalsIdleVoiceAlert = config.EnableAllTerminalsIdleVoiceAlert;
        GlobalSettings.EnableAllTerminalsIdlePopupAlert = config.EnableAllTerminalsIdlePopupAlert;
        GlobalSettings.AllTerminalsIdleTemplate = config.AllTerminalsIdleTemplate;
        GlobalSettings.AllTerminalsIdleThresholdMinutes = config.AllTerminalsIdleThresholdMinutes;
        GlobalSettings.AllTerminalsIdleVoiceRepeatMinutes = config.AllTerminalsIdleVoiceRepeatMinutes;
        GlobalSettings.ClipboardConversionEnabled = config.ClipboardConversionEnabled;
        GlobalSettings.ClipboardOutputFormat = config.ClipboardOutputFormat;
        GlobalSettings.ClipboardImageDirectory = config.ClipboardImageDirectory;
        GlobalSettings.ClipboardImageFormat = config.ClipboardImageFormat;

        // Auto Backup Config Loading
        GlobalSettings.EnableAutoBackup = config.EnableAutoBackup;
        GlobalSettings.MaxBackupsRetained = config.MaxBackupsRetained;
        GlobalSettings.BackupIntervalMinutes = config.BackupIntervalMinutes;

        // History Config Loading
        GlobalSettings.HistoryRetentionCount = config.HistoryRetentionCount;
        GlobalSettings.TimeZoneOffset = config.TimeZoneOffset;
        GlobalSettings.HistoryFileNameMaxLength = config.HistoryFileNameMaxLength;
        GlobalSettings.TerminalInputDelayMs = config.TerminalInputDelayMs;
        GlobalSettings.SubmitTriggerMode = config.SubmitTriggerMode;
        
        // Setup Terminals
        Terminals = new ObservableCollection<TerminalConfig>(config.Terminals);
        foreach (var term in Terminals)
        {
            term.PropertyChanged += Term_PropertyChanged;
        }
        
        // Support Custom Commands
        CustomCommands.Load();
        Themes.Load(config.MainWindowTheme);

        // Build Navigation List (Flat List Grouping)
        NavigationItems.Clear();
        NavigationItems.Add(GlobalSettings);
        NavigationItems.Add(CustomCommands);
        NavigationItems.Add(Themes);

        var configuredGroups = config.GroupNameList ?? new List<string>();
        var groupedTerminals = new HashSet<TerminalConfig>();

        // Insert explicit groups and their child terminals
        foreach (var groupname in configuredGroups)
        {
            bool isExpanded = !config.CollapsedGroups.Contains(groupname);
            var header = new GroupHeaderViewModel { GroupName = groupname, IsExpanded = isExpanded };
            header.PropertyChanged += Header_PropertyChanged;
            NavigationItems.Add(header);

            foreach (var term in Terminals.Where(t => t.GroupName == groupname))
            {
                term.IsVisible = isExpanded;
                NavigationItems.Add(term);
                groupedTerminals.Add(term);
            }
        }

        // Handle Orphans (Terminals not in GroupNameList)
        var orphans = Terminals.Except(groupedTerminals).ToList();
        if (orphans.Any())
        {
            bool isExpanded = !config.CollapsedGroups.Contains("UntitledFolder");
            var orphanHeader = new GroupHeaderViewModel { GroupName = "UntitledFolder", IsExpanded = isExpanded };
            orphanHeader.PropertyChanged += Header_PropertyChanged;
            ConfigManager.Instance.Config.GroupNameList.Add("UntitledFolder");
            NavigationItems.Add(orphanHeader);

            foreach (var term in orphans)
            {
                term.GroupName = "UntitledFolder";
                term.IsVisible = isExpanded;
                NavigationItems.Add(term);
            }
            Save();
        }

        // Default Selection
        SelectedItem = GlobalSettings;

        // Restore Window Size
        WindowWidth = config.SettingsWindowWidth > 0 ? config.SettingsWindowWidth : 1800;
        WindowHeight = config.SettingsWindowHeight > 0 ? config.SettingsWindowHeight : 1300;
    }

    [RelayCommand]
    private void AddFolder()
    {
        string baseName = "New Folder";
        string finalName = baseName;
        int counter = 1;

        // Auto-suffixing logic for directory group name
        while (NavigationItems.OfType<GroupHeaderViewModel>().Any(g => g.GroupName == finalName))
        {
            finalName = $"{baseName} ({counter})";
            counter++;
        }

        var newGroup = new GroupHeaderViewModel { GroupName = finalName, IsExpanded = true };
        newGroup.PropertyChanged += Header_PropertyChanged;
        
        string baseTermName = "Terminal";
        int termCounter = Terminals.Count + 1;
        string finalTermName = $"{baseTermName} {termCounter}";
        while (Terminals.Any(t => t.Name == finalTermName))
        {
            termCounter++;
            finalTermName = $"{baseTermName} {termCounter}";
        }

        var newTerm = new TerminalConfig 
        { 
            Name = finalTermName,
            DraftStorageKey = TerminalConfig.CreateDraftStorageKey(),
            EnableAutoSubmitDraftQueueOnCompletion = false,
            GroupName = finalName,
            StartupCommand = GlobalSettings.DefaultStartupCommand
        };
        newTerm.PropertyChanged += Term_PropertyChanged;
        
        Terminals.Add(newTerm);
        NavigationItems.Add(newGroup);
        NavigationItems.Add(newTerm);
        
        ConfigManager.Instance.Config.GroupNameList.Add(finalName);
        
        SelectedItem = newTerm;
        Save();
    }

    [RelayCommand]
    private void AddTerminal(string groupName = "")
    {
        // --- 上下文焦点嗅探 (Contextual Group Resolution) ---
        // 优先级: 显式参数 > 当前选中的目录头 > 当前选中终端的归属组 > 列表第一个正式目录
        string targetGroup;
        if (!string.IsNullOrEmpty(groupName))
        {
            targetGroup = groupName;
        }
        else if (SelectedItem is GroupHeaderViewModel selectedGroup)
        {
            targetGroup = selectedGroup.GroupName;
        }
        else if (SelectedItem is TerminalConfig selectedTerm && !string.IsNullOrEmpty(selectedTerm.GroupName))
        {
            targetGroup = selectedTerm.GroupName;
        }
        else
        {
            // 兜底：抓取导航列表中第一个正式目录作为默认落脚点
            var firstGroup = NavigationItems.OfType<GroupHeaderViewModel>().FirstOrDefault();
            targetGroup = firstGroup?.GroupName ?? "UntitledFolder";
        }

        // 安全防护：如果目标组尚未在视觉树中注册，补足创建
        if (!NavigationItems.OfType<GroupHeaderViewModel>().Any(g => g.GroupName == targetGroup))
        {
            var header = new GroupHeaderViewModel { GroupName = targetGroup, IsExpanded = true };
            header.PropertyChanged += Header_PropertyChanged;
            NavigationItems.Insert(FixedNavigationItemCount, header); // Insert after fixed settings pages
            ConfigManager.Instance.Config.GroupNameList.Add(targetGroup);
        }
        
        // 防重名碰撞哨卡 (Creation-time Collision Guard)
        string baseTermName = "Terminal";
        int termCounter = Terminals.Count + 1;
        string finalTermName = $"{baseTermName} {termCounter}";
        while (Terminals.Any(t => t.Name == finalTermName))
        {
            termCounter++;
            finalTermName = $"{baseTermName} {termCounter}";
        }

        // 静默孵化：新终端默认不在主窗口显示，用户需手动点亮
        var newTerm = new TerminalConfig
        {
            Name = finalTermName,
            DraftStorageKey = TerminalConfig.CreateDraftStorageKey(),
            EnableAutoSubmitDraftQueueOnCompletion = false,
            WorkingDirectory = "",
            StartupCommand = GlobalSettings.DefaultStartupCommand,
            ShowInMainWindow = false,
            GroupName = targetGroup
        };
        newTerm.PropertyChanged += Term_PropertyChanged;
        Terminals.Add(newTerm);
        
        // 视觉渲染定位：确保新终端紧随其归属组尾部插入
        var groupHeader = NavigationItems.OfType<GroupHeaderViewModel>().LastOrDefault(g => g.GroupName == targetGroup);
        if (groupHeader != null)
        {
            int insertIndex = NavigationItems.IndexOf(groupHeader);
            while (insertIndex + 1 < NavigationItems.Count && NavigationItems[insertIndex + 1] is TerminalConfig)
            {
                insertIndex++;
            }
            NavigationItems.Insert(insertIndex + 1, newTerm);
        }
        else
        {
            NavigationItems.Add(newTerm);
        }

        SelectedItem = newTerm;
        Save();
    }

    [RelayCommand]
    public void ToggleGroup(GroupHeaderViewModel header)
    {
        // ToggleButton uses Two-Way binding, so IsExpanded is already flipped correctly by UI!
        int startIndex = NavigationItems.IndexOf(header) + 1;
        for (int i = startIndex; i < NavigationItems.Count; i++)
        {
            if (NavigationItems[i] is GroupHeaderViewModel) break;
            if (NavigationItems[i] is TerminalConfig tc)
            {
                tc.IsVisible = header.IsExpanded;
            }
        }

        // Persist the state immediately (Rebuild structure handles names, but direct toggle needs quick sync)
        if (header.IsExpanded)
        {
            ConfigManager.Instance.Config.CollapsedGroups.Remove(header.GroupName);
        }
        else
        {
            if (!ConfigManager.Instance.Config.CollapsedGroups.Contains(header.GroupName))
            {
                ConfigManager.Instance.Config.CollapsedGroups.Add(header.GroupName);
            }
        }
        Save();
    }

    private void Term_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TerminalConfig.GroupName))
        {
            Save();
        }
    }

    private void Header_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GroupHeaderViewModel.GroupName))
        {
            RebuildDataStructureFromVisuals();
        }
    }

    [RelayCommand]
    private void Clone(TerminalConfig term)
    {
        var newTerm = new TerminalConfig
        {
            Name = $"{term.Name} Clone",
            DraftStorageKey = TerminalConfig.CreateDraftStorageKey(),
            TerminalVoiceName = term.TerminalVoiceName,
            TerminalHistorySaveFolder = string.IsNullOrWhiteSpace(term.TerminalHistorySaveFolder) ? term.Name : term.TerminalHistorySaveFolder,
            LastInputSaveDirectory = string.Empty,
            EnableAutoSubmitDraftQueueOnCompletion = term.EnableAutoSubmitDraftQueueOnCompletion,
            WorkingDirectory = term.WorkingDirectory,
            StartupCommand = term.StartupCommand,
            ShowInMainWindow = term.ShowInMainWindow,
            GroupName = term.GroupName
        };
        newTerm.PropertyChanged += Term_PropertyChanged;

        var index = Terminals.IndexOf(term);
        if (index != -1)
        {
            Terminals.Insert(index + 1, newTerm);
            int navigationIndex = NavigationItems.IndexOf(term);
            NavigationItems.Insert(navigationIndex + 1, newTerm);
            SelectedItem = newTerm;
            Save();
        }
    }

    [RelayCommand]
    private void Delete(TerminalConfig term)
    {
        if (Terminals.Contains(term))
        {
            var result = System.Windows.MessageBox.Show(
                $"Are you sure you want to delete '{term.Name}'?",
                "Delete Terminal",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            term.PropertyChanged -= Term_PropertyChanged;
            Terminals.Remove(term);
            NavigationItems.Remove(term);
            
            if (SelectedItem == term)
            {
                SelectedItem = NavigationItems.FirstOrDefault();
            }
            Save();
        }
    }

    [RelayCommand]
    private void DeleteFolder(GroupHeaderViewModel group)
    {
        if (group.GroupName == "UntitledFolder")
        {
            System.Windows.MessageBox.Show(
                "Cannot delete the default 'UntitledFolder' group.",
                "Delete Folder",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return;
        }

        var terminalsInGroup = Terminals.Where(t => t.GroupName == group.GroupName).ToList();
        string warningMessage = terminalsInGroup.Count > 0 
            ? $"This folder contains {terminalsInGroup.Count} terminals.\n\nDeleting it will also PERMANENTLY delete all these child terminals. Are you absolutely sure?"
            : $"Are you sure you want to delete the empty folder '{group.GroupName}'?";

        var result = System.Windows.MessageBox.Show(
            warningMessage,
            "Delete Folder",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        // 1. Delete all terminals associated with the group
        foreach (var term in terminalsInGroup)
        {
            term.PropertyChanged -= Term_PropertyChanged;
            Terminals.Remove(term);
            NavigationItems.Remove(term);

            if (SelectedItem == term)
            {
                SelectedItem = NavigationItems.FirstOrDefault();
            }
        }

        // 2. Clear Group name from configuration array
        if (ConfigManager.Instance.Config.GroupNameList != null && ConfigManager.Instance.Config.GroupNameList.Contains(group.GroupName))
        {
            ConfigManager.Instance.Config.GroupNameList.Remove(group.GroupName);
        }

        // 3. Remove Header Item visually
        NavigationItems.Remove(group);
        
        if (SelectedItem == group)
        {
            SelectedItem = NavigationItems.FirstOrDefault();
        }

        // Flush IO
        Save();
    }

    [RelayCommand]
    private void MoveUp(TerminalConfig term)
    {
        var index = Terminals.IndexOf(term);
        if (index > 0)
        {
            Terminals.Move(index, index - 1);
            int navigationIndex = NavigationItems.IndexOf(term);
            NavigationItems.Move(navigationIndex, navigationIndex - 1);
            Save();
        }
    }

    [RelayCommand]
    private void MoveDown(TerminalConfig term)
    {
        var index = Terminals.IndexOf(term);
        if (index < Terminals.Count - 1)
        {
            Terminals.Move(index, index + 1);
            int navigationIndex = NavigationItems.IndexOf(term);
            NavigationItems.Move(navigationIndex, navigationIndex + 1);
            Save();
        }
    }

    public void ExecuteDragDropReorder(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex
            || oldIndex < FixedNavigationItemCount
            || newIndex < FixedNavigationItemCount
            || newIndex >= NavigationItems.Count)
            return;

        var movingItem = NavigationItems[oldIndex];

        if (movingItem is GroupHeaderViewModel header)
        {
            var block = new List<object> { header };
            for (int i = oldIndex + 1; i < NavigationItems.Count; i++)
            {
                if (NavigationItems[i] is GroupHeaderViewModel) break;
                if (NavigationItems[i] is TerminalConfig) block.Add(NavigationItems[i]);
            }

            int adjustedNewIndex = newIndex;
            if (newIndex > oldIndex)
            {
                if (newIndex < oldIndex + block.Count) return; // Prevent dropping a folder inside itself
                adjustedNewIndex = newIndex - block.Count + 1;
            }

            for (int i = 0; i < block.Count; i++) NavigationItems.RemoveAt(oldIndex);

            if (adjustedNewIndex > NavigationItems.Count) adjustedNewIndex = NavigationItems.Count;
            if (adjustedNewIndex < 0) adjustedNewIndex = 0;

            for (int i = 0; i < block.Count; i++)
            {
                NavigationItems.Insert(adjustedNewIndex + i, block[i]);
            }
        }
        else if (movingItem is TerminalConfig)
        {
            NavigationItems.Move(oldIndex, newIndex);
        }

        RebuildDataStructureFromVisuals();
    }

    private void RebuildDataStructureFromVisuals()
    {
        var nextCollapsed = new List<string>();
        ConfigManager.Instance.Config.GroupNameList.Clear();
        Terminals.Clear();

        var seenNames = new HashSet<string>();
        string currentGroup = "";

        foreach (var item in NavigationItems)
        {
            if (item is GroupHeaderViewModel header)
            {
                currentGroup = header.GroupName;
                
                // --- Safe Duplicate Collision Resolution ---
                if (seenNames.Contains(currentGroup))
                {
                    int counter = 1;
                    string newName = $"{currentGroup} ({counter})";
                    while (seenNames.Contains(newName))
                    {
                        counter++;
                        newName = $"{currentGroup} ({counter})";
                    }
                    
                    // Temporarily detach to prevent recursive rebuilds
                    header.PropertyChanged -= Header_PropertyChanged;
                    header.GroupName = newName;
                    header.PropertyChanged += Header_PropertyChanged;
                    
                    currentGroup = newName;
                }
                seenNames.Add(currentGroup);
                // -------------------------------------------

                ConfigManager.Instance.Config.GroupNameList.Add(currentGroup);
                if (!header.IsExpanded)
                {
                    nextCollapsed.Add(currentGroup);
                }
            }
            else if (item is TerminalConfig tc)
            {
                tc.GroupName = currentGroup; // Inherit nearest parent above
                Terminals.Add(tc);
            }
        }
        
        ConfigManager.Instance.Config.CollapsedGroups = nextCollapsed;
        Save();
    }

    [RelayCommand]
    private void InsertText(string? text)
    {
        if (SelectedItem is TerminalConfig term && !string.IsNullOrEmpty(text))
        {
            term.StartupCommand += "\n" + text;
        }
    }

    public void Save(string? remotePasswordHashOverride = null)
    {
        // Update Global Config
        var config = ConfigManager.Instance.Config;
        
        config.GridRows = GlobalSettings.GridRows;
        config.InputFontSize = GlobalSettings.InputFontSize;
        config.InputFontFamily = GlobalSettings.InputFontFamily;
        config.InputMinHeight = Math.Clamp(GlobalSettings.InputMinHeight, 21.0, 500.0);
        GlobalSettings.InputMinHeight = config.InputMinHeight;
        config.InputMaxHeightPercent = GlobalSettings.InputMaxHeightPercent;
        config.InputWatermarkFormat = ConfigManager.NormalizeInputWatermarkFormat(GlobalSettings.InputWatermarkFormat);
        GlobalSettings.InputWatermarkFormat = config.InputWatermarkFormat;
        config.DefaultStartupCommand = GlobalSettings.DefaultStartupCommand;
        config.MinimizeMainWindowToTray = GlobalSettings.MinimizeMainWindowToTray;
        config.CloseMainWindowToTrayInsteadOfExit = GlobalSettings.CloseMainWindowToTrayInsteadOfExit;
        config.StartMinimizedWhenLaunchedAtStartup = GlobalSettings.StartMinimizedWhenLaunchedAtStartup;
        config.EnableAllTaskAlerts = GlobalSettings.EnableAllTaskAlerts;
        config.EnableRemoteWebConsole = GlobalSettings.EnableRemoteWebConsole;
        config.RemoteProtocolMode = string.IsNullOrWhiteSpace(GlobalSettings.RemoteProtocolMode) ? "HTTP" : GlobalSettings.RemoteProtocolMode.Trim();
        GlobalSettings.RemoteProtocolMode = config.RemoteProtocolMode;
        config.RemoteBindAddress = string.IsNullOrWhiteSpace(GlobalSettings.RemoteBindAddress) ? "0.0.0.0" : GlobalSettings.RemoteBindAddress.Trim();
        GlobalSettings.RemoteBindAddress = config.RemoteBindAddress;
        config.RemotePort = Math.Clamp(GlobalSettings.RemotePort, 1024, 65535);
        GlobalSettings.RemotePort = config.RemotePort;
        config.RemoteSessionTimeoutMinutes = Math.Clamp(GlobalSettings.RemoteSessionTimeoutMinutes, 1, 10080);
        GlobalSettings.RemoteSessionTimeoutMinutes = config.RemoteSessionTimeoutMinutes;
        config.RemoteCookieLifetimeDays = Math.Clamp(GlobalSettings.RemoteCookieLifetimeDays, 1, 3650);
        GlobalSettings.RemoteCookieLifetimeDays = config.RemoteCookieLifetimeDays;
        config.RemoteMaxLoginAttempts = Math.Clamp(GlobalSettings.RemoteMaxLoginAttempts, 1, 20);
        GlobalSettings.RemoteMaxLoginAttempts = config.RemoteMaxLoginAttempts;
        config.RemoteLoginLockoutMinutes = Math.Clamp(GlobalSettings.RemoteLoginLockoutMinutes, 1, 1440);
        GlobalSettings.RemoteLoginLockoutMinutes = config.RemoteLoginLockoutMinutes;
        if (remotePasswordHashOverride != null)
        {
            config.RemotePasswordHash = remotePasswordHashOverride;
            GlobalSettings.SetRemotePasswordConfigured(!string.IsNullOrWhiteSpace(remotePasswordHashOverride));
        }

        config.EnableTaskCompletionTts = GlobalSettings.EnableTaskCompletionTts;
        config.TaskCompletionTtsTemplate = GlobalSettings.TaskCompletionTtsTemplate;
        config.EnableTaskFailureTts = GlobalSettings.EnableTaskFailureTts;
        config.TaskFailureTtsTemplate = GlobalSettings.TaskFailureTtsTemplate;
        config.TaskCompletionCheckIntervalSeconds = Math.Clamp(GlobalSettings.TaskCompletionCheckIntervalSeconds, 1, 3600);
        GlobalSettings.TaskCompletionCheckIntervalSeconds = config.TaskCompletionCheckIntervalSeconds;
        config.TaskCompletionStableThresholdMinutes = Math.Clamp(GlobalSettings.TaskCompletionStableThresholdMinutes, 1, 1440);
        GlobalSettings.TaskCompletionStableThresholdMinutes = config.TaskCompletionStableThresholdMinutes;
        config.TaskCompletionTailLineCount = Math.Clamp(GlobalSettings.TaskCompletionTailLineCount, 5, 200);
        GlobalSettings.TaskCompletionTailLineCount = config.TaskCompletionTailLineCount;
        config.EnableWaitForUserInputGuard = GlobalSettings.EnableWaitForUserInputGuard;
        config.MainWindowTheme = Themes.BuildThemeConfig();
        config.WaitForUserInputKeywords = GlobalSettings.WaitForUserInputKeywords;
        config.TaskFailureKeywords = GlobalSettings.TaskFailureKeywords;
        config.TtsVoiceName = GlobalSettings.TtsVoiceName;
        config.TtsRate = Math.Clamp(GlobalSettings.TtsRate, -10, 10);
        GlobalSettings.TtsRate = config.TtsRate;
        config.TtsVolume = Math.Clamp(GlobalSettings.TtsVolume, 0, 100);
        GlobalSettings.TtsVolume = config.TtsVolume;
        config.EnableAllTerminalsIdleVoiceAlert = GlobalSettings.EnableAllTerminalsIdleVoiceAlert;
        config.EnableAllTerminalsIdlePopupAlert = GlobalSettings.EnableAllTerminalsIdlePopupAlert;
        config.AllTerminalsIdleTemplate = GlobalSettings.AllTerminalsIdleTemplate;
        config.AllTerminalsIdleThresholdMinutes = Math.Clamp(GlobalSettings.AllTerminalsIdleThresholdMinutes, 1, 1440);
        GlobalSettings.AllTerminalsIdleThresholdMinutes = config.AllTerminalsIdleThresholdMinutes;
        config.AllTerminalsIdleVoiceRepeatMinutes = Math.Clamp(GlobalSettings.AllTerminalsIdleVoiceRepeatMinutes, 1, 1440);
        GlobalSettings.AllTerminalsIdleVoiceRepeatMinutes = config.AllTerminalsIdleVoiceRepeatMinutes;
        config.ClipboardConversionEnabled = GlobalSettings.ClipboardConversionEnabled;
        config.ClipboardOutputFormat = GlobalSettings.ClipboardOutputFormat;
        config.ClipboardImageDirectory = GlobalSettings.ClipboardImageDirectory;
        config.ClipboardImageFormat = GlobalSettings.ClipboardImageFormat;
        
        // Auto Backup Config Saving
        config.EnableAutoBackup = GlobalSettings.EnableAutoBackup;
        config.MaxBackupsRetained = GlobalSettings.MaxBackupsRetained;
        config.BackupIntervalMinutes = GlobalSettings.BackupIntervalMinutes;

        // History Config Saving
        config.HistoryRetentionCount = GlobalSettings.HistoryRetentionCount;
        config.TimeZoneOffset = GlobalSettings.TimeZoneOffset;
        config.HistoryFileNameMaxLength = GlobalSettings.HistoryFileNameMaxLength;
        config.TerminalInputDelayMs = GlobalSettings.TerminalInputDelayMs;
        config.SubmitTriggerMode = GlobalSettings.SubmitTriggerMode;

        config.SettingsWindowWidth = WindowWidth;
        config.SettingsWindowHeight = WindowHeight;
        config.SettingsNavWidth = NavWidth;
        EnsureTerminalHistorySaveFolders(Terminals);
        config.Terminals = new List<TerminalConfig>(Terminals);
        // config.GroupNameList was independently rebuilt during RebuildDataStructureFromVisuals operations
        
        // Save CustomCommands Model
        CustomCommands.Save();
        
        ConfigManager.Instance.Save();
        TerminalShell.Core.StartupManager.RefreshStartupShortcut();
        SettingsSaved?.Invoke();
    }

    private static void EnsureTerminalHistorySaveFolders(IEnumerable<TerminalConfig> terminals)
    {
        foreach (TerminalConfig terminal in terminals)
        {
            if (!string.IsNullOrWhiteSpace(terminal.TerminalHistorySaveFolder))
            {
                continue;
            }

            terminal.TerminalHistorySaveFolder = string.IsNullOrWhiteSpace(terminal.Name) ? "Default" : terminal.Name;
        }
    }

    public event System.Action? SettingsSaved;
}
