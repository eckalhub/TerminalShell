using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using TerminalShell.Interop;
using TerminalShell.Core;
using TerminalShell.Core.Config;
using TerminalShell.Services;

using System.Collections.Generic; // For List
using System.Collections.ObjectModel; // For ObservableCollection
using System.Linq;
using TerminalShell.Views;

namespace TerminalShell.Models;

public class CustomCommandItem
{
    public string DisplayText { get; set; } = string.Empty;
    public string FullText { get; set; } = string.Empty;
    public string CategoryKey { get; set; } = string.Empty;
}

public enum CommandPopupMode
{
    None,
    SlashSearch,
    FunctionCategory
}

public enum TerminalSendOrigin
{
    Manual,
    Startup,
    AutoDraft
}

public enum TerminalUserInteractionKind
{
    InputEdited,
    SaveDraft,
    LoadDraft,
    DeleteDraft,
    MoveDraftLeft,
    MoveDraftRight,
    ClearInput
}

public sealed class TerminalCommandSentEventArgs
{
    public string CommandText { get; init; } = string.Empty;
    public TerminalSendOrigin Origin { get; init; } = TerminalSendOrigin.Manual;
    public string? DraftId { get; init; }
}

public partial class TerminalSession : ObservableObject
{
    private static readonly object _consoleLock = new object();
    private static readonly Regex ConsecutiveWhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private Process? _process;
    private IntPtr _cachedWindowHandle = IntPtr.Zero;
    
    /// <summary>
    /// [v2.0 Overlay] Cached HWND for persistence across WPF DataTemplate recreation.
    /// ConsoleHost reads/writes this to survive RDP-triggered visual tree rebuilds.
    /// </summary>
    public IntPtr CachedWindowHandle
    {
        get => _cachedWindowHandle;
        set => _cachedWindowHandle = value;
    }
    
    // We expose the Process object so the View can grab the Handle
    public Process? AppProcess => _process;
    public string WorkingDirectory => _workingDirectory;
    public string OpenWorkingDirectoryMenuText =>
        string.IsNullOrWhiteSpace(WorkingDirectory)
            ? "Open Working Directory"
            : $"Open Working Directory [{WorkingDirectory}]";
    public string DraftStorageKey { get; private set; } = string.Empty;

    public event Action<TerminalSession, TerminalCommandSentEventArgs>? UserCommandSent;
    public event Action<TerminalSession, TerminalUserInteractionKind>? UserInteractionOccurred;

    [ObservableProperty]
    private string _outputLog = "Terminal Initializing...";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveDraftCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveInputToFileCommand))]
    private string _inputBuffer = "";

    [ObservableProperty]
    private string _title = "Terminal";

    [ObservableProperty]
    private string _terminalVoiceName = string.Empty;

    [ObservableProperty]
    private string _terminalHistorySaveFolder = string.Empty;

    [ObservableProperty]
    private string _lastInputSaveDirectory = string.Empty;

    [ObservableProperty]
    private bool _isCommandPopupOpen;

    [ObservableProperty]
    private ObservableCollection<CustomCommandItem> _filteredCommands = new();

    [ObservableProperty]
    private int _selectedCommandIndex = 0;

    [ObservableProperty]
    private double _commandPopupMaxHeight = 300.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPopupHeaderText))]
    [NotifyPropertyChangedFor(nameof(CommandPopupHeaderVisibility))]
    private CommandPopupMode _commandPopupMode = CommandPopupMode.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPopupHeaderText))]
    [NotifyPropertyChangedFor(nameof(CommandPopupHeaderVisibility))]
    private string _commandPopupCategoryKey = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPopupHeaderText))]
    [NotifyPropertyChangedFor(nameof(CommandPopupHeaderVisibility))]
    private string _commandPopupFilterText = string.Empty;

    [ObservableProperty]
    private bool _isAutoDraftQueueEnabled;

    [ObservableProperty]
    private bool _isAutoDraftQueueActive;

    [ObservableProperty]
    private bool _isAutoDraftQueueWaitingForUserInput;

    [ObservableProperty]
    private bool _isWaitingForUserInput;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInputDrafts))]
    [NotifyPropertyChangedFor(nameof(DraftOverlayVisibility))]
    [NotifyPropertyChangedFor(nameof(DraftButtonColumnCount))]
    [NotifyCanExecuteChangedFor(nameof(MoveDraftLeftCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveDraftRightCommand))]
    private ObservableCollection<TerminalDraft> _inputDrafts = new();

    private readonly string _workingDirectory;
    private IDraftStorageService? _draftStorageService;
    private string? _loadedDraftId;
    private int _suppressedTextEditNotifications;

    public TerminalSession(string name, string workingDirectory = "", string shellType = "cmd")
    {
        Name = name;
        _workingDirectory = workingDirectory;
        ShellType = shellType;
        Title = name; // Initialize Title with Name by default
        StartNativeProcess();
    }

    public string Name { get; }
    public string ShellType { get; }
    public string CommandPopupHeaderText =>
        !IsCommandPopupOpen || CommandPopupMode != CommandPopupMode.FunctionCategory || string.IsNullOrWhiteSpace(CommandPopupCategoryKey)
            ? string.Empty
            : string.IsNullOrWhiteSpace(CommandPopupFilterText)
                ? $"{CommandPopupCategoryKey} block · All commands"
                : $"{CommandPopupCategoryKey} block · Filter: {CommandPopupFilterText}";

    public Visibility CommandPopupHeaderVisibility =>
        string.IsNullOrWhiteSpace(CommandPopupHeaderText) ? Visibility.Collapsed : Visibility.Visible;

    public bool HasInputDrafts => InputDrafts.Count > 0;
    public Visibility DraftOverlayVisibility => HasInputDrafts ? Visibility.Visible : Visibility.Collapsed;
    public int DraftButtonColumnCount => Math.Max(InputDrafts.Count, 1);

    partial void OnIsCommandPopupOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(CommandPopupHeaderText));
        OnPropertyChanged(nameof(CommandPopupHeaderVisibility));

        if (!value)
        {
            CommandPopupMode = CommandPopupMode.None;
            CommandPopupCategoryKey = string.Empty;
            CommandPopupFilterText = string.Empty;
            SelectedCommandIndex = 0;
            FilteredCommands.Clear();
        }
    }

    private async void StartNativeProcess()
    {
        try
        {
            string shellExe = ResolveShellFileName(ShellType);
            
            NativeMethods.STARTUPINFO si = new NativeMethods.STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            // [v3.94 Right-Side Throw]: Throw far right, size 10x10 to eliminate wide terminal "tail" bleeding
            si.dwFlags = NativeMethods.STARTF_USEPOSITION | NativeMethods.STARTF_USESIZE;
            si.dwX = 32000;
            si.dwY = 32000;
            si.dwXSize = 10;
            si.dwYSize = 10;

            NativeMethods.PROCESS_INFORMATION pi = new NativeMethods.PROCESS_INFORMATION();
            NativeMethods.SECURITY_ATTRIBUTES pSec = new NativeMethods.SECURITY_ATTRIBUTES();
            NativeMethods.SECURITY_ATTRIBUTES tSec = new NativeMethods.SECURITY_ATTRIBUTES();

            pSec.nLength = Marshal.SizeOf(pSec);
            tSec.nLength = Marshal.SizeOf(tSec);

            // [v3.94] Zero-Flicker: Launch the process completely off-screen using Win32 CreateProcess
            // [v3.95] Native Working Directory Injection: Pass the pre-validated working directory to avoid CD concat bugs
            string? targetDirectory = !string.IsNullOrWhiteSpace(_workingDirectory) && Directory.Exists(_workingDirectory) 
                ? _workingDirectory : null;

            bool success = NativeMethods.CreateProcess(
                null,
                shellExe,
                ref pSec,
                ref tSec,
                false,
                0, // Normal creation flags
                IntPtr.Zero,
                targetDirectory,
                ref si,
                out pi);

            if (success)
            {
                TerminalProcessJob.Instance.TryAddProcess(pi.hProcess);

                // Close the newly created thread/process handles to avoid memory leaks (we rely on .NET Process wrapper later)
                NativeMethods.CloseHandle(pi.hThread);
                NativeMethods.CloseHandle(pi.hProcess);

                // Reconstruct the .NET Process object so the rest of the app doesn't break
                _process = Process.GetProcessById(pi.dwProcessId);
                SimpleLogger.Log($"Starting Terminal Process (Off-Screen Throw): {_process.Id}");
                
                // Allow time for window creation
                await Task.Run(() => 
                {
                    // Try to wait for input idle, but don't crash if it fails
                    try
                    {
                        _process.WaitForInputIdle();
                    }
                    catch (InvalidOperationException ex)
                    {
                        SimpleLogger.Log($"[ProcessStartup] WaitForInputIdle skipped for PID {_process.Id}: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.LogError(ex, "WaitForInputIdle");
                    }

                    try 
                    {
                        // Spin wait for Handle using AttachConsole Handle Hunter (Ultra Reliable)
                        int retries = 0;
                        IntPtr foundHwnd = IntPtr.Zero;
                        while (foundHwnd == IntPtr.Zero && retries < 50)
                        {
                            Thread.Sleep(100);
                            foundHwnd = NativeMethods.GetConsoleHwndForProcess((uint)_process.Id);
                            retries++;
                        }
                        
                        // Cache the handle BEFORE it gets reparented
                        if (foundHwnd != IntPtr.Zero)
                        {
                            _cachedWindowHandle = foundHwnd;
                            
                            // DIAGNOSTIC LOG
                            NativeMethods.GetWindowRect(foundHwnd, out NativeMethods.RECT rect1);
                            SimpleLogger.Log($"[ZERO-FLICKER] HWND Captured via AttachConsole: {foundHwnd} for PID: {_process.Id}. Bounds: X={rect1.Left}, Y={rect1.Top}, W={rect1.Right - rect1.Left}, H={rect1.Bottom - rect1.Top}");
                        }
                        else
                        {
                            SimpleLogger.Log($"WARNING: Failed to capture Hidden HWND for PID: {_process.Id} after 50 retries via AttachConsole");
                        }
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.LogError(ex, "StartNativeProcess Wait Loop");
                    }
                });

                // Notify UI that Process is ready
                OnPropertyChanged(nameof(AppProcess));
                OutputLog = "Terminal Ready. Native Window Embedded.";
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                OutputLog = $"Failed to CreateProcess. Error: {error}";
                SimpleLogger.Log($"[ZERO-FLICKER] {OutputLog}");
            }
        }
        catch (Exception ex)
        {
            OutputLog = $"Error starting process: {ex.Message}";
            SimpleLogger.LogError(ex, "StartNativeProcess");
        }
    }

    public void ConfigureDraftStorage(string draftStorageKey, IDraftStorageService? draftStorageService)
    {
        DraftStorageKey = draftStorageKey ?? string.Empty;
        _draftStorageService = draftStorageService;
        ReloadDraftsFromStorage();
    }

    public bool SendCommand()
    {
        TerminalDraft? loadedDraft = InputDrafts.FirstOrDefault(draft => string.Equals(draft.Id, _loadedDraftId, StringComparison.Ordinal));
        _loadedDraftId = null;
        return SendCommandCore(InputBuffer, TerminalSendOrigin.Manual, loadedDraft);
    }

    public bool TrySendCommandText(string? commandText)
    {
        return SendCommandCore(commandText, TerminalSendOrigin.Manual, null);
    }

    public IReadOnlyList<TerminalDraft> GetInputDraftsSnapshot()
    {
        return InputDrafts.Select(draft => draft.Clone()).ToList();
    }

    public bool TrySendDraftById(string? draftId)
    {
        if (string.IsNullOrWhiteSpace(draftId))
        {
            return false;
        }

        TerminalDraft? draft = InputDrafts.FirstOrDefault(item => string.Equals(item.Id, draftId, StringComparison.Ordinal));
        if (draft == null)
        {
            return false;
        }

        return SendCommandCore(draft.Text, TerminalSendOrigin.Manual, draft);
    }

    public bool DeleteDraftById(string? draftId)
    {
        if (string.IsNullOrWhiteSpace(draftId) || _draftStorageService == null || string.IsNullOrWhiteSpace(DraftStorageKey))
        {
            return false;
        }

        if (!_draftStorageService.DeleteDraft(DraftStorageKey, draftId))
        {
            return false;
        }

        ReloadDraftsFromStorage();
        RaiseUserInteraction(TerminalUserInteractionKind.DeleteDraft);
        return true;
    }

    public void ResizeConsoleBuffer()
    {
        lock (_consoleLock)
        {
            try
            {
                if (_process == null || _process.HasExited)
                {
                    SimpleLogger.Log("[BUFFER_SYNC] ResizeConsoleBuffer skipped because process is missing or exited.");
                    return;
                }

                // 1. Detach (Safety)
                NativeMethods.FreeConsole();

                // 2. Attach
                if (!NativeMethods.AttachConsole((uint)_process.Id))
                {
                    SimpleLogger.Log($"[BUFFER_SYNC] ResizeConsoleBuffer skipped because AttachConsole failed. PID={_process.Id}");
                    return;
                }

                IntPtr hStdOut = NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE);
                if (hStdOut == IntPtr.Zero || hStdOut == (IntPtr)(-1))
                {
                    SimpleLogger.Log($"[BUFFER_SYNC] ResizeConsoleBuffer skipped because STDOUT handle is invalid. PID={_process.Id}");
                    return;
                }

                if (!NativeMethods.GetConsoleScreenBufferInfo(hStdOut, out var info))
                {
                    SimpleLogger.Log($"[BUFFER_SYNC] ResizeConsoleBuffer skipped because GetConsoleScreenBufferInfo failed. PID={_process.Id}");
                    return;
                }

                // Calculate new width based on the current visible console window width after SetWindowPos.
                short newWidth = (short)(info.srWindow.Right - info.srWindow.Left + 1);
                if (newWidth <= 0)
                {
                    SimpleLogger.Log($"[BUFFER_SYNC] ResizeConsoleBuffer skipped because visible window width was invalid ({newWidth}). PID={_process.Id}");
                    return;
                }

                // Keep height as is to preserve history and avoid destructive reflow.
                short newHeight = info.dwSize.Y;
                if (newHeight <= 0)
                {
                    SimpleLogger.Log($"[BUFFER_SYNC] ResizeConsoleBuffer skipped because buffer height was invalid ({newHeight}). PID={_process.Id}");
                    return;
                }

                if (info.dwSize.X == newWidth)
                {
                    SimpleLogger.Log($"[BUFFER_SYNC] ResizeConsoleBuffer skipped because width already matches ({newWidth}). PID={_process.Id}");
                    return;
                }

                NativeMethods.SetConsoleScreenBufferSize(hStdOut, new NativeMethods.COORD(newWidth, newHeight));
                SimpleLogger.Log($"[BUFFER_SYNC] ResizeConsoleBuffer applied. PID={_process.Id} NewWidth={newWidth} Height={newHeight}");
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "ResizeConsoleBuffer");
            }
            finally
            {
                NativeMethods.FreeConsole();
            }
        }
    }

    public async Task ExecuteStartupSequence(TerminalConfig config)
    {
        // [v3.95] Working Directory is now injected natively via CreateProcess lpCurrentDirectory.
        // Removed all Set-Location / cd /d string-based commands here to prevent PSReadLine enter-swallow bugs.

        // [v3.96] Shell Initialization Grace Period
        // PowerShell PSReadLine and cmd take time to fully initialize and hook the console buffer.
        // Sending Virtual-Key Enter events immediately upon startup often results in the shell
        // swallowing the Enter key, leaving the text hanging at the prompt.
        await Task.Delay(2000);

        // Parse and Execute Startup Command
        if (!string.IsNullOrWhiteSpace(config.StartupCommand))
        {
            using (StringReader reader = new StringReader(config.StartupCommand))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (line.StartsWith("[sleep=") && line.EndsWith("]"))
                    {
                        // Parse sleep token: [sleep=5000]
                        string content = line.Substring(7, line.Length - 8);
                        if (int.TryParse(content, out int ms))
                        {
                            await Task.Delay(ms);
                        }
                    }
                    else
                    {
                        SendInternal(line);
                        // Default small delay between lines to prevent buffer overflow
                        await Task.Delay(100); 
                    }
                }
            }
        }
    }

    // internal helper to bypass UI buffer binding
    private void SendInternal(string cmd)
    {
        SendCommandCore(cmd, TerminalSendOrigin.Startup, null);
    }

    [RelayCommand(CanExecute = nameof(CanSaveDraft))]
    private void SaveDraft()
    {
        if (_draftStorageService == null || string.IsNullOrWhiteSpace(DraftStorageKey))
        {
            return;
        }

        TerminalDraft? savedDraft = _draftStorageService.SaveDraft(DraftStorageKey, InputBuffer);
        if (savedDraft == null)
        {
            return;
        }

        ReloadDraftsFromStorage();
        RaiseUserInteraction(TerminalUserInteractionKind.SaveDraft);
    }

    private bool CanSaveDraft()
    {
        return !string.IsNullOrWhiteSpace(InputBuffer);
    }

    [RelayCommand(CanExecute = nameof(CanSaveInputToFile))]
    private void SaveInputToFile()
    {
        Microsoft.Win32.SaveFileDialog dialog = new()
        {
            Title = "Save Input To File",
            AddExtension = true,
            DefaultExt = ".md",
            Filter = "所有文件类型 (*.*)|*.*",
            FileName = InputBufferFileSaveService.BuildDefaultFileName(ConfigManager.Instance.Config.TimeZoneOffset),
            InitialDirectory = InputBufferFileSaveService.ResolveInitialDirectory(LastInputSaveDirectory, WorkingDirectory),
            OverwritePrompt = true
        };

        bool? dialogResult = System.Windows.Application.Current?.MainWindow is Window owner
            ? dialog.ShowDialog(owner)
            : dialog.ShowDialog();

        if (dialogResult != true)
        {
            return;
        }

        try
        {
            File.WriteAllText(dialog.FileName, InputBuffer, new UTF8Encoding(false));
            PersistLastInputSaveDirectory(Path.GetDirectoryName(dialog.FileName));
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to save input to file.{Environment.NewLine}{ex.Message}",
                "TerminalShell",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private bool CanSaveInputToFile()
    {
        return !string.IsNullOrEmpty(InputBuffer);
    }

    [RelayCommand]
    private void LoadDraft(TerminalDraft? draft)
    {
        if (draft == null)
        {
            return;
        }

        InputBuffer = draft.Text;
        _loadedDraftId = draft.Id;
        RaiseUserInteraction(TerminalUserInteractionKind.LoadDraft);
    }

    [RelayCommand]
    private void DeleteDraft(TerminalDraft? draft)
    {
        if (draft == null || _draftStorageService == null || string.IsNullOrWhiteSpace(DraftStorageKey))
        {
            return;
        }

        if (_draftStorageService.DeleteDraft(DraftStorageKey, draft.Id))
        {
            if (string.Equals(_loadedDraftId, draft.Id, StringComparison.Ordinal))
            {
                _loadedDraftId = null;
            }

            ReloadDraftsFromStorage();
            RaiseUserInteraction(TerminalUserInteractionKind.DeleteDraft);
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveDraftLeft))]
    private void MoveDraftLeft(TerminalDraft? draft)
    {
        MoveDraftInternal(draft, -1, TerminalUserInteractionKind.MoveDraftLeft);
    }

    private bool CanMoveDraftLeft(TerminalDraft? draft)
    {
        if (draft == null)
        {
            return false;
        }

        int index = FindDraftIndex(draft.Id);
        return index > 0;
    }

    [RelayCommand(CanExecute = nameof(CanMoveDraftRight))]
    private void MoveDraftRight(TerminalDraft? draft)
    {
        MoveDraftInternal(draft, 1, TerminalUserInteractionKind.MoveDraftRight);
    }

    private bool CanMoveDraftRight(TerminalDraft? draft)
    {
        if (draft == null)
        {
            return false;
        }

        int index = FindDraftIndex(draft.Id);
        return index >= 0 && index < InputDrafts.Count - 1;
    }

    public void Close()
    {
        SimpleLogger.Log($"[SHUTDOWN] TerminalSession.Close() STARTED for {Name} (PID: {_process?.Id})");
        try
        {
            // Step 1: Instantly hide the overlay window to prevent visual artifacts
            if (_cachedWindowHandle != IntPtr.Zero)
            {
                SimpleLogger.Log($"[SHUTDOWN] Hiding overlay HWND {_cachedWindowHandle}...");
                NativeMethods.ShowWindow(_cachedWindowHandle, NativeMethods.SW_HIDE);
            }

            // Step 2 (治本方案): Reverse-lookup the REAL shell PID from the cached HWND.
            // The _process field points to conhost.exe (the console host), NOT powershell.exe itself.
            // GetWindowThreadProcessId gives us the process that actually owns the window — the shell.
            if (_cachedWindowHandle != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(_cachedWindowHandle, out uint realShellPid);
                if (realShellPid != 0)
                {
                    SimpleLogger.Log($"[SHUTDOWN] HWND→PID reverse-lookup: real shell PID = {realShellPid}");
                    try
                    {
                        var realProcess = Process.GetProcessById((int)realShellPid);
                        if (!realProcess.HasExited)
                        {
                            // Kill the entire process tree so child processes (e.g. spawned by the shell) are cleaned up too
                            realProcess.Kill(entireProcessTree: true);
                            realProcess.Dispose();
                            SimpleLogger.Log($"[SHUTDOWN] Real shell process tree (PID {realShellPid}) killed.");
                        }
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.LogError(ex, $"[SHUTDOWN] Failed to kill real shell PID {realShellPid}");
                    }
                }
            }

            // Step 3: Also kill the conhost/host process as a fallback cleanup
            if (_process != null && !_process.HasExited)
            {
                SimpleLogger.Log($"[SHUTDOWN] Killing conhost/host process PID {_process.Id}...");
                try { _process.Kill(); } catch (Exception ex) { SimpleLogger.LogError(ex, "Fallback conhost Kill"); }
                _process.Dispose();
            }

            SimpleLogger.Log($"[SHUTDOWN] TerminalSession.Close() FINISHED for {Name}");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, $"TerminalSession.Close() for {Name}");
        }
    }

    [RelayCommand]
    private void ClearInput()
    {
        InputBuffer = string.Empty;
        RaiseUserInteraction(TerminalUserInteractionKind.ClearInput);
    }

    public void NotifyTextEditedByUser()
    {
        RaiseUserInteraction(TerminalUserInteractionKind.InputEdited);
    }

    private void PersistLastInputSaveDirectory(string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        LastInputSaveDirectory = directoryPath;

        TerminalConfig? configTerminal = ConfigManager.Instance.Config.Terminals
            .FirstOrDefault(term => string.Equals(term.Name, Name, StringComparison.Ordinal));

        if (configTerminal == null)
        {
            return;
        }

        if (string.Equals(configTerminal.LastInputSaveDirectory, directoryPath, StringComparison.Ordinal))
        {
            return;
        }

        configTerminal.LastInputSaveDirectory = directoryPath;
        ConfigManager.Instance.Save();
    }

    public bool TryConsumeTextEditNotificationSuppression()
    {
        if (_suppressedTextEditNotifications <= 0)
        {
            return false;
        }

        _suppressedTextEditNotifications--;
        return true;
    }

    public bool TrySendNextDraftAutomatically()
    {
        TerminalDraft? nextDraft = InputDrafts.FirstOrDefault();
        if (nextDraft == null)
        {
            return false;
        }

        return SendCommandCore(nextDraft.Text, TerminalSendOrigin.AutoDraft, nextDraft);
    }

    [RelayCommand]
    private void ReadTerminalContent()
    {
        if (_process == null || _process.HasExited)
        {
            System.Windows.MessageBox.Show("Terminal not running", "TerminalShell", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        string terminalText = ReadTerminalSnapshot();

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var window = new TerminalContentWindow(terminalText)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            window.ShowDialog();
        });
    }

    public string ReadTerminalTailSnapshot(int tailLineCount)
    {
        return ReadTerminalSnapshot(tailLineCount);
    }

    private string ReadTerminalSnapshot(int? tailLineCount = null)
    {
        if (_process == null || _process.HasExited)
        {
            return "Cannot read terminal content.";
        }

        List<string> lines = new List<string>();

        lock (_consoleLock)
        {
            bool isAttached = false;
            try
            {
                NativeMethods.FreeConsole();

                isAttached = NativeMethods.AttachConsole((uint)_process.Id);
                if (!isAttached)
                {
                    int err = Marshal.GetLastWin32Error();
                    SimpleLogger.Log($"[ReadConsole] AttachConsole failed for PID {_process.Id}, Error: {err}");
                    return "Cannot read terminal content.";
                }

                IntPtr hStdOut = NativeMethods.GetStdHandle(NativeMethods.STD_OUTPUT_HANDLE);
                if (hStdOut == IntPtr.Zero || hStdOut == (IntPtr)(-1))
                {
                    return "Cannot read terminal content.";
                }

                if (!NativeMethods.GetConsoleScreenBufferInfo(hStdOut, out var csbi))
                {
                    return "Cannot read terminal content.";
                }

                short width = csbi.dwSize.X;
                short height = csbi.dwSize.Y;
                short contentBottom = GetSnapshotContentBottom(csbi, height);
                short linesToRead = height;
                if (tailLineCount.HasValue && tailLineCount.Value > 0)
                {
                    int paddedTailCount = Math.Max(tailLineCount.Value * 2, tailLineCount.Value);
                    linesToRead = (short)Math.Min(contentBottom + 1, paddedTailCount);
                }
                else
                {
                    linesToRead = (short)Math.Min(height, contentBottom + 1);
                }

                short startY = (short)Math.Max(0, contentBottom - linesToRead + 1);
                char[] buffer = new char[width];

                for (short y = startY; y <= contentBottom; y++)
                {
                    NativeMethods.COORD coord = new NativeMethods.COORD(0, y);
                    if (NativeMethods.ReadConsoleOutputCharacter(hStdOut, buffer, (uint)width, coord, out var charsRead))
                    {
                        string line = new string(buffer, 0, (int)charsRead).TrimEnd('\0', ' ', '\t', '\r', '\n');
                        lines.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, tailLineCount.HasValue ? "ReadTerminalTailSnapshot" : "ReadTerminalContent");
                return "Cannot read terminal content.";
            }
            finally
            {
                if (isAttached)
                {
                    NativeMethods.FreeConsole();
                }
            }
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        if (tailLineCount.HasValue && tailLineCount.Value > 0 && lines.Count > tailLineCount.Value)
        {
            lines = lines.Skip(lines.Count - tailLineCount.Value).ToList();
        }

        return string.Join(Environment.NewLine, lines).TrimEnd('\r', '\n', ' ');
    }

    internal static short GetSnapshotContentBottom(NativeMethods.CONSOLE_SCREEN_BUFFER_INFO csbi, short bufferHeight)
    {
        short maxValidRow = (short)Math.Max(0, bufferHeight - 1);
        short cursorRow = Math.Clamp(csbi.dwCursorPosition.Y, (short)0, maxValidRow);
        short windowBottom = Math.Clamp(csbi.srWindow.Bottom, (short)0, maxValidRow);
        return (short)Math.Max(cursorRow, windowBottom);
    }

    /// <summary>
    /// Executes the startup command if one is configured for this terminal.
    /// </summary>
    private static string ResolveShellFileName(string shellType)
    {
        // Build the preferred shell chain based on shellType
        string[] candidates = shellType?.ToLowerInvariant() switch
        {
            "pwsh" or "pwsh.exe"             => ["pwsh.exe", "powershell.exe", "cmd.exe"],  // PS7 → PS5.1 → cmd
            "powershell" or "powershell.exe" => ["powershell.exe", "cmd.exe"],              // PS5.1 → cmd
            _                                => ["cmd.exe"]                                 // cmd (always exists)
        };

        foreach (var exe in candidates)
        {
            if (IsExecutableAvailable(exe))
            {
                if (exe != candidates[0])
                    SimpleLogger.Log($"[ShellFallback] '{candidates[0]}' not found, falling back to '{exe}'");
                return exe;
            }
        }

        // Ultimate fallback (should never reach here on Windows)
        SimpleLogger.Log("[ShellFallback] All candidates unavailable, forcing cmd.exe");
        return "cmd.exe";
    }

    /// <summary>
    /// Checks if an executable is available via PATH or as an absolute path.
    /// </summary>
    private static bool IsExecutableAvailable(string exeName)
    {
        // 1. Absolute path check
        if (Path.IsPathRooted(exeName))
            return File.Exists(exeName);

        // 2. Search in PATH environment variable
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            try
            {
                var fullPath = Path.Combine(dir.Trim(), exeName);
                if (File.Exists(fullPath))
                    return true;
            }
            catch { /* skip invalid path entries */ }
        }

        // 3. Check common well-known locations for pwsh
        if (exeName.Equals("pwsh.exe", StringComparison.OrdinalIgnoreCase))
        {
            var commonPaths = new[]
            {
                @"C:\Program Files\PowerShell\7\pwsh.exe",
                @"C:\Program Files (x86)\PowerShell\7\pwsh.exe",
            };
            if (commonPaths.Any(File.Exists))
                return true;
        }

        return false;
    }

    private void ReloadDraftsFromStorage()
    {
        if (_draftStorageService == null || string.IsNullOrWhiteSpace(DraftStorageKey))
        {
            InputDrafts = new ObservableCollection<TerminalDraft>();
            _loadedDraftId = null;
            return;
        }

        IReadOnlyList<TerminalDraft> drafts = _draftStorageService.GetDrafts(DraftStorageKey);
        InputDrafts = new ObservableCollection<TerminalDraft>(drafts);

        if (!string.IsNullOrWhiteSpace(_loadedDraftId) && !InputDrafts.Any(draft => string.Equals(draft.Id, _loadedDraftId, StringComparison.Ordinal)))
        {
            _loadedDraftId = null;
        }
    }

    private void MoveDraftInternal(TerminalDraft? draft, int offset, TerminalUserInteractionKind interactionKind)
    {
        if (draft == null || _draftStorageService == null || string.IsNullOrWhiteSpace(DraftStorageKey))
        {
            return;
        }

        if (_draftStorageService.MoveDraft(DraftStorageKey, draft.Id, offset))
        {
            ReloadDraftsFromStorage();
            RaiseUserInteraction(interactionKind);
        }
    }

    private int FindDraftIndex(string? draftId)
    {
        if (string.IsNullOrWhiteSpace(draftId))
        {
            return -1;
        }

        for (int i = 0; i < InputDrafts.Count; i++)
        {
            if (string.Equals(InputDrafts[i].Id, draftId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private bool SendCommandCore(string? commandText, TerminalSendOrigin origin, TerminalDraft? associatedDraft)
    {
        if (commandText == null)
        {
            return false;
        }

        string commandToSend = commandText;
        bool sendSucceeded = false;

        lock (_consoleLock)
        {
            try
            {
                if (_process == null || _process.HasExited)
                {
                    SimpleLogger.Log("SendCommand aborted: Process is null or exited.");
                    return false;
                }

                if (origin != TerminalSendOrigin.Startup)
                {
                    TerminalShell.Services.HistoryService.Save(commandToSend, TerminalHistorySaveFolder, Name);
                }

                NativeMethods.FreeConsole();

                bool attached = NativeMethods.AttachConsole((uint)_process.Id);
                SimpleLogger.Log($"SendCommand: PID={_process.Id}, AttachConsole={attached}, InputLen={commandToSend.Length}, Origin={origin}");
                if (attached)
                {
                    IntPtr hStdIn = NativeMethods.GetStdHandle(NativeMethods.STD_INPUT_HANDLE);
                    if (hStdIn != IntPtr.Zero && hStdIn != (IntPtr)(-1))
                    {
                        List<NativeMethods.INPUT_RECORD> textRecords = new List<NativeMethods.INPUT_RECORD>();
                        foreach (char c in commandToSend)
                        {
                            textRecords.Add(new NativeMethods.INPUT_RECORD
                            {
                                EventType = NativeMethods.KEY_EVENT,
                                KeyEvent = new NativeMethods.KEY_EVENT_RECORD
                                {
                                    bKeyDown = 1, wRepeatCount = 1, wVirtualKeyCode = 0xE7, UnicodeChar = c
                                }
                            });
                            textRecords.Add(new NativeMethods.INPUT_RECORD
                            {
                                EventType = NativeMethods.KEY_EVENT,
                                KeyEvent = new NativeMethods.KEY_EVENT_RECORD
                                {
                                    bKeyDown = 0, wRepeatCount = 1, wVirtualKeyCode = 0xE7, UnicodeChar = c
                                }
                            });
                        }

                        int written;
                        if (textRecords.Count > 0)
                        {
                            NativeMethods.WriteConsoleInput(hStdIn, textRecords.ToArray(), textRecords.Count, out written);
                        }

                        int inputDelayMs = ConfigManager.Instance.Config.TerminalInputDelayMs;
                        if (inputDelayMs < 0)
                        {
                            inputDelayMs = 0;
                        }

                        Thread.Sleep(inputDelayMs);

                        ushort vkReturn = 0x0D;
                        ushort scanCodeReturn = 0x1C;
                        char charReturn = '\r';

                        NativeMethods.INPUT_RECORD[] enterDown = new NativeMethods.INPUT_RECORD[1];
                        enterDown[0] = new NativeMethods.INPUT_RECORD
                        {
                            EventType = NativeMethods.KEY_EVENT,
                            KeyEvent = new NativeMethods.KEY_EVENT_RECORD
                            {
                                bKeyDown = 1, wRepeatCount = 1, wVirtualKeyCode = vkReturn, wVirtualScanCode = scanCodeReturn, UnicodeChar = charReturn
                            }
                        };
                        if (!NativeMethods.WriteConsoleInput(hStdIn, enterDown, 1, out written))
                        {
                            SimpleLogger.Log($"WriteConsoleInput (Enter Down) FAILED: {Marshal.GetLastWin32Error()}");
                        }

                        Thread.Sleep(20);

                        NativeMethods.INPUT_RECORD[] enterUp = new NativeMethods.INPUT_RECORD[1];
                        enterUp[0] = new NativeMethods.INPUT_RECORD
                        {
                            EventType = NativeMethods.KEY_EVENT,
                            KeyEvent = new NativeMethods.KEY_EVENT_RECORD
                            {
                                bKeyDown = 0, wRepeatCount = 1, wVirtualKeyCode = vkReturn, wVirtualScanCode = scanCodeReturn, UnicodeChar = charReturn
                            }
                        };
                        NativeMethods.WriteConsoleInput(hStdIn, enterUp, 1, out written);
                        sendSucceeded = true;
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "ConsoleInputInjection");
            }
            finally
            {
                NativeMethods.FreeConsole();
            }
        }

        if (sendSucceeded && origin != TerminalSendOrigin.Startup && !string.IsNullOrWhiteSpace(commandToSend))
        {
            UserCommandSent?.Invoke(this, new TerminalCommandSentEventArgs
            {
                CommandText = commandToSend,
                Origin = origin,
                DraftId = associatedDraft?.Id
            });
            DeleteDraftAfterSuccessfulSend(commandToSend, associatedDraft);
        }

        if (origin == TerminalSendOrigin.Manual)
        {
            _suppressedTextEditNotifications++;
            InputBuffer = string.Empty;
        }

        return sendSucceeded;
    }

    private void DeleteDraftAfterSuccessfulSend(string sentText, TerminalDraft? draftToDelete)
    {
        if (_draftStorageService == null || string.IsNullOrWhiteSpace(DraftStorageKey) || draftToDelete == null)
        {
            return;
        }

        if (!AreDraftTextsEquivalent(sentText, draftToDelete.Text))
        {
            return;
        }

        if (_draftStorageService.DeleteDraft(DraftStorageKey, draftToDelete.Id))
        {
            ReloadDraftsFromStorage();
        }
    }

    private static bool AreDraftTextsEquivalent(string left, string right)
    {
        string strictLeft = NormalizeDraftText(left, collapseLineBreaks: false);
        string strictRight = NormalizeDraftText(right, collapseLineBreaks: false);
        if (string.Equals(strictLeft, strictRight, StringComparison.Ordinal))
        {
            return true;
        }

        string looseLeft = NormalizeDraftText(left, collapseLineBreaks: true);
        string looseRight = NormalizeDraftText(right, collapseLineBreaks: true);
        return string.Equals(looseLeft, looseRight, StringComparison.Ordinal);
    }

    private static string NormalizeDraftText(string? text, bool collapseLineBreaks)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\u00A0', ' ');
        StringBuilder builder = new();
        foreach (char c in normalized)
        {
            if (c is '\u200B' or '\u200C' or '\u200D' or '\uFEFF')
            {
                continue;
            }

            if (collapseLineBreaks && c == '\n')
            {
                builder.Append(' ');
                continue;
            }

            builder.Append(char.IsWhiteSpace(c) && c != '\n' ? ' ' : c);
        }

        if (collapseLineBreaks)
        {
            return ConsecutiveWhitespaceRegex.Replace(builder.ToString(), " ").Trim();
        }

        List<string> lines = builder
            .ToString()
            .Split('\n')
            .Select(line => ConsecutiveWhitespaceRegex.Replace(line.Trim(), " "))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        return string.Join("\n", lines).Trim();
    }

    private void RaiseUserInteraction(TerminalUserInteractionKind kind)
    {
        UserInteractionOccurred?.Invoke(this, kind);
    }
}
