using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using TerminalShell.Core;
using TerminalShell.Models;

namespace TerminalShell.ViewModels;

public sealed class TerminalHeaderContextMenuViewModel
{
    private readonly MainViewModel _owner;

    public TerminalHeaderContextMenuViewModel(MainViewModel owner, TerminalSession session)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        SimpleLogger.Log($"[HeaderContextMenuVM] Constructing snapshot for session '{session.Name}'. SourceItems={owner.GroupedMenuTerminals.Count}");

        OpenSettingsCommand = owner.OpenSettingsCommand;
        OpenWorkingDirectoryMenuText = session.OpenWorkingDirectoryMenuText;
        OpenWorkingDirectoryCommand = new BoundCommand(owner.OpenWorkingDirectoryCommand, session);
        ReloadTerminalCommand = new BoundCommand(owner.ReloadTerminalCommand, session);
        HideCommand = new BoundCommand(owner.HideTerminalCommand, session);
        ToggleFullScreenCommand = new BoundCommand(owner.ToggleFullScreenCommand, session);
        GroupedMenuTerminals = BuildGroupedMenuTerminals(owner.GroupedMenuTerminals, owner.ShowTerminalCommand);
        SimpleLogger.Log($"[HeaderContextMenuVM] Snapshot ready for session '{session.Name}'. SnapshotItems={GroupedMenuTerminals.Count}");
    }

    public ICommand OpenSettingsCommand { get; }
    public string OpenWorkingDirectoryMenuText { get; }
    public ICommand OpenWorkingDirectoryCommand { get; }
    public ICommand ReloadTerminalCommand { get; }
    public ICommand HideCommand { get; }
    public ICommand ToggleFullScreenCommand { get; }
    public IReadOnlyList<TerminalHeaderContextMenuItemViewModel> GroupedMenuTerminals { get; }

    public bool IsTopmost
    {
        get => _owner.IsTopmost;
        set => _owner.IsTopmost = value;
    }

    public bool IsAllTaskAlertsEnabled
    {
        get => _owner.IsAllTaskAlertsEnabled;
        set => _owner.IsAllTaskAlertsEnabled = value;
    }

    internal static IReadOnlyList<TerminalHeaderContextMenuItemViewModel> BuildGroupedMenuTerminals(
        IEnumerable<ContextMenuItemViewModel> items,
        ICommand showTerminalCommand)
    {
        List<ContextMenuItemViewModel> itemList = items.ToList();
        int headerCount = itemList.Count(item => item.IsHeader);
        int terminalCount = itemList.Count - headerCount;
        SimpleLogger.Log($"[HeaderContextMenuVM] BuildGroupedMenuTerminals SourceCount={itemList.Count} HeaderCount={headerCount} TerminalCount={terminalCount}");

        return itemList.Select(item =>
        {
            bool isDisplayed = item.Terminal?.ShowInMainWindow == true;
            bool isEnabled = !item.IsHeader && item.Terminal != null && !isDisplayed;

            return new TerminalHeaderContextMenuItemViewModel
            {
                HeaderText = isDisplayed ? $"{item.Name} (Displayed)" : item.Name,
                IsHeader = item.IsHeader,
                IsEnabled = isEnabled,
                Command = isEnabled ? new BoundCommand(showTerminalCommand, item.Terminal) : null
            };
        }).ToList();
    }
}
