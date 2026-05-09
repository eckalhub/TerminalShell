using CommunityToolkit.Mvvm.ComponentModel;

namespace TerminalShell.ViewModels;

public partial class GroupHeaderViewModel : ObservableObject
{
    [ObservableProperty]
    private string _groupName = "";

    [ObservableProperty]
    private bool _isExpanded = true;
    
    // Alias Name property to match typical bindings if needed
    public string Name => GroupName;
}
