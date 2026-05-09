using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TerminalShell.Models;

public partial class TerminalConfig : ObservableObject
{
    public static string CreateDraftStorageKey() => Guid.NewGuid().ToString("N");

    [JsonPropertyName("name")]
    [ObservableProperty]
    private string _name = "Terminal new";

    [JsonPropertyName("terminalVoiceName")]
    [ObservableProperty]
    private string _terminalVoiceName = "";

    [JsonPropertyName("terminalHistorySaveFolder")]
    [ObservableProperty]
    private string _terminalHistorySaveFolder = "";

    [JsonPropertyName("lastInputSaveDirectory")]
    [ObservableProperty]
    private string _lastInputSaveDirectory = "";

    [JsonPropertyName("draftStorageKey")]
    [ObservableProperty]
    private string _draftStorageKey = "";

    [JsonPropertyName("enableAutoSubmitDraftQueueOnCompletion")]
    [ObservableProperty]
    private bool _enableAutoSubmitDraftQueueOnCompletion = false;

    [JsonPropertyName("workingDirectory")]
    [ObservableProperty]
    private string _workingDirectory = "";

    [JsonPropertyName("startupCommand")]
    [ObservableProperty]
    private string _startupCommand = "";

    [JsonPropertyName("showInMainWindow")]
    [ObservableProperty]
    private bool _showInMainWindow = false;

    [JsonPropertyName("shellType")]
    [ObservableProperty]
    private string _shellType = "pwsh.exe"; // "cmd.exe" / "powershell.exe" / "pwsh.exe"

    [JsonPropertyName("groupName")]
    [ObservableProperty]
    private string _groupName = "";

    [JsonPropertyName("notes")]
    [ObservableProperty]
    private string _notes = "";

    [JsonIgnore]
    [ObservableProperty]
    private bool _isVisible = true;
}
