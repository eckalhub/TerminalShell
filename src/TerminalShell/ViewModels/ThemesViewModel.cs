using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TerminalShell.Core;
using TerminalShell.Models;
using Brush = System.Windows.Media.Brush;

namespace TerminalShell.ViewModels;

public enum ThemePreviewKind
{
    WindowSession,
    Input,
    WaitForUserInput,
    Buttons,
    MenusAndIntelliSense
}

public sealed class ThemeColorGroupViewModel
{
    public ThemeColorGroupViewModel(
        string title,
        ThemePreviewKind previewKind,
        MainWindowThemeState previewThemeState,
        IEnumerable<ThemeColorItemViewModel> items)
    {
        Title = title;
        PreviewKind = previewKind;
        PreviewThemeState = previewThemeState;
        Items = new ObservableCollection<ThemeColorItemViewModel>(items);
    }

    public string Title { get; }
    public ThemePreviewKind PreviewKind { get; }
    public MainWindowThemeState PreviewThemeState { get; }
    public ObservableCollection<ThemeColorItemViewModel> Items { get; }
}

public partial class ThemeColorItemViewModel : ObservableObject
{
    public ThemeColorItemViewModel(string key, string label, string toolTip, string defaultColor)
    {
        Key = key;
        Label = label;
        ToolTip = toolTip;
        DefaultColor = UiColorHelper.NormalizeColorString(defaultColor, defaultColor);
        Value = DefaultColor;
    }

    public string Key { get; }
    public string Label { get; }
    public string ToolTip { get; }
    public string DefaultColor { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NormalizedValue))]
    [NotifyPropertyChangedFor(nameof(PreviewBrush))]
    [NotifyPropertyChangedFor(nameof(PreviewToolTip))]
    private string _value = string.Empty;

    public string NormalizedValue => UiColorHelper.NormalizeColorString(Value, DefaultColor);
    public Brush PreviewBrush => UiColorHelper.CreateSolidBrush(Value, DefaultColor);
    public string PreviewToolTip => $"点击选择颜色 ({NormalizedValue})";

    public void Load(string? rawValue)
    {
        Value = UiColorHelper.NormalizeColorString(rawValue, DefaultColor);
    }

    public string GetNormalizedValue()
    {
        return NormalizedValue;
    }

    [RelayCommand]
    private void ResetToDefault()
    {
        Value = DefaultColor;
    }
}

public partial class ThemesViewModel : ObservableObject
{
    private readonly Dictionary<string, ThemeColorItemViewModel> _itemsByKey = new(StringComparer.Ordinal);
    private bool _suspendLivePreviewRefresh;

    public string Name => "themes";
    public string GroupName => string.Empty;

    public ObservableCollection<ThemeColorGroupViewModel> Groups { get; } = new();
    public MainWindowThemeState LivePreviewThemeState { get; } = new();

    public ThemesViewModel()
    {
        BuildGroups();
        Load(new MainWindowThemeConfig());
    }

    public void Load(MainWindowThemeConfig? theme)
    {
        MainWindowThemeConfig normalized = MainWindowThemeConfig.Normalize(theme);

        _suspendLivePreviewRefresh = true;
        try
        {
            Set(nameof(MainWindowThemeConfig.WindowBackgroundColor), normalized.WindowBackgroundColor);
            Set(nameof(MainWindowThemeConfig.SessionBorderColor), normalized.SessionBorderColor);
            Set(nameof(MainWindowThemeConfig.SessionHeaderBackgroundColor), normalized.SessionHeaderBackgroundColor);
            Set(nameof(MainWindowThemeConfig.SessionHeaderForegroundColor), normalized.SessionHeaderForegroundColor);
            Set(nameof(MainWindowThemeConfig.ResizeHandleHoverColor), normalized.ResizeHandleHoverColor);
            Set(nameof(MainWindowThemeConfig.InputContainerBackgroundColor), normalized.InputContainerBackgroundColor);
            Set(nameof(MainWindowThemeConfig.InputTextBoxBackgroundColor), normalized.InputTextBoxBackgroundColor);
            Set(nameof(MainWindowThemeConfig.InputTextBoxForegroundColor), normalized.InputTextBoxForegroundColor);
            Set(nameof(MainWindowThemeConfig.InputTextBoxBorderColor), normalized.InputTextBoxBorderColor);
            Set(nameof(MainWindowThemeConfig.InputWatermarkForegroundColor), normalized.InputWatermarkForegroundColor);
            Set(nameof(MainWindowThemeConfig.WaitForUserInputHighlightColor), normalized.WaitForUserInputHighlightColor);
            Set(nameof(MainWindowThemeConfig.WaitForUserInputContainerBackgroundColor), normalized.WaitForUserInputContainerBackgroundColor);
            Set(nameof(MainWindowThemeConfig.WaitForUserInputTextBoxBackgroundColor), normalized.WaitForUserInputTextBoxBackgroundColor);
            Set(nameof(MainWindowThemeConfig.WaitForUserInputTextBoxForegroundColor), normalized.WaitForUserInputTextBoxForegroundColor);
            Set(nameof(MainWindowThemeConfig.WaitForUserInputWatermarkForegroundColor), normalized.WaitForUserInputWatermarkForegroundColor);
            Set(nameof(MainWindowThemeConfig.SendButtonBackgroundColor), normalized.SendButtonBackgroundColor);
            Set(nameof(MainWindowThemeConfig.SendButtonForegroundColor), normalized.SendButtonForegroundColor);
            Set(nameof(MainWindowThemeConfig.SendButtonHoverBackgroundColor), normalized.SendButtonHoverBackgroundColor);
            Set(nameof(MainWindowThemeConfig.SendButtonHoverForegroundColor), normalized.SendButtonHoverForegroundColor);
            Set(nameof(MainWindowThemeConfig.HistoryButtonBackgroundColor), normalized.HistoryButtonBackgroundColor);
            Set(nameof(MainWindowThemeConfig.HistoryButtonForegroundColor), normalized.HistoryButtonForegroundColor);
            Set(nameof(MainWindowThemeConfig.HistoryButtonHoverBackgroundColor), normalized.HistoryButtonHoverBackgroundColor);
            Set(nameof(MainWindowThemeConfig.HistoryButtonHoverForegroundColor), normalized.HistoryButtonHoverForegroundColor);
            Set(nameof(MainWindowThemeConfig.DraftButtonBackgroundColor), normalized.DraftButtonBackgroundColor);
            Set(nameof(MainWindowThemeConfig.DraftButtonForegroundColor), normalized.DraftButtonForegroundColor);
            Set(nameof(MainWindowThemeConfig.DraftButtonActiveBackgroundColor), normalized.DraftButtonActiveBackgroundColor);
            Set(nameof(MainWindowThemeConfig.DraftButtonActiveForegroundColor), normalized.DraftButtonActiveForegroundColor);
            Set(nameof(MainWindowThemeConfig.ContextMenuBackgroundColor), normalized.ContextMenuBackgroundColor);
            Set(nameof(MainWindowThemeConfig.ContextMenuGutterBackgroundColor), normalized.ContextMenuGutterBackgroundColor);
            Set(nameof(MainWindowThemeConfig.ContextMenuForegroundColor), normalized.ContextMenuForegroundColor);
            Set(nameof(MainWindowThemeConfig.ContextMenuBorderColor), normalized.ContextMenuBorderColor);
            Set(nameof(MainWindowThemeConfig.ContextMenuSeparatorColor), normalized.ContextMenuSeparatorColor);
            Set(nameof(MainWindowThemeConfig.ContextMenuShortcutForegroundColor), normalized.ContextMenuShortcutForegroundColor);
            Set(nameof(MainWindowThemeConfig.ContextMenuAccentForegroundColor), normalized.ContextMenuAccentForegroundColor);
            Set(nameof(MainWindowThemeConfig.ContextMenuHighlightBackgroundColor), normalized.ContextMenuHighlightBackgroundColor);
            Set(nameof(MainWindowThemeConfig.ContextMenuGroupHeaderForegroundColor), normalized.ContextMenuGroupHeaderForegroundColor);
            Set(nameof(MainWindowThemeConfig.CommandPopupBackgroundColor), normalized.CommandPopupBackgroundColor);
            Set(nameof(MainWindowThemeConfig.CommandPopupBorderColor), normalized.CommandPopupBorderColor);
            Set(nameof(MainWindowThemeConfig.CommandPopupHeaderBackgroundColor), normalized.CommandPopupHeaderBackgroundColor);
            Set(nameof(MainWindowThemeConfig.CommandPopupHeaderForegroundColor), normalized.CommandPopupHeaderForegroundColor);
            Set(nameof(MainWindowThemeConfig.CommandPopupItemForegroundColor), normalized.CommandPopupItemForegroundColor);
            Set(nameof(MainWindowThemeConfig.CommandPopupItemHoverBackgroundColor), normalized.CommandPopupItemHoverBackgroundColor);
            Set(nameof(MainWindowThemeConfig.CommandPopupItemSelectedBackgroundColor), normalized.CommandPopupItemSelectedBackgroundColor);
        }
        finally
        {
            _suspendLivePreviewRefresh = false;
        }

        RefreshLivePreviewTheme();
    }

    public MainWindowThemeConfig BuildThemeConfig()
    {
        return MainWindowThemeConfig.Normalize(new MainWindowThemeConfig
        {
            WindowBackgroundColor = Get(nameof(MainWindowThemeConfig.WindowBackgroundColor)),
            SessionBorderColor = Get(nameof(MainWindowThemeConfig.SessionBorderColor)),
            SessionHeaderBackgroundColor = Get(nameof(MainWindowThemeConfig.SessionHeaderBackgroundColor)),
            SessionHeaderForegroundColor = Get(nameof(MainWindowThemeConfig.SessionHeaderForegroundColor)),
            ResizeHandleHoverColor = Get(nameof(MainWindowThemeConfig.ResizeHandleHoverColor)),
            InputContainerBackgroundColor = Get(nameof(MainWindowThemeConfig.InputContainerBackgroundColor)),
            InputTextBoxBackgroundColor = Get(nameof(MainWindowThemeConfig.InputTextBoxBackgroundColor)),
            InputTextBoxForegroundColor = Get(nameof(MainWindowThemeConfig.InputTextBoxForegroundColor)),
            InputTextBoxBorderColor = Get(nameof(MainWindowThemeConfig.InputTextBoxBorderColor)),
            InputWatermarkForegroundColor = Get(nameof(MainWindowThemeConfig.InputWatermarkForegroundColor)),
            WaitForUserInputHighlightColor = Get(nameof(MainWindowThemeConfig.WaitForUserInputHighlightColor)),
            WaitForUserInputContainerBackgroundColor = Get(nameof(MainWindowThemeConfig.WaitForUserInputContainerBackgroundColor)),
            WaitForUserInputTextBoxBackgroundColor = Get(nameof(MainWindowThemeConfig.WaitForUserInputTextBoxBackgroundColor)),
            WaitForUserInputTextBoxForegroundColor = Get(nameof(MainWindowThemeConfig.WaitForUserInputTextBoxForegroundColor)),
            WaitForUserInputWatermarkForegroundColor = Get(nameof(MainWindowThemeConfig.WaitForUserInputWatermarkForegroundColor)),
            SendButtonBackgroundColor = Get(nameof(MainWindowThemeConfig.SendButtonBackgroundColor)),
            SendButtonForegroundColor = Get(nameof(MainWindowThemeConfig.SendButtonForegroundColor)),
            SendButtonHoverBackgroundColor = Get(nameof(MainWindowThemeConfig.SendButtonHoverBackgroundColor)),
            SendButtonHoverForegroundColor = Get(nameof(MainWindowThemeConfig.SendButtonHoverForegroundColor)),
            HistoryButtonBackgroundColor = Get(nameof(MainWindowThemeConfig.HistoryButtonBackgroundColor)),
            HistoryButtonForegroundColor = Get(nameof(MainWindowThemeConfig.HistoryButtonForegroundColor)),
            HistoryButtonHoverBackgroundColor = Get(nameof(MainWindowThemeConfig.HistoryButtonHoverBackgroundColor)),
            HistoryButtonHoverForegroundColor = Get(nameof(MainWindowThemeConfig.HistoryButtonHoverForegroundColor)),
            DraftButtonBackgroundColor = Get(nameof(MainWindowThemeConfig.DraftButtonBackgroundColor)),
            DraftButtonForegroundColor = Get(nameof(MainWindowThemeConfig.DraftButtonForegroundColor)),
            DraftButtonActiveBackgroundColor = Get(nameof(MainWindowThemeConfig.DraftButtonActiveBackgroundColor)),
            DraftButtonActiveForegroundColor = Get(nameof(MainWindowThemeConfig.DraftButtonActiveForegroundColor)),
            ContextMenuBackgroundColor = Get(nameof(MainWindowThemeConfig.ContextMenuBackgroundColor)),
            ContextMenuGutterBackgroundColor = Get(nameof(MainWindowThemeConfig.ContextMenuGutterBackgroundColor)),
            ContextMenuForegroundColor = Get(nameof(MainWindowThemeConfig.ContextMenuForegroundColor)),
            ContextMenuBorderColor = Get(nameof(MainWindowThemeConfig.ContextMenuBorderColor)),
            ContextMenuSeparatorColor = Get(nameof(MainWindowThemeConfig.ContextMenuSeparatorColor)),
            ContextMenuShortcutForegroundColor = Get(nameof(MainWindowThemeConfig.ContextMenuShortcutForegroundColor)),
            ContextMenuAccentForegroundColor = Get(nameof(MainWindowThemeConfig.ContextMenuAccentForegroundColor)),
            ContextMenuHighlightBackgroundColor = Get(nameof(MainWindowThemeConfig.ContextMenuHighlightBackgroundColor)),
            ContextMenuGroupHeaderForegroundColor = Get(nameof(MainWindowThemeConfig.ContextMenuGroupHeaderForegroundColor)),
            CommandPopupBackgroundColor = Get(nameof(MainWindowThemeConfig.CommandPopupBackgroundColor)),
            CommandPopupBorderColor = Get(nameof(MainWindowThemeConfig.CommandPopupBorderColor)),
            CommandPopupHeaderBackgroundColor = Get(nameof(MainWindowThemeConfig.CommandPopupHeaderBackgroundColor)),
            CommandPopupHeaderForegroundColor = Get(nameof(MainWindowThemeConfig.CommandPopupHeaderForegroundColor)),
            CommandPopupItemForegroundColor = Get(nameof(MainWindowThemeConfig.CommandPopupItemForegroundColor)),
            CommandPopupItemHoverBackgroundColor = Get(nameof(MainWindowThemeConfig.CommandPopupItemHoverBackgroundColor)),
            CommandPopupItemSelectedBackgroundColor = Get(nameof(MainWindowThemeConfig.CommandPopupItemSelectedBackgroundColor))
        });
    }

    [RelayCommand]
    private void ResetAllToDefaults()
    {
        _suspendLivePreviewRefresh = true;
        try
        {
            foreach (ThemeColorItemViewModel item in _itemsByKey.Values)
            {
                item.Load(item.DefaultColor);
            }
        }
        finally
        {
            _suspendLivePreviewRefresh = false;
        }

        RefreshLivePreviewTheme();
    }

    private void BuildGroups()
    {
        Groups.Add(new ThemeColorGroupViewModel(
            "Window & Session",
            ThemePreviewKind.WindowSession,
            LivePreviewThemeState,
            [
                CreateItem(nameof(MainWindowThemeConfig.WindowBackgroundColor), "Window Background", "Main window shell background.", MainWindowThemeConfig.DefaultWindowBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.SessionBorderColor), "Session Border", "Terminal session border.", MainWindowThemeConfig.DefaultSessionBorderColor),
                CreateItem(nameof(MainWindowThemeConfig.SessionHeaderBackgroundColor), "Session Header Background", "Terminal title bar background.", MainWindowThemeConfig.DefaultSessionHeaderBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.SessionHeaderForegroundColor), "Session Header Text", "Terminal title and header button text.", MainWindowThemeConfig.DefaultSessionHeaderForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.ResizeHandleHoverColor), "Resize Handle Hover", "Input resize handle hover highlight.", MainWindowThemeConfig.DefaultResizeHandleHoverColor)
            ]));

        Groups.Add(new ThemeColorGroupViewModel(
            "Input",
            ThemePreviewKind.Input,
            LivePreviewThemeState,
            [
                CreateItem(nameof(MainWindowThemeConfig.InputContainerBackgroundColor), "Input Container Background", "Outer input area background.", MainWindowThemeConfig.DefaultInputContainerBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.InputTextBoxBackgroundColor), "Input Background", "Main input text box background.", MainWindowThemeConfig.DefaultInputTextBoxBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.InputTextBoxForegroundColor), "Input Text", "Main input text color.", MainWindowThemeConfig.DefaultInputTextBoxForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.InputTextBoxBorderColor), "Input Border", "Main input text box border.", MainWindowThemeConfig.DefaultInputTextBoxBorderColor),
                CreateItem(nameof(MainWindowThemeConfig.InputWatermarkForegroundColor), "Input Watermark Text", "Placeholder watermark text color.", MainWindowThemeConfig.DefaultInputWatermarkForegroundColor)
            ]));

        Groups.Add(new ThemeColorGroupViewModel(
            "Wait For User Input",
            ThemePreviewKind.WaitForUserInput,
            LivePreviewThemeState,
            [
                CreateItem(nameof(MainWindowThemeConfig.WaitForUserInputHighlightColor), "Wait Highlight", "Shared highlight for waiting-for-input border and draft state.", MainWindowThemeConfig.DefaultWaitForUserInputHighlightColor),
                CreateItem(nameof(MainWindowThemeConfig.WaitForUserInputContainerBackgroundColor), "Wait Container Background", "Input container background while waiting for user input.", MainWindowThemeConfig.DefaultWaitForUserInputContainerBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.WaitForUserInputTextBoxBackgroundColor), "Wait Input Background", "Input text box background while waiting for user input.", MainWindowThemeConfig.DefaultWaitForUserInputTextBoxBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.WaitForUserInputTextBoxForegroundColor), "Wait Input Text", "Input text color while waiting for user input.", MainWindowThemeConfig.DefaultWaitForUserInputTextBoxForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.WaitForUserInputWatermarkForegroundColor), "Wait Watermark Text", "Placeholder watermark text color while waiting for user input.", MainWindowThemeConfig.DefaultWaitForUserInputWatermarkForegroundColor)
            ]));

        Groups.Add(new ThemeColorGroupViewModel(
            "Buttons",
            ThemePreviewKind.Buttons,
            LivePreviewThemeState,
            [
                CreateItem(nameof(MainWindowThemeConfig.SendButtonBackgroundColor), "Send Background", "Send button background.", MainWindowThemeConfig.DefaultSendButtonBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.SendButtonForegroundColor), "Send Text", "Send button text color.", MainWindowThemeConfig.DefaultSendButtonForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.SendButtonHoverBackgroundColor), "Send Hover Background", "Send button hover background.", MainWindowThemeConfig.DefaultSendButtonHoverBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.SendButtonHoverForegroundColor), "Send Hover Text", "Send button hover text color.", MainWindowThemeConfig.DefaultSendButtonHoverForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.HistoryButtonBackgroundColor), "History Background", "History button background.", MainWindowThemeConfig.DefaultHistoryButtonBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.HistoryButtonForegroundColor), "History Text", "History button text color.", MainWindowThemeConfig.DefaultHistoryButtonForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.HistoryButtonHoverBackgroundColor), "History Hover Background", "History button hover background.", MainWindowThemeConfig.DefaultHistoryButtonHoverBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.HistoryButtonHoverForegroundColor), "History Hover Text", "History button hover text color.", MainWindowThemeConfig.DefaultHistoryButtonHoverForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.DraftButtonBackgroundColor), "Draft Background", "Draft button background.", MainWindowThemeConfig.DefaultDraftButtonBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.DraftButtonForegroundColor), "Draft Text", "Draft button text color.", MainWindowThemeConfig.DefaultDraftButtonForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.DraftButtonActiveBackgroundColor), "Draft Active Background", "Draft button background while auto-draft is active.", MainWindowThemeConfig.DefaultDraftButtonActiveBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.DraftButtonActiveForegroundColor), "Draft Active Text", "Draft button text color while auto-draft is active.", MainWindowThemeConfig.DefaultDraftButtonActiveForegroundColor)
            ]));

        Groups.Add(new ThemeColorGroupViewModel(
            "Menus & IntelliSense",
            ThemePreviewKind.MenusAndIntelliSense,
            LivePreviewThemeState,
            [
                CreateItem(nameof(MainWindowThemeConfig.ContextMenuBackgroundColor), "Menu Background", "Shared context menu background.", MainWindowThemeConfig.DefaultContextMenuBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.ContextMenuGutterBackgroundColor), "Menu Gutter Background", "Left gutter background shared by menu check areas and submenu padding.", MainWindowThemeConfig.DefaultContextMenuGutterBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.ContextMenuForegroundColor), "Menu Text", "Shared context menu text color.", MainWindowThemeConfig.DefaultContextMenuForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.ContextMenuBorderColor), "Menu Border", "Shared context menu border.", MainWindowThemeConfig.DefaultContextMenuBorderColor),
                CreateItem(nameof(MainWindowThemeConfig.ContextMenuSeparatorColor), "Menu Separator", "Shared context menu separator line.", MainWindowThemeConfig.DefaultContextMenuSeparatorColor),
                CreateItem(nameof(MainWindowThemeConfig.ContextMenuShortcutForegroundColor), "Menu Shortcut Text", "Shortcut hint text inside menus.", MainWindowThemeConfig.DefaultContextMenuShortcutForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.ContextMenuAccentForegroundColor), "Menu Accent", "Accent color for check marks and key highlights.", MainWindowThemeConfig.DefaultContextMenuAccentForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.ContextMenuHighlightBackgroundColor), "Menu Hover Background", "Shared menu item hover background.", MainWindowThemeConfig.DefaultContextMenuHighlightBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.ContextMenuGroupHeaderForegroundColor), "Menu Group Header Text", "Show Terminal submenu group header text.", MainWindowThemeConfig.DefaultContextMenuGroupHeaderForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.CommandPopupBackgroundColor), "Popup Background", "Command popup background.", MainWindowThemeConfig.DefaultCommandPopupBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.CommandPopupBorderColor), "Popup Border", "Command popup border.", MainWindowThemeConfig.DefaultCommandPopupBorderColor),
                CreateItem(nameof(MainWindowThemeConfig.CommandPopupHeaderBackgroundColor), "Popup Header Background", "Command popup header background.", MainWindowThemeConfig.DefaultCommandPopupHeaderBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.CommandPopupHeaderForegroundColor), "Popup Header Text", "Command popup header text color.", MainWindowThemeConfig.DefaultCommandPopupHeaderForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.CommandPopupItemForegroundColor), "Popup Item Text", "Command popup item text color.", MainWindowThemeConfig.DefaultCommandPopupItemForegroundColor),
                CreateItem(nameof(MainWindowThemeConfig.CommandPopupItemHoverBackgroundColor), "Popup Hover Background", "Command popup hover background.", MainWindowThemeConfig.DefaultCommandPopupItemHoverBackgroundColor),
                CreateItem(nameof(MainWindowThemeConfig.CommandPopupItemSelectedBackgroundColor), "Popup Selected Background", "Command popup selected item background.", MainWindowThemeConfig.DefaultCommandPopupItemSelectedBackgroundColor)
            ]));
    }

    private ThemeColorItemViewModel CreateItem(string key, string label, string toolTip, string defaultColor)
    {
        ThemeColorItemViewModel item = new(key, label, toolTip, defaultColor);
        item.PropertyChanged += ThemeItem_PropertyChanged;
        _itemsByKey.Add(key, item);
        return item;
    }

    private void ThemeItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suspendLivePreviewRefresh)
        {
            return;
        }

        if (e.PropertyName == nameof(ThemeColorItemViewModel.Value))
        {
            RefreshLivePreviewTheme();
        }
    }

    private void RefreshLivePreviewTheme()
    {
        LivePreviewThemeState.Load(BuildThemeConfig());
    }

    private void Set(string key, string value)
    {
        _itemsByKey[key].Load(value);
    }

    private string Get(string key)
    {
        return _itemsByKey[key].GetNormalizedValue();
    }
}
