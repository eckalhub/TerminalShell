using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using TerminalShell.Controls;
using TerminalShell.Core;
using TerminalShell.Core.Config;
using TerminalShell.Models;
using TerminalShell.Services;
using TerminalShell.ViewModels;
using Point = System.Windows.Point;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using IDataObject = System.Windows.IDataObject;
using ListBox = System.Windows.Controls.ListBox;
using ListBoxItem = System.Windows.Controls.ListBoxItem;

using System.Linq;

namespace TerminalShell.Views;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel _mainVm;

    public SettingsWindow(MainViewModel mainVm)
    {
        InitializeComponent();
        RuntimeAppIdentity.ApplyWindowIcon(this);
        _mainVm = mainVm;
        this.DataContext = new SettingsViewModel();
        
        // Setup Debounce Timer for Hot Reload (avoids multi-firing on keystrokes)
        _debounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _debounceTimer.Tick += (s, args) =>
        {
            _debounceTimer.Stop();
            _mainVm.ReloadSessions();
        };

        // Subscribe to auto-save events from ViewModel to trigger hot reload
        if (this.DataContext is SettingsViewModel vm)
        {
            vm.SettingsSaved += () =>
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };
        }
    }

    private const int FixedNavigationItemCount = 3;

    private Point _dragStartPoint;
    private readonly System.Windows.Threading.DispatcherTimer _debounceTimer;
    private DropIndicatorAdorner? _currentAdorner;
    private ListBoxItem? _previewTargetItem;
    private DropInsertionPlacement _previewPlacement = DropInsertionPlacement.None;
    private int _previewFinalIndex = -1;

    private sealed record DropPreview(ListBoxItem TargetItem, DropInsertionPlacement Placement, int FinalIndex);

    private void ListBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void ListBox_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed
            || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        Point position = e.GetPosition(null);
        bool exceededDragThreshold =
            Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance;

        if (!exceededDragThreshold)
        {
            return;
        }

        ListBoxItem? listBoxItem = FindVisualParent<ListBoxItem>(source);
        if (listBoxItem == null || IsFixedNavigationItem(listBoxItem.DataContext))
        {
            return;
        }

        ClearDropPreview();

        try
        {
            DragDrop.DoDragDrop(listBoxItem, listBoxItem.DataContext, DragDropEffects.Move);
        }
        finally
        {
            ClearDropPreview();
        }
    }

    private void ListBox_DragOver(object sender, DragEventArgs e)
    {
        if (sender is not ListBox listBox
            || DataContext is not SettingsViewModel vm
            || e.OriginalSource is not DependencyObject source
            || !TryGetDraggedNavigationItem(e.Data, out object draggedItem))
        {
            e.Effects = DragDropEffects.None;
            ClearDropPreview();
            e.Handled = true;
            return;
        }

        ListBoxItem? hoveredItem = FindVisualParent<ListBoxItem>(source);
        DropPreview? preview = null;

        if (hoveredItem != null && !IsFixedNavigationItem(hoveredItem.DataContext))
        {
            DropInsertionPlacement placement = e.GetPosition(hoveredItem).Y < hoveredItem.ActualHeight / 2
                ? DropInsertionPlacement.Before
                : DropInsertionPlacement.After;

            TryBuildDropPreviewForTarget(vm, listBox, draggedItem, hoveredItem, placement, out preview);
        }
        else if (hoveredItem == null)
        {
            TryBuildBottomDropPreview(listBox, vm, draggedItem, e.GetPosition(listBox), out preview);
        }

        if (preview == null)
        {
            e.Effects = DragDropEffects.None;
            ClearDropPreview();
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        ApplyDropPreview(preview);
        e.Handled = true;
    }

    private void ListBox_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is not ListBox listBox)
        {
            return;
        }

        Point position = e.GetPosition(listBox);
        if (!IsPointWithin(listBox, position))
        {
            ClearDropPreview();
        }
    }

    private void ListBox_Drop(object sender, DragEventArgs e)
    {
        int finalIndex = _previewFinalIndex;
        ClearDropPreview();

        if (DataContext is not SettingsViewModel vm
            || !TryGetDraggedNavigationItem(e.Data, out object droppedData)
            || IsFixedNavigationItem(droppedData))
        {
            return;
        }

        int sourceIndex = vm.NavigationItems.IndexOf(droppedData);
        if (sourceIndex < 0
            || finalIndex < FixedNavigationItemCount
            || finalIndex >= vm.NavigationItems.Count)
        {
            return;
        }

        vm.ExecuteDragDropReorder(sourceIndex, finalIndex);
        e.Handled = true;
    }

    private void ApplyDropPreview(DropPreview preview)
    {
        if (_previewTargetItem == preview.TargetItem
            && _previewPlacement == preview.Placement
            && _previewFinalIndex == preview.FinalIndex)
        {
            return;
        }

        ClearDropPreview();

        _previewTargetItem = preview.TargetItem;
        _previewPlacement = preview.Placement;
        _previewFinalIndex = preview.FinalIndex;
        DropInsertionState.SetPlacement(preview.TargetItem, preview.Placement);

        AdornerLayer? layer = AdornerLayer.GetAdornerLayer(preview.TargetItem);
        if (layer != null)
        {
            _currentAdorner = new DropIndicatorAdorner(preview.TargetItem, preview.Placement);
            layer.Add(_currentAdorner);
        }
    }

    private void ClearDropPreview()
    {
        if (_previewTargetItem != null)
        {
            DropInsertionState.SetPlacement(_previewTargetItem, DropInsertionPlacement.None);

            if (_currentAdorner != null)
            {
                AdornerLayer? layer = AdornerLayer.GetAdornerLayer(_previewTargetItem);
                layer?.Remove(_currentAdorner);
            }
        }

        _currentAdorner = null;
        _previewTargetItem = null;
        _previewPlacement = DropInsertionPlacement.None;
        _previewFinalIndex = -1;
    }

    private bool TryBuildBottomDropPreview(
        ListBox listBox,
        SettingsViewModel vm,
        object draggedItem,
        Point pointerInList,
        out DropPreview? preview)
    {
        preview = null;

        if (!IsPointWithin(listBox, pointerInList)
            || !TryGetLastVisibleMovableItem(listBox, vm, out ListBoxItem lastVisibleItem))
        {
            return false;
        }

        Point lastItemTopLeft = lastVisibleItem.TranslatePoint(new Point(0, 0), listBox);
        double lastItemBottom = lastItemTopLeft.Y + lastVisibleItem.ActualHeight;
        if (pointerInList.Y < lastItemBottom - 1)
        {
            return false;
        }

        return TryBuildDropPreviewForTarget(
            vm,
            listBox,
            draggedItem,
            lastVisibleItem,
            DropInsertionPlacement.After,
            out preview);
    }

    private bool TryBuildDropPreviewForTarget(
        SettingsViewModel vm,
        ListBox listBox,
        object draggedItem,
        ListBoxItem targetItem,
        DropInsertionPlacement placement,
        out DropPreview? preview)
    {
        preview = null;

        int sourceIndex = vm.NavigationItems.IndexOf(draggedItem);
        int targetIndex = listBox.ItemContainerGenerator.IndexFromContainer(targetItem);
        if (sourceIndex < 0 || targetIndex < 0)
        {
            return false;
        }

        int finalIndex = CalculateFinalIndex(sourceIndex, targetIndex, placement);
        if (!IsValidDropPreview(vm, draggedItem, sourceIndex, finalIndex))
        {
            return false;
        }

        preview = new DropPreview(targetItem, placement, finalIndex);
        return true;
    }

    private static bool IsValidDropPreview(SettingsViewModel vm, object draggedItem, int sourceIndex, int finalIndex)
    {
        if (sourceIndex < FixedNavigationItemCount
            || finalIndex < FixedNavigationItemCount
            || finalIndex >= vm.NavigationItems.Count)
        {
            return false;
        }

        if (draggedItem is GroupHeaderViewModel)
        {
            int blockCount = GetGroupBlockCount(vm, sourceIndex);
            if (finalIndex > sourceIndex && finalIndex < sourceIndex + blockCount)
            {
                return false;
            }

            int adjustedNewIndex = finalIndex > sourceIndex
                ? finalIndex - blockCount + 1
                : finalIndex;

            return adjustedNewIndex != sourceIndex;
        }

        return finalIndex != sourceIndex;
    }

    private static int GetGroupBlockCount(SettingsViewModel vm, int sourceIndex)
    {
        int blockCount = 1;

        for (int i = sourceIndex + 1; i < vm.NavigationItems.Count; i++)
        {
            if (vm.NavigationItems[i] is GroupHeaderViewModel)
            {
                break;
            }

            if (vm.NavigationItems[i] is TerminalConfig)
            {
                blockCount++;
            }
        }

        return blockCount;
    }

    private static int CalculateFinalIndex(int sourceIndex, int targetIndex, DropInsertionPlacement placement)
    {
        int finalIndex = targetIndex;

        if (sourceIndex < targetIndex)
        {
            finalIndex = placement == DropInsertionPlacement.Before ? targetIndex - 1 : targetIndex;
        }
        else if (sourceIndex > targetIndex)
        {
            finalIndex = placement == DropInsertionPlacement.Before ? targetIndex : targetIndex + 1;
        }

        return finalIndex;
    }

    private static bool TryGetLastVisibleMovableItem(ListBox listBox, SettingsViewModel vm, out ListBoxItem item)
    {
        for (int i = vm.NavigationItems.Count - 1; i >= FixedNavigationItemCount; i--)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem container
                && container.Visibility == Visibility.Visible)
            {
                item = container;
                return true;
            }
        }

        item = null!;
        return false;
    }

    private static bool TryGetDraggedNavigationItem(IDataObject data, out object draggedItem)
    {
        draggedItem = data.GetData(typeof(TerminalConfig))
            ?? data.GetData(typeof(GroupHeaderViewModel));

        if (draggedItem == null)
        {
            draggedItem = null!;
            return false;
        }

        return true;
    }

    private static bool IsPointWithin(FrameworkElement element, Point point)
    {
        return point.X >= 0
            && point.Y >= 0
            && point.X <= element.ActualWidth
            && point.Y <= element.ActualHeight;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
            {
                return parent;
            }

            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private static bool IsFixedNavigationItem(object? item)
    {
        return item is GlobalSettingsViewModel
            or CustomCommandsViewModel
            or ThemesViewModel;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (SaveSettings())
        {
            this.DialogResult = true;
            this.Close();
        }
    }

    private void SaveOnlyButton_Click(object sender, RoutedEventArgs e)
    {
        if (SaveSettings())
        {
            // Since we are not closing, we need to manually trigger the reload on MainViewModel 
            // because ShowDialog() hasn't returned yet.
            _mainVm.ReloadSessions();
        }
    }

    private bool SaveSettings()
    {
        if (this.DataContext is SettingsViewModel vm)
        {
            if (!vm.GlobalSettings.TryPrepareRemotePasswordHash(
                    ConfigManager.Instance.Config.RemotePasswordHash,
                    out string? remotePasswordHashOverride,
                    out string remotePasswordError))
            {
                System.Windows.MessageBox.Show(
                    remotePasswordError,
                    "Remote Web Console Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return false;
            }

            CustomCommandValidationResult validation = CustomCommandParser.Validate(vm.CustomCommands.CustomCommandsString);
            if (!validation.IsValid)
            {
                System.Windows.MessageBox.Show(
                    validation.BuildMessage(),
                    "Custom Command Block Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return false;
            }

            vm.Save(remotePasswordHashOverride);
            vm.GlobalSettings.ClearRemotePasswordInputs();
            ClearRemotePasswordTextBoxes(this);
            return true;
        }

        return false;
    }

    private void RemotePasswordTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        bool isConfirm = string.Equals(textBox.Tag as string, "Confirm", StringComparison.Ordinal);
        vm.GlobalSettings.SetRemotePasswordInput(textBox.Text, isConfirm);
    }

    private static void ClearRemotePasswordTextBoxes(DependencyObject root)
    {
        foreach (System.Windows.Controls.TextBox textBox in FindVisualChildren<System.Windows.Controls.TextBox>(root))
        {
            if (string.Equals(textBox.Tag as string, "Primary", StringComparison.Ordinal)
                || string.Equals(textBox.Tag as string, "Confirm", StringComparison.Ordinal))
            {
                textBox.Clear();
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        if (root == null)
        {
            yield break;
        }

        int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T typedChild)
            {
                yield return typedChild;
            }

            foreach (T descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private void InsertCustomCommandMacro_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string macroKey)
        {
            return;
        }

        System.Windows.Controls.TextBox? textBox = FindVisualChildren<System.Windows.Controls.TextBox>(this)
            .FirstOrDefault(candidate => string.Equals(candidate.Name, "CustomCommandsTextBox", StringComparison.Ordinal));
        if (textBox == null)
        {
            return;
        }

        CustomCommandInsertResult result = CustomCommandInsertHelper.InsertMacroAtCaret(
            textBox.Text,
            textBox.CaretIndex,
            macroKey);

        textBox.Text = result.Text;

        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            textBox.CaretIndex = result.CaretIndex;
            textBox.Focus();
        });
    }

    private void ThemeColorPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button
            || button.DataContext is not ThemeColorItemViewModel item)
        {
            return;
        }

        System.Windows.Media.Color currentColor = ParseColorOrFallback(
            item.Value,
            item.DefaultColor);
        System.Windows.Media.Color defaultColor = ParseColorOrFallback(
            item.DefaultColor,
            item.DefaultColor);

        ColorPickerWindow pickerWindow = new(currentColor, defaultColor)
        {
            Owner = this
        };

        if (pickerWindow.ShowDialog() != true)
        {
            return;
        }

        string selectedHex = UiColorHelper.NormalizeColorString(
            pickerWindow.SelectedHex,
            item.DefaultColor);

        item.Value = selectedHex;
    }

    private static System.Windows.Media.Color ParseColorOrFallback(string? rawValue, string fallbackValue)
    {
        string normalizedColor = UiColorHelper.NormalizeColorString(rawValue, fallbackValue);
        object? converted = System.Windows.Media.ColorConverter.ConvertFromString(normalizedColor);
        return converted is System.Windows.Media.Color color
            ? color
            : System.Windows.Media.Colors.Orange;
    }

    private void InsertText_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string textToInsert)
        {
            // Find parent StackPanel (which is the root of the DataTemplate)
            // Button is in Grid -> StackPanel
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(button); // Grid
            if (parent != null)
            {
                parent = System.Windows.Media.VisualTreeHelper.GetParent(parent); // StackPanel
            }
            
            if (parent is System.Windows.Controls.StackPanel stackPanel)
            {
                // Find TextBox named "StartupCommandBox"
                var textBox = stackPanel.Children.OfType<System.Windows.Controls.TextBox>()
                                     .FirstOrDefault(t => t.Name == "StartupCommandBox");
                
                if (textBox != null)
                {
                    int caretIndex = textBox.CaretIndex;
                    string currentText = textBox.Text; // Assuming not null
                    
                    if (string.IsNullOrEmpty(currentText)) currentText = "";

                    // Insert text
                    // If caret is at valid position
                    if (caretIndex < 0) caretIndex = 0;
                    if (caretIndex > currentText.Length) caretIndex = currentText.Length;

                    string newText = currentText.Insert(caretIndex, textToInsert);
                    textBox.Text = newText;
                    
                    // Move caret
                    textBox.CaretIndex = caretIndex + textToInsert.Length;
                    textBox.Focus();
                    
                    // Update Binding source
                    var binding = textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                    binding?.UpdateSource();
                }
            }
        }
    }

    private void Copyright_Click(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        OpenExternalLink(e.Uri?.AbsoluteUri ?? "https://github.com/eckalhub/TerminalShell");
        e.Handled = true;
    }

    private void TailscaleLink_Click(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        OpenExternalLink("https://tailscale.com/");
        e.Handled = true;
    }

    private void FrpLink_Click(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
    {
        OpenExternalLink("https://github.com/fatedier/frp");
        e.Handled = true;
    }

    private static void OpenExternalLink(string url)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private async void CopyRemoteAccessUrl_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button
            || DataContext is not SettingsViewModel vm)
        {
            return;
        }

        string url = vm.GlobalSettings.RemoteAccessUrl?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            System.Windows.MessageBox.Show(
                "Remote access URL is empty.",
                "Remote Web Console Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(url);
            const string copiedText = "Copied";
            string restoreToken = Guid.NewGuid().ToString("N");
            button.Tag = restoreToken;
            button.Content = copiedText;

            await Task.Delay(1400);

            if (string.Equals(button.Tag as string, restoreToken, StringComparison.Ordinal))
            {
                button.Content = "Copy";
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to copy remote access URL: {ex.Message}",
                "Remote Web Console Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void BrowseWorkingDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.DataContext is Models.TerminalConfig config)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Working Directory",
                InitialDirectory = System.IO.Directory.Exists(config.WorkingDirectory) ? config.WorkingDirectory : null
            };

            if (dialog.ShowDialog() == true)
            {
                config.WorkingDirectory = dialog.FolderName;
            }
        }
    }
    private void ClearImageDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.DataContext is ViewModels.GlobalSettingsViewModel settings)
        {
            string dir = settings.ClipboardImageDirectory;
            if (string.IsNullOrWhiteSpace(dir) || !System.IO.Directory.Exists(dir))
            {
                System.Windows.MessageBox.Show($"Directory does not exist:\n{dir}", "Information", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to delete all image files in this directory?\n\nDirectory: {dir}\n\nThis action cannot be undone!", 
                "Warning", 
                System.Windows.MessageBoxButton.YesNo, 
                System.Windows.MessageBoxImage.Warning, 
                System.Windows.MessageBoxResult.No);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    var extensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" };
                    int count = 0;
                    foreach (var ext in extensions)
                    {
                        var files = System.IO.Directory.GetFiles(dir, ext);
                        foreach (var file in files)
                        {
                            System.IO.File.Delete(file);
                            count++;
                        }
                    }
                    System.Windows.MessageBox.Show($"Cleanup completed! Deleted {count} files.", "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Cleanup failed: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }

    private void GlobalTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            // ========== Ctrl+X 剪切拦截 ==========
            if (e.Key == System.Windows.Input.Key.X && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            {
                if (textBox.SelectionLength > 0)
                {
                    try 
                    { 
                        var svc = new TerminalShell.Services.Clipboard.ClipboardService(new TerminalShell.Services.Clipboard.ClipboardConfigService(() => TerminalShell.Core.Config.ConfigManager.Instance.Config));
                        _ = svc.SetTextAsync(textBox.SelectedText);
                        textBox.SelectedText = ""; 
                    } 
                    catch (Exception ex) { TerminalShell.Core.SimpleLogger.LogError(ex, "[Cut] GlobalTextBox - 设置剪贴板失败"); }
                }
                e.Handled = true;
                return;
            }

            // ========== Ctrl+C 复制拦截 ==========
            if (e.Key == System.Windows.Input.Key.C && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            {
                if (textBox.SelectionLength > 0)
                {
                    try 
                    { 
                        var svc = new TerminalShell.Services.Clipboard.ClipboardService(new TerminalShell.Services.Clipboard.ClipboardConfigService(() => TerminalShell.Core.Config.ConfigManager.Instance.Config));
                        _ = svc.SetTextAsync(textBox.SelectedText); 
                    } 
                    catch (Exception ex) { TerminalShell.Core.SimpleLogger.LogError(ex, "[Copy] GlobalTextBox - 设置剪贴板失败"); }
                }
                e.Handled = true;
                return;
            }

            // ========== Ctrl+V 粘贴拦截 ==========
            if (e.Key == System.Windows.Input.Key.V && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            {
                try
                {
                    var svc = new TerminalShell.Services.Clipboard.ClipboardService(new TerminalShell.Services.Clipboard.ClipboardConfigService(() => TerminalShell.Core.Config.ConfigManager.Instance.Config));
                    _ = PasteAsync(textBox, svc);
                }
                catch (Exception ex) { TerminalShell.Core.SimpleLogger.LogError(ex, "[Paste] GlobalTextBox - 提取剪贴板失败"); }
                e.Handled = true;
                return;
            }
        }
    }

    private async System.Threading.Tasks.Task PasteAsync(System.Windows.Controls.TextBox textBox, TerminalShell.Services.Clipboard.ClipboardService svc)
    {
        var text = await svc.GetConvertedContentAsync();
        if (!string.IsNullOrEmpty(text))
        {
            textBox.SelectedText = text;
            textBox.CaretIndex += text.Length;
            textBox.SelectionLength = 0;
        }
    }
}
