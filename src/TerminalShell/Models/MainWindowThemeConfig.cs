using System;
using System.Text.Json.Serialization;
using TerminalShell.Core;

namespace TerminalShell.Models;

public sealed class MainWindowThemeConfig
{
    public const string DefaultWindowBackgroundColor = "#1E1E1E";
    public const string DefaultSessionBorderColor = "#3E3E42";
    public const string DefaultSessionHeaderBackgroundColor = "#2D2D30";
    public const string DefaultSessionHeaderForegroundColor = "#CCCCCC";
    public const string DefaultResizeHandleHoverColor = "#007ACC";
    public const string DefaultInputContainerBackgroundColor = "#2D2D30";
    public const string DefaultInputTextBoxBackgroundColor = "#252526";
    public const string DefaultInputTextBoxForegroundColor = "#FFFFFF";
    public const string DefaultInputTextBoxBorderColor = "#3E3E42";
    public const string DefaultInputWatermarkForegroundColor = "#808080";
    public const string DefaultWaitForUserInputHighlightColor = AppConfig.DefaultWaitForUserInputHighlightColor;
    public const string DefaultWaitForUserInputContainerBackgroundColor = "#2D2D30";
    public const string DefaultWaitForUserInputTextBoxBackgroundColor = "#009873";
    public const string DefaultWaitForUserInputTextBoxForegroundColor = "#000000";
    public const string DefaultWaitForUserInputWatermarkForegroundColor = "#48E58F";
    public const string DefaultSendButtonBackgroundColor = "#009873";
    public const string DefaultSendButtonForegroundColor = "#000000";
    public const string DefaultSendButtonHoverBackgroundColor = "#008867";
    public const string DefaultSendButtonHoverForegroundColor = "#BCBCBC";
    public const string DefaultHistoryButtonBackgroundColor = "#808080";
    public const string DefaultHistoryButtonForegroundColor = "#FFFFFF";
    public const string DefaultHistoryButtonHoverBackgroundColor = "#9A9A9A";
    public const string DefaultHistoryButtonHoverForegroundColor = "#3C3C3C";
    public const string DefaultDraftButtonBackgroundColor = "#505050";
    public const string DefaultDraftButtonForegroundColor = "#FFFFFF";
    public const string DefaultDraftButtonActiveBackgroundColor = "#009873";
    public const string DefaultDraftButtonActiveForegroundColor = "#000000";
    public const string DefaultContextMenuBackgroundColor = "#2F3035";
    public const string DefaultContextMenuGutterBackgroundColor = DefaultContextMenuBackgroundColor;
    public const string DefaultContextMenuForegroundColor = "#F5F5F5";
    public const string DefaultContextMenuBorderColor = "#3B3C41";
    public const string DefaultContextMenuSeparatorColor = "#45464B";
    public const string DefaultContextMenuShortcutForegroundColor = "#B6B7BD";
    public const string DefaultContextMenuAccentForegroundColor = "#1496F2";
    public const string DefaultContextMenuHighlightBackgroundColor = "#757579";
    public const string DefaultContextMenuGroupHeaderForegroundColor = "#D2D4DA";
    public const string DefaultCommandPopupBackgroundColor = "#2C2D31";
    public const string DefaultCommandPopupBorderColor = "#3B3C41";
    public const string DefaultCommandPopupHeaderBackgroundColor = "#313338";
    public const string DefaultCommandPopupHeaderForegroundColor = "#AEB0B7";
    public const string DefaultCommandPopupItemForegroundColor = "#F5F5F5";
    public const string DefaultCommandPopupItemHoverBackgroundColor = "#3E4046";
    public const string DefaultCommandPopupItemSelectedBackgroundColor = "#465361";

    [JsonPropertyName("windowBackgroundColor")]
    public string WindowBackgroundColor { get; set; } = DefaultWindowBackgroundColor;

    [JsonPropertyName("sessionBorderColor")]
    public string SessionBorderColor { get; set; } = DefaultSessionBorderColor;

    [JsonPropertyName("sessionHeaderBackgroundColor")]
    public string SessionHeaderBackgroundColor { get; set; } = DefaultSessionHeaderBackgroundColor;

    [JsonPropertyName("sessionHeaderForegroundColor")]
    public string SessionHeaderForegroundColor { get; set; } = DefaultSessionHeaderForegroundColor;

    [JsonPropertyName("resizeHandleHoverColor")]
    public string ResizeHandleHoverColor { get; set; } = DefaultResizeHandleHoverColor;

    [JsonPropertyName("inputContainerBackgroundColor")]
    public string InputContainerBackgroundColor { get; set; } = DefaultInputContainerBackgroundColor;

    [JsonPropertyName("inputTextBoxBackgroundColor")]
    public string InputTextBoxBackgroundColor { get; set; } = DefaultInputTextBoxBackgroundColor;

    [JsonPropertyName("inputTextBoxForegroundColor")]
    public string InputTextBoxForegroundColor { get; set; } = DefaultInputTextBoxForegroundColor;

    [JsonPropertyName("inputTextBoxBorderColor")]
    public string InputTextBoxBorderColor { get; set; } = DefaultInputTextBoxBorderColor;

    [JsonPropertyName("inputWatermarkForegroundColor")]
    public string InputWatermarkForegroundColor { get; set; } = DefaultInputWatermarkForegroundColor;

    [JsonPropertyName("waitForUserInputHighlightColor")]
    public string WaitForUserInputHighlightColor { get; set; } = DefaultWaitForUserInputHighlightColor;

    [JsonPropertyName("waitForUserInputContainerBackgroundColor")]
    public string WaitForUserInputContainerBackgroundColor { get; set; } = DefaultWaitForUserInputContainerBackgroundColor;

    [JsonPropertyName("waitForUserInputTextBoxBackgroundColor")]
    public string WaitForUserInputTextBoxBackgroundColor { get; set; } = DefaultWaitForUserInputTextBoxBackgroundColor;

    [JsonPropertyName("waitForUserInputTextBoxForegroundColor")]
    public string WaitForUserInputTextBoxForegroundColor { get; set; } = DefaultWaitForUserInputTextBoxForegroundColor;

    [JsonPropertyName("waitForUserInputWatermarkForegroundColor")]
    public string WaitForUserInputWatermarkForegroundColor { get; set; } = DefaultWaitForUserInputWatermarkForegroundColor;

    [JsonPropertyName("sendButtonBackgroundColor")]
    public string SendButtonBackgroundColor { get; set; } = DefaultSendButtonBackgroundColor;

    [JsonPropertyName("sendButtonForegroundColor")]
    public string SendButtonForegroundColor { get; set; } = DefaultSendButtonForegroundColor;

    [JsonPropertyName("sendButtonHoverBackgroundColor")]
    public string SendButtonHoverBackgroundColor { get; set; } = DefaultSendButtonHoverBackgroundColor;

    [JsonPropertyName("sendButtonHoverForegroundColor")]
    public string SendButtonHoverForegroundColor { get; set; } = DefaultSendButtonHoverForegroundColor;

    [JsonPropertyName("historyButtonBackgroundColor")]
    public string HistoryButtonBackgroundColor { get; set; } = DefaultHistoryButtonBackgroundColor;

    [JsonPropertyName("historyButtonForegroundColor")]
    public string HistoryButtonForegroundColor { get; set; } = DefaultHistoryButtonForegroundColor;

    [JsonPropertyName("historyButtonHoverBackgroundColor")]
    public string HistoryButtonHoverBackgroundColor { get; set; } = DefaultHistoryButtonHoverBackgroundColor;

    [JsonPropertyName("historyButtonHoverForegroundColor")]
    public string HistoryButtonHoverForegroundColor { get; set; } = DefaultHistoryButtonHoverForegroundColor;

    [JsonPropertyName("draftButtonBackgroundColor")]
    public string DraftButtonBackgroundColor { get; set; } = DefaultDraftButtonBackgroundColor;

    [JsonPropertyName("draftButtonForegroundColor")]
    public string DraftButtonForegroundColor { get; set; } = DefaultDraftButtonForegroundColor;

    [JsonPropertyName("draftButtonActiveBackgroundColor")]
    public string DraftButtonActiveBackgroundColor { get; set; } = DefaultDraftButtonActiveBackgroundColor;

    [JsonPropertyName("draftButtonActiveForegroundColor")]
    public string DraftButtonActiveForegroundColor { get; set; } = DefaultDraftButtonActiveForegroundColor;

    [JsonPropertyName("contextMenuBackgroundColor")]
    public string ContextMenuBackgroundColor { get; set; } = DefaultContextMenuBackgroundColor;

    [JsonPropertyName("contextMenuGutterBackgroundColor")]
    public string ContextMenuGutterBackgroundColor { get; set; } = DefaultContextMenuGutterBackgroundColor;

    [JsonPropertyName("contextMenuForegroundColor")]
    public string ContextMenuForegroundColor { get; set; } = DefaultContextMenuForegroundColor;

    [JsonPropertyName("contextMenuBorderColor")]
    public string ContextMenuBorderColor { get; set; } = DefaultContextMenuBorderColor;

    [JsonPropertyName("contextMenuSeparatorColor")]
    public string ContextMenuSeparatorColor { get; set; } = DefaultContextMenuSeparatorColor;

    [JsonPropertyName("contextMenuShortcutForegroundColor")]
    public string ContextMenuShortcutForegroundColor { get; set; } = DefaultContextMenuShortcutForegroundColor;

    [JsonPropertyName("contextMenuAccentForegroundColor")]
    public string ContextMenuAccentForegroundColor { get; set; } = DefaultContextMenuAccentForegroundColor;

    [JsonPropertyName("contextMenuHighlightBackgroundColor")]
    public string ContextMenuHighlightBackgroundColor { get; set; } = DefaultContextMenuHighlightBackgroundColor;

    [JsonPropertyName("contextMenuGroupHeaderForegroundColor")]
    public string ContextMenuGroupHeaderForegroundColor { get; set; } = DefaultContextMenuGroupHeaderForegroundColor;

    [JsonPropertyName("commandPopupBackgroundColor")]
    public string CommandPopupBackgroundColor { get; set; } = DefaultCommandPopupBackgroundColor;

    [JsonPropertyName("commandPopupBorderColor")]
    public string CommandPopupBorderColor { get; set; } = DefaultCommandPopupBorderColor;

    [JsonPropertyName("commandPopupHeaderBackgroundColor")]
    public string CommandPopupHeaderBackgroundColor { get; set; } = DefaultCommandPopupHeaderBackgroundColor;

    [JsonPropertyName("commandPopupHeaderForegroundColor")]
    public string CommandPopupHeaderForegroundColor { get; set; } = DefaultCommandPopupHeaderForegroundColor;

    [JsonPropertyName("commandPopupItemForegroundColor")]
    public string CommandPopupItemForegroundColor { get; set; } = DefaultCommandPopupItemForegroundColor;

    [JsonPropertyName("commandPopupItemHoverBackgroundColor")]
    public string CommandPopupItemHoverBackgroundColor { get; set; } = DefaultCommandPopupItemHoverBackgroundColor;

    [JsonPropertyName("commandPopupItemSelectedBackgroundColor")]
    public string CommandPopupItemSelectedBackgroundColor { get; set; } = DefaultCommandPopupItemSelectedBackgroundColor;

    public static MainWindowThemeConfig Normalize(MainWindowThemeConfig? theme, string? legacyWaitForUserInputHighlightColor = null)
    {
        bool isMissingTheme = theme == null;
        theme ??= new MainWindowThemeConfig();

        string waitHighlightColor = UiColorHelper.NormalizeColorString(
            (isMissingTheme || string.IsNullOrWhiteSpace(theme.WaitForUserInputHighlightColor))
                ? legacyWaitForUserInputHighlightColor
                : theme.WaitForUserInputHighlightColor,
            DefaultWaitForUserInputHighlightColor);

        bool shouldDeriveLegacyWaitBackgrounds = ShouldDeriveLegacyWaitBackgrounds(theme, waitHighlightColor, legacyWaitForUserInputHighlightColor);
        string defaultWaitContainerBackgroundColor = shouldDeriveLegacyWaitBackgrounds
            ? UiColorHelper.CreateScaledColorString(waitHighlightColor, waitHighlightColor, 0.28)
            : DefaultWaitForUserInputContainerBackgroundColor;
        string defaultWaitTextBoxBackgroundColor = shouldDeriveLegacyWaitBackgrounds
            ? UiColorHelper.CreateScaledColorString(waitHighlightColor, waitHighlightColor, 0.38)
            : DefaultWaitForUserInputTextBoxBackgroundColor;
        string rawWaitContainerBackgroundColor = isMissingTheme ? string.Empty : theme.WaitForUserInputContainerBackgroundColor;
        string rawWaitTextBoxBackgroundColor = isMissingTheme ? string.Empty : theme.WaitForUserInputTextBoxBackgroundColor;
        string normalizedInputTextBoxForegroundColor = UiColorHelper.NormalizeColorString(
            theme.InputTextBoxForegroundColor,
            DefaultInputTextBoxForegroundColor);
        string normalizedInputWatermarkForegroundColor = UiColorHelper.NormalizeColorString(
            theme.InputWatermarkForegroundColor,
            DefaultInputWatermarkForegroundColor);
        string normalizedWaitForUserInputTextBoxForegroundColor = UiColorHelper.NormalizeColorString(
            string.IsNullOrWhiteSpace(theme.WaitForUserInputTextBoxForegroundColor)
                ? normalizedInputTextBoxForegroundColor
                : theme.WaitForUserInputTextBoxForegroundColor,
            normalizedInputTextBoxForegroundColor);
        string normalizedWaitForUserInputWatermarkForegroundColor = UiColorHelper.NormalizeColorString(
            string.IsNullOrWhiteSpace(theme.WaitForUserInputWatermarkForegroundColor)
                ? normalizedInputWatermarkForegroundColor
                : theme.WaitForUserInputWatermarkForegroundColor,
            normalizedInputWatermarkForegroundColor);
        string normalizedContextMenuBackgroundColor = UiColorHelper.NormalizeColorString(
            theme.ContextMenuBackgroundColor,
            DefaultContextMenuBackgroundColor);

        return new MainWindowThemeConfig
        {
            WindowBackgroundColor = UiColorHelper.NormalizeColorString(theme.WindowBackgroundColor, DefaultWindowBackgroundColor),
            SessionBorderColor = UiColorHelper.NormalizeColorString(theme.SessionBorderColor, DefaultSessionBorderColor),
            SessionHeaderBackgroundColor = UiColorHelper.NormalizeColorString(theme.SessionHeaderBackgroundColor, DefaultSessionHeaderBackgroundColor),
            SessionHeaderForegroundColor = UiColorHelper.NormalizeColorString(theme.SessionHeaderForegroundColor, DefaultSessionHeaderForegroundColor),
            ResizeHandleHoverColor = UiColorHelper.NormalizeColorString(theme.ResizeHandleHoverColor, DefaultResizeHandleHoverColor),
            InputContainerBackgroundColor = UiColorHelper.NormalizeColorString(theme.InputContainerBackgroundColor, DefaultInputContainerBackgroundColor),
            InputTextBoxBackgroundColor = UiColorHelper.NormalizeColorString(theme.InputTextBoxBackgroundColor, DefaultInputTextBoxBackgroundColor),
            InputTextBoxForegroundColor = normalizedInputTextBoxForegroundColor,
            InputTextBoxBorderColor = UiColorHelper.NormalizeColorString(theme.InputTextBoxBorderColor, DefaultInputTextBoxBorderColor),
            InputWatermarkForegroundColor = normalizedInputWatermarkForegroundColor,
            WaitForUserInputHighlightColor = waitHighlightColor,
            WaitForUserInputContainerBackgroundColor = UiColorHelper.NormalizeColorString(rawWaitContainerBackgroundColor, defaultWaitContainerBackgroundColor),
            WaitForUserInputTextBoxBackgroundColor = UiColorHelper.NormalizeColorString(rawWaitTextBoxBackgroundColor, defaultWaitTextBoxBackgroundColor),
            WaitForUserInputTextBoxForegroundColor = normalizedWaitForUserInputTextBoxForegroundColor,
            WaitForUserInputWatermarkForegroundColor = normalizedWaitForUserInputWatermarkForegroundColor,
            SendButtonBackgroundColor = UiColorHelper.NormalizeColorString(theme.SendButtonBackgroundColor, DefaultSendButtonBackgroundColor),
            SendButtonForegroundColor = UiColorHelper.NormalizeColorString(theme.SendButtonForegroundColor, DefaultSendButtonForegroundColor),
            SendButtonHoverBackgroundColor = UiColorHelper.NormalizeColorString(theme.SendButtonHoverBackgroundColor, DefaultSendButtonHoverBackgroundColor),
            SendButtonHoverForegroundColor = UiColorHelper.NormalizeColorString(theme.SendButtonHoverForegroundColor, DefaultSendButtonHoverForegroundColor),
            HistoryButtonBackgroundColor = UiColorHelper.NormalizeColorString(theme.HistoryButtonBackgroundColor, DefaultHistoryButtonBackgroundColor),
            HistoryButtonForegroundColor = UiColorHelper.NormalizeColorString(theme.HistoryButtonForegroundColor, DefaultHistoryButtonForegroundColor),
            HistoryButtonHoverBackgroundColor = UiColorHelper.NormalizeColorString(theme.HistoryButtonHoverBackgroundColor, DefaultHistoryButtonHoverBackgroundColor),
            HistoryButtonHoverForegroundColor = UiColorHelper.NormalizeColorString(theme.HistoryButtonHoverForegroundColor, DefaultHistoryButtonHoverForegroundColor),
            DraftButtonBackgroundColor = UiColorHelper.NormalizeColorString(theme.DraftButtonBackgroundColor, DefaultDraftButtonBackgroundColor),
            DraftButtonForegroundColor = UiColorHelper.NormalizeColorString(theme.DraftButtonForegroundColor, DefaultDraftButtonForegroundColor),
            DraftButtonActiveBackgroundColor = UiColorHelper.NormalizeColorString(theme.DraftButtonActiveBackgroundColor, DefaultDraftButtonActiveBackgroundColor),
            DraftButtonActiveForegroundColor = UiColorHelper.NormalizeColorString(theme.DraftButtonActiveForegroundColor, DefaultDraftButtonActiveForegroundColor),
            ContextMenuBackgroundColor = normalizedContextMenuBackgroundColor,
            ContextMenuGutterBackgroundColor = UiColorHelper.NormalizeColorString(theme.ContextMenuGutterBackgroundColor, normalizedContextMenuBackgroundColor),
            ContextMenuForegroundColor = UiColorHelper.NormalizeColorString(theme.ContextMenuForegroundColor, DefaultContextMenuForegroundColor),
            ContextMenuBorderColor = UiColorHelper.NormalizeColorString(theme.ContextMenuBorderColor, DefaultContextMenuBorderColor),
            ContextMenuSeparatorColor = UiColorHelper.NormalizeColorString(theme.ContextMenuSeparatorColor, DefaultContextMenuSeparatorColor),
            ContextMenuShortcutForegroundColor = UiColorHelper.NormalizeColorString(theme.ContextMenuShortcutForegroundColor, DefaultContextMenuShortcutForegroundColor),
            ContextMenuAccentForegroundColor = UiColorHelper.NormalizeColorString(theme.ContextMenuAccentForegroundColor, DefaultContextMenuAccentForegroundColor),
            ContextMenuHighlightBackgroundColor = UiColorHelper.NormalizeColorString(theme.ContextMenuHighlightBackgroundColor, DefaultContextMenuHighlightBackgroundColor),
            ContextMenuGroupHeaderForegroundColor = UiColorHelper.NormalizeColorString(theme.ContextMenuGroupHeaderForegroundColor, DefaultContextMenuGroupHeaderForegroundColor),
            CommandPopupBackgroundColor = UiColorHelper.NormalizeColorString(theme.CommandPopupBackgroundColor, DefaultCommandPopupBackgroundColor),
            CommandPopupBorderColor = UiColorHelper.NormalizeColorString(theme.CommandPopupBorderColor, DefaultCommandPopupBorderColor),
            CommandPopupHeaderBackgroundColor = UiColorHelper.NormalizeColorString(theme.CommandPopupHeaderBackgroundColor, DefaultCommandPopupHeaderBackgroundColor),
            CommandPopupHeaderForegroundColor = UiColorHelper.NormalizeColorString(theme.CommandPopupHeaderForegroundColor, DefaultCommandPopupHeaderForegroundColor),
            CommandPopupItemForegroundColor = UiColorHelper.NormalizeColorString(theme.CommandPopupItemForegroundColor, DefaultCommandPopupItemForegroundColor),
            CommandPopupItemHoverBackgroundColor = UiColorHelper.NormalizeColorString(theme.CommandPopupItemHoverBackgroundColor, DefaultCommandPopupItemHoverBackgroundColor),
            CommandPopupItemSelectedBackgroundColor = UiColorHelper.NormalizeColorString(theme.CommandPopupItemSelectedBackgroundColor, DefaultCommandPopupItemSelectedBackgroundColor)
        };
    }

    private static bool ShouldDeriveLegacyWaitBackgrounds(MainWindowThemeConfig theme, string waitHighlightColor, string? legacyWaitForUserInputHighlightColor)
    {
        if (!string.IsNullOrWhiteSpace(legacyWaitForUserInputHighlightColor))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(theme.WaitForUserInputHighlightColor))
        {
            return false;
        }

        return !string.Equals(waitHighlightColor, DefaultWaitForUserInputHighlightColor, StringComparison.OrdinalIgnoreCase);
    }
}
