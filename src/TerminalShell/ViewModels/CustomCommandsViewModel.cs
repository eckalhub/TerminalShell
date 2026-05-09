using CommunityToolkit.Mvvm.ComponentModel;
using TerminalShell.Core;
using TerminalShell.Core.Config;

namespace TerminalShell.ViewModels;

public partial class CustomCommandsViewModel : ObservableObject
{
    public string Name => "Custom Commands";
    public string GroupName => "";

    [ObservableProperty]
    private string _customCommandsString = CustomCommandDefaults.Template;

    [ObservableProperty]
    private int _customCommandMaxDisplayLength = 100;

    public void Load()
    {
        string configuredValue = ConfigManager.Instance.Config.CustomCommandsString ?? string.Empty;
        CustomCommandsString = CustomCommandDefaults.GetEffectiveValue(configuredValue);
        CustomCommandMaxDisplayLength = ConfigManager.Instance.Config.CustomCommandMaxDisplayLength;
    }

    public void Save()
    {
        ConfigManager.Instance.Config.CustomCommandsString = CustomCommandsString;
        ConfigManager.Instance.Config.CustomCommandMaxDisplayLength = CustomCommandMaxDisplayLength;
    }
}
