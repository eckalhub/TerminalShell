using System.Windows.Input;

namespace TerminalShell.ViewModels;

public sealed class TerminalHeaderContextMenuItemViewModel
{
    public string HeaderText { get; init; } = string.Empty;
    public bool IsHeader { get; init; }
    public bool IsEnabled { get; init; }
    public ICommand? Command { get; init; }
}
