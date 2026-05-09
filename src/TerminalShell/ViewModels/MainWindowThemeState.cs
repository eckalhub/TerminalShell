using CommunityToolkit.Mvvm.ComponentModel;
using TerminalShell.Core;
using TerminalShell.Models;
using Brush = System.Windows.Media.Brush;

namespace TerminalShell.ViewModels;

public partial class MainWindowThemeState : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowBackgroundBrush))]
    private string _windowBackgroundColor = MainWindowThemeConfig.DefaultWindowBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SessionBorderBrush))]
    private string _sessionBorderColor = MainWindowThemeConfig.DefaultSessionBorderColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SessionHeaderBackgroundBrush))]
    private string _sessionHeaderBackgroundColor = MainWindowThemeConfig.DefaultSessionHeaderBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SessionHeaderForegroundBrush))]
    private string _sessionHeaderForegroundColor = MainWindowThemeConfig.DefaultSessionHeaderForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResizeHandleHoverBrush))]
    private string _resizeHandleHoverColor = MainWindowThemeConfig.DefaultResizeHandleHoverColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputContainerBackgroundBrush))]
    private string _inputContainerBackgroundColor = MainWindowThemeConfig.DefaultInputContainerBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputTextBoxBackgroundBrush))]
    private string _inputTextBoxBackgroundColor = MainWindowThemeConfig.DefaultInputTextBoxBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputTextBoxForegroundBrush))]
    private string _inputTextBoxForegroundColor = MainWindowThemeConfig.DefaultInputTextBoxForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputTextBoxBorderBrush))]
    private string _inputTextBoxBorderColor = MainWindowThemeConfig.DefaultInputTextBoxBorderColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputWatermarkForegroundBrush))]
    private string _inputWatermarkForegroundColor = MainWindowThemeConfig.DefaultInputWatermarkForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WaitForUserInputHighlightBrush))]
    private string _waitForUserInputHighlightColor = MainWindowThemeConfig.DefaultWaitForUserInputHighlightColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WaitForUserInputContainerBackgroundBrush))]
    private string _waitForUserInputContainerBackgroundColor = MainWindowThemeConfig.DefaultWaitForUserInputContainerBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WaitForUserInputTextBoxBackgroundBrush))]
    private string _waitForUserInputTextBoxBackgroundColor = MainWindowThemeConfig.DefaultWaitForUserInputTextBoxBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WaitForUserInputTextBoxForegroundBrush))]
    private string _waitForUserInputTextBoxForegroundColor = MainWindowThemeConfig.DefaultWaitForUserInputTextBoxForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WaitForUserInputWatermarkForegroundBrush))]
    private string _waitForUserInputWatermarkForegroundColor = MainWindowThemeConfig.DefaultWaitForUserInputWatermarkForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SendButtonBackgroundBrush))]
    private string _sendButtonBackgroundColor = MainWindowThemeConfig.DefaultSendButtonBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SendButtonForegroundBrush))]
    private string _sendButtonForegroundColor = MainWindowThemeConfig.DefaultSendButtonForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SendButtonHoverBackgroundBrush))]
    private string _sendButtonHoverBackgroundColor = MainWindowThemeConfig.DefaultSendButtonHoverBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SendButtonHoverForegroundBrush))]
    private string _sendButtonHoverForegroundColor = MainWindowThemeConfig.DefaultSendButtonHoverForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HistoryButtonBackgroundBrush))]
    private string _historyButtonBackgroundColor = MainWindowThemeConfig.DefaultHistoryButtonBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HistoryButtonForegroundBrush))]
    private string _historyButtonForegroundColor = MainWindowThemeConfig.DefaultHistoryButtonForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HistoryButtonHoverBackgroundBrush))]
    private string _historyButtonHoverBackgroundColor = MainWindowThemeConfig.DefaultHistoryButtonHoverBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HistoryButtonHoverForegroundBrush))]
    private string _historyButtonHoverForegroundColor = MainWindowThemeConfig.DefaultHistoryButtonHoverForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DraftButtonBackgroundBrush))]
    private string _draftButtonBackgroundColor = MainWindowThemeConfig.DefaultDraftButtonBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DraftButtonForegroundBrush))]
    private string _draftButtonForegroundColor = MainWindowThemeConfig.DefaultDraftButtonForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DraftButtonActiveBackgroundBrush))]
    private string _draftButtonActiveBackgroundColor = MainWindowThemeConfig.DefaultDraftButtonActiveBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DraftButtonActiveForegroundBrush))]
    private string _draftButtonActiveForegroundColor = MainWindowThemeConfig.DefaultDraftButtonActiveForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextMenuBackgroundBrush))]
    private string _contextMenuBackgroundColor = MainWindowThemeConfig.DefaultContextMenuBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextMenuGutterBackgroundBrush))]
    private string _contextMenuGutterBackgroundColor = MainWindowThemeConfig.DefaultContextMenuGutterBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextMenuForegroundBrush))]
    private string _contextMenuForegroundColor = MainWindowThemeConfig.DefaultContextMenuForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextMenuBorderBrush))]
    private string _contextMenuBorderColor = MainWindowThemeConfig.DefaultContextMenuBorderColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextMenuSeparatorBrush))]
    private string _contextMenuSeparatorColor = MainWindowThemeConfig.DefaultContextMenuSeparatorColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextMenuShortcutForegroundBrush))]
    private string _contextMenuShortcutForegroundColor = MainWindowThemeConfig.DefaultContextMenuShortcutForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextMenuAccentForegroundBrush))]
    private string _contextMenuAccentForegroundColor = MainWindowThemeConfig.DefaultContextMenuAccentForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextMenuHighlightBackgroundBrush))]
    private string _contextMenuHighlightBackgroundColor = MainWindowThemeConfig.DefaultContextMenuHighlightBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextMenuGroupHeaderForegroundBrush))]
    private string _contextMenuGroupHeaderForegroundColor = MainWindowThemeConfig.DefaultContextMenuGroupHeaderForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPopupBackgroundBrush))]
    private string _commandPopupBackgroundColor = MainWindowThemeConfig.DefaultCommandPopupBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPopupBorderBrush))]
    private string _commandPopupBorderColor = MainWindowThemeConfig.DefaultCommandPopupBorderColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPopupHeaderBackgroundBrush))]
    private string _commandPopupHeaderBackgroundColor = MainWindowThemeConfig.DefaultCommandPopupHeaderBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPopupHeaderForegroundBrush))]
    private string _commandPopupHeaderForegroundColor = MainWindowThemeConfig.DefaultCommandPopupHeaderForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPopupItemForegroundBrush))]
    private string _commandPopupItemForegroundColor = MainWindowThemeConfig.DefaultCommandPopupItemForegroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPopupItemHoverBackgroundBrush))]
    private string _commandPopupItemHoverBackgroundColor = MainWindowThemeConfig.DefaultCommandPopupItemHoverBackgroundColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CommandPopupItemSelectedBackgroundBrush))]
    private string _commandPopupItemSelectedBackgroundColor = MainWindowThemeConfig.DefaultCommandPopupItemSelectedBackgroundColor;

    public Brush WindowBackgroundBrush => UiColorHelper.CreateSolidBrush(WindowBackgroundColor, MainWindowThemeConfig.DefaultWindowBackgroundColor);
    public Brush SessionBorderBrush => UiColorHelper.CreateSolidBrush(SessionBorderColor, MainWindowThemeConfig.DefaultSessionBorderColor);
    public Brush SessionHeaderBackgroundBrush => UiColorHelper.CreateSolidBrush(SessionHeaderBackgroundColor, MainWindowThemeConfig.DefaultSessionHeaderBackgroundColor);
    public Brush SessionHeaderForegroundBrush => UiColorHelper.CreateSolidBrush(SessionHeaderForegroundColor, MainWindowThemeConfig.DefaultSessionHeaderForegroundColor);
    public Brush ResizeHandleHoverBrush => UiColorHelper.CreateSolidBrush(ResizeHandleHoverColor, MainWindowThemeConfig.DefaultResizeHandleHoverColor);
    public Brush InputContainerBackgroundBrush => UiColorHelper.CreateSolidBrush(InputContainerBackgroundColor, MainWindowThemeConfig.DefaultInputContainerBackgroundColor);
    public Brush InputTextBoxBackgroundBrush => UiColorHelper.CreateSolidBrush(InputTextBoxBackgroundColor, MainWindowThemeConfig.DefaultInputTextBoxBackgroundColor);
    public Brush InputTextBoxForegroundBrush => UiColorHelper.CreateSolidBrush(InputTextBoxForegroundColor, MainWindowThemeConfig.DefaultInputTextBoxForegroundColor);
    public Brush InputTextBoxBorderBrush => UiColorHelper.CreateSolidBrush(InputTextBoxBorderColor, MainWindowThemeConfig.DefaultInputTextBoxBorderColor);
    public Brush InputWatermarkForegroundBrush => UiColorHelper.CreateSolidBrush(InputWatermarkForegroundColor, MainWindowThemeConfig.DefaultInputWatermarkForegroundColor);
    public Brush WaitForUserInputHighlightBrush => UiColorHelper.CreateSolidBrush(WaitForUserInputHighlightColor, MainWindowThemeConfig.DefaultWaitForUserInputHighlightColor);
    public Brush WaitForUserInputContainerBackgroundBrush => UiColorHelper.CreateSolidBrush(WaitForUserInputContainerBackgroundColor, MainWindowThemeConfig.DefaultWaitForUserInputContainerBackgroundColor);
    public Brush WaitForUserInputTextBoxBackgroundBrush => UiColorHelper.CreateSolidBrush(WaitForUserInputTextBoxBackgroundColor, MainWindowThemeConfig.DefaultWaitForUserInputTextBoxBackgroundColor);
    public Brush WaitForUserInputTextBoxForegroundBrush => UiColorHelper.CreateSolidBrush(WaitForUserInputTextBoxForegroundColor, MainWindowThemeConfig.DefaultWaitForUserInputTextBoxForegroundColor);
    public Brush WaitForUserInputWatermarkForegroundBrush => UiColorHelper.CreateSolidBrush(WaitForUserInputWatermarkForegroundColor, MainWindowThemeConfig.DefaultWaitForUserInputWatermarkForegroundColor);
    public Brush SendButtonBackgroundBrush => UiColorHelper.CreateSolidBrush(SendButtonBackgroundColor, MainWindowThemeConfig.DefaultSendButtonBackgroundColor);
    public Brush SendButtonForegroundBrush => UiColorHelper.CreateSolidBrush(SendButtonForegroundColor, MainWindowThemeConfig.DefaultSendButtonForegroundColor);
    public Brush SendButtonHoverBackgroundBrush => UiColorHelper.CreateSolidBrush(SendButtonHoverBackgroundColor, MainWindowThemeConfig.DefaultSendButtonHoverBackgroundColor);
    public Brush SendButtonHoverForegroundBrush => UiColorHelper.CreateSolidBrush(SendButtonHoverForegroundColor, MainWindowThemeConfig.DefaultSendButtonHoverForegroundColor);
    public Brush HistoryButtonBackgroundBrush => UiColorHelper.CreateSolidBrush(HistoryButtonBackgroundColor, MainWindowThemeConfig.DefaultHistoryButtonBackgroundColor);
    public Brush HistoryButtonForegroundBrush => UiColorHelper.CreateSolidBrush(HistoryButtonForegroundColor, MainWindowThemeConfig.DefaultHistoryButtonForegroundColor);
    public Brush HistoryButtonHoverBackgroundBrush => UiColorHelper.CreateSolidBrush(HistoryButtonHoverBackgroundColor, MainWindowThemeConfig.DefaultHistoryButtonHoverBackgroundColor);
    public Brush HistoryButtonHoverForegroundBrush => UiColorHelper.CreateSolidBrush(HistoryButtonHoverForegroundColor, MainWindowThemeConfig.DefaultHistoryButtonHoverForegroundColor);
    public Brush DraftButtonBackgroundBrush => UiColorHelper.CreateSolidBrush(DraftButtonBackgroundColor, MainWindowThemeConfig.DefaultDraftButtonBackgroundColor);
    public Brush DraftButtonForegroundBrush => UiColorHelper.CreateSolidBrush(DraftButtonForegroundColor, MainWindowThemeConfig.DefaultDraftButtonForegroundColor);
    public Brush DraftButtonActiveBackgroundBrush => UiColorHelper.CreateSolidBrush(DraftButtonActiveBackgroundColor, MainWindowThemeConfig.DefaultDraftButtonActiveBackgroundColor);
    public Brush DraftButtonActiveForegroundBrush => UiColorHelper.CreateSolidBrush(DraftButtonActiveForegroundColor, MainWindowThemeConfig.DefaultDraftButtonActiveForegroundColor);
    public Brush ContextMenuBackgroundBrush => UiColorHelper.CreateSolidBrush(ContextMenuBackgroundColor, MainWindowThemeConfig.DefaultContextMenuBackgroundColor);
    public Brush ContextMenuGutterBackgroundBrush => UiColorHelper.CreateSolidBrush(ContextMenuGutterBackgroundColor, MainWindowThemeConfig.DefaultContextMenuGutterBackgroundColor);
    public Brush ContextMenuForegroundBrush => UiColorHelper.CreateSolidBrush(ContextMenuForegroundColor, MainWindowThemeConfig.DefaultContextMenuForegroundColor);
    public Brush ContextMenuBorderBrush => UiColorHelper.CreateSolidBrush(ContextMenuBorderColor, MainWindowThemeConfig.DefaultContextMenuBorderColor);
    public Brush ContextMenuSeparatorBrush => UiColorHelper.CreateSolidBrush(ContextMenuSeparatorColor, MainWindowThemeConfig.DefaultContextMenuSeparatorColor);
    public Brush ContextMenuShortcutForegroundBrush => UiColorHelper.CreateSolidBrush(ContextMenuShortcutForegroundColor, MainWindowThemeConfig.DefaultContextMenuShortcutForegroundColor);
    public Brush ContextMenuAccentForegroundBrush => UiColorHelper.CreateSolidBrush(ContextMenuAccentForegroundColor, MainWindowThemeConfig.DefaultContextMenuAccentForegroundColor);
    public Brush ContextMenuHighlightBackgroundBrush => UiColorHelper.CreateSolidBrush(ContextMenuHighlightBackgroundColor, MainWindowThemeConfig.DefaultContextMenuHighlightBackgroundColor);
    public Brush ContextMenuGroupHeaderForegroundBrush => UiColorHelper.CreateSolidBrush(ContextMenuGroupHeaderForegroundColor, MainWindowThemeConfig.DefaultContextMenuGroupHeaderForegroundColor);
    public Brush CommandPopupBackgroundBrush => UiColorHelper.CreateSolidBrush(CommandPopupBackgroundColor, MainWindowThemeConfig.DefaultCommandPopupBackgroundColor);
    public Brush CommandPopupBorderBrush => UiColorHelper.CreateSolidBrush(CommandPopupBorderColor, MainWindowThemeConfig.DefaultCommandPopupBorderColor);
    public Brush CommandPopupHeaderBackgroundBrush => UiColorHelper.CreateSolidBrush(CommandPopupHeaderBackgroundColor, MainWindowThemeConfig.DefaultCommandPopupHeaderBackgroundColor);
    public Brush CommandPopupHeaderForegroundBrush => UiColorHelper.CreateSolidBrush(CommandPopupHeaderForegroundColor, MainWindowThemeConfig.DefaultCommandPopupHeaderForegroundColor);
    public Brush CommandPopupItemForegroundBrush => UiColorHelper.CreateSolidBrush(CommandPopupItemForegroundColor, MainWindowThemeConfig.DefaultCommandPopupItemForegroundColor);
    public Brush CommandPopupItemHoverBackgroundBrush => UiColorHelper.CreateSolidBrush(CommandPopupItemHoverBackgroundColor, MainWindowThemeConfig.DefaultCommandPopupItemHoverBackgroundColor);
    public Brush CommandPopupItemSelectedBackgroundBrush => UiColorHelper.CreateSolidBrush(CommandPopupItemSelectedBackgroundColor, MainWindowThemeConfig.DefaultCommandPopupItemSelectedBackgroundColor);

    public void Load(MainWindowThemeConfig? theme)
    {
        MainWindowThemeConfig normalized = MainWindowThemeConfig.Normalize(theme);

        WindowBackgroundColor = normalized.WindowBackgroundColor;
        SessionBorderColor = normalized.SessionBorderColor;
        SessionHeaderBackgroundColor = normalized.SessionHeaderBackgroundColor;
        SessionHeaderForegroundColor = normalized.SessionHeaderForegroundColor;
        ResizeHandleHoverColor = normalized.ResizeHandleHoverColor;
        InputContainerBackgroundColor = normalized.InputContainerBackgroundColor;
        InputTextBoxBackgroundColor = normalized.InputTextBoxBackgroundColor;
        InputTextBoxForegroundColor = normalized.InputTextBoxForegroundColor;
        InputTextBoxBorderColor = normalized.InputTextBoxBorderColor;
        InputWatermarkForegroundColor = normalized.InputWatermarkForegroundColor;
        WaitForUserInputHighlightColor = normalized.WaitForUserInputHighlightColor;
        WaitForUserInputContainerBackgroundColor = normalized.WaitForUserInputContainerBackgroundColor;
        WaitForUserInputTextBoxBackgroundColor = normalized.WaitForUserInputTextBoxBackgroundColor;
        WaitForUserInputTextBoxForegroundColor = normalized.WaitForUserInputTextBoxForegroundColor;
        WaitForUserInputWatermarkForegroundColor = normalized.WaitForUserInputWatermarkForegroundColor;
        SendButtonBackgroundColor = normalized.SendButtonBackgroundColor;
        SendButtonForegroundColor = normalized.SendButtonForegroundColor;
        SendButtonHoverBackgroundColor = normalized.SendButtonHoverBackgroundColor;
        SendButtonHoverForegroundColor = normalized.SendButtonHoverForegroundColor;
        HistoryButtonBackgroundColor = normalized.HistoryButtonBackgroundColor;
        HistoryButtonForegroundColor = normalized.HistoryButtonForegroundColor;
        HistoryButtonHoverBackgroundColor = normalized.HistoryButtonHoverBackgroundColor;
        HistoryButtonHoverForegroundColor = normalized.HistoryButtonHoverForegroundColor;
        DraftButtonBackgroundColor = normalized.DraftButtonBackgroundColor;
        DraftButtonForegroundColor = normalized.DraftButtonForegroundColor;
        DraftButtonActiveBackgroundColor = normalized.DraftButtonActiveBackgroundColor;
        DraftButtonActiveForegroundColor = normalized.DraftButtonActiveForegroundColor;
        ContextMenuBackgroundColor = normalized.ContextMenuBackgroundColor;
        ContextMenuGutterBackgroundColor = normalized.ContextMenuGutterBackgroundColor;
        ContextMenuForegroundColor = normalized.ContextMenuForegroundColor;
        ContextMenuBorderColor = normalized.ContextMenuBorderColor;
        ContextMenuSeparatorColor = normalized.ContextMenuSeparatorColor;
        ContextMenuShortcutForegroundColor = normalized.ContextMenuShortcutForegroundColor;
        ContextMenuAccentForegroundColor = normalized.ContextMenuAccentForegroundColor;
        ContextMenuHighlightBackgroundColor = normalized.ContextMenuHighlightBackgroundColor;
        ContextMenuGroupHeaderForegroundColor = normalized.ContextMenuGroupHeaderForegroundColor;
        CommandPopupBackgroundColor = normalized.CommandPopupBackgroundColor;
        CommandPopupBorderColor = normalized.CommandPopupBorderColor;
        CommandPopupHeaderBackgroundColor = normalized.CommandPopupHeaderBackgroundColor;
        CommandPopupHeaderForegroundColor = normalized.CommandPopupHeaderForegroundColor;
        CommandPopupItemForegroundColor = normalized.CommandPopupItemForegroundColor;
        CommandPopupItemHoverBackgroundColor = normalized.CommandPopupItemHoverBackgroundColor;
        CommandPopupItemSelectedBackgroundColor = normalized.CommandPopupItemSelectedBackgroundColor;
    }
}
