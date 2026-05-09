using TerminalShell.Models;

namespace TerminalShell.ViewModels;

public class ContextMenuItemViewModel
{
    public bool IsHeader { get; set; }
    public string Name { get; set; } = string.Empty;
    public TerminalConfig? Terminal { get; set; }
}
