namespace TerminalShell.Services;

public sealed class RemoteWebConsoleHostStateChangedEventArgs : EventArgs
{
    public RemoteWebConsoleHostStateChangedEventArgs(
        bool isRunning,
        bool hasError,
        string titleSuffix,
        string statusText,
        bool shouldWarnUser,
        string warningMessage)
    {
        IsRunning = isRunning;
        HasError = hasError;
        TitleSuffix = titleSuffix ?? string.Empty;
        StatusText = statusText ?? string.Empty;
        ShouldWarnUser = shouldWarnUser;
        WarningMessage = warningMessage ?? string.Empty;
    }

    public bool IsRunning { get; }

    public bool HasError { get; }

    public string TitleSuffix { get; }

    public string StatusText { get; }

    public bool ShouldWarnUser { get; }

    public string WarningMessage { get; }
}
