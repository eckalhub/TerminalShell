using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Collections.Generic;
using TerminalShell.ViewModels;
using TerminalShell.Models;
using TerminalShell.Services.Clipboard;
using TerminalShell.Services;
using TerminalShell.Core;
using System.Linq;

namespace TerminalShell.Views;

public partial class MainWindow : Window
{
    private const double MinimumInputHeight = 21.0;
    private const double CommandPopupTopSafetyMargin = 4.0;
    private const double CommandPopupPlacementGap = 5.0;
    private int _shutdownSequenceStarted;
    private int _shutdownSequenceCompleted;
    private ClipboardService? _clipboardService;
    private readonly Dictionary<TerminalSession, HistoryWindow> _historyWindows = new();
    private readonly Dictionary<TerminalSession, System.Windows.Controls.TextBox> _sessionInputTextBoxes = new();
    private Rect _lastNormalVisibleBounds;
    private bool _hasLastNormalVisibleBounds;

    // ========== Input Resize Handle Drag State ==========
    private bool _isDraggingInputResize = false;
    private double _dragStartY;
    private double _dragStartHeight;

    public MainWindow()
    {
        InitializeComponent();
        RuntimeAppIdentity.ApplyWindowIcon(this);
        
        this.DataContext = new MainViewModel();
        SimpleLogger.Log("[MainWindow] Constructor completed and DataContext assigned.");

        // Config Integration
        this.Loaded += MainWindow_Loaded;
        this.Closing += MainWindow_Closing;
        this.StateChanged += MainWindow_StateChanged;
        this.LocationChanged += MainWindow_LocationChanged;
        this.SizeChanged += MainWindow_SizeChanged;
    }

    private void HeaderContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not ContextMenu contextMenu)
            {
                SimpleLogger.Log("[HeaderContextMenu] Opened skipped: sender is not ContextMenu.");
                return;
            }

            SimpleLogger.Log($"[HeaderContextMenu] Opened fired. PlacementTargetType={contextMenu.PlacementTarget?.GetType().FullName ?? "<null>"}");

            if (contextMenu.PlacementTarget is not FrameworkElement placementTarget)
            {
                SimpleLogger.Log("[HeaderContextMenu] Opened skipped: PlacementTarget is not FrameworkElement.");
                return;
            }

            if (placementTarget.DataContext is not TerminalSession session)
            {
                SimpleLogger.Log($"[HeaderContextMenu] Opened skipped: PlacementTarget.DataContext={placementTarget.DataContext?.GetType().FullName ?? "<null>"}");
                return;
            }

            if (DataContext is not MainViewModel viewModel)
            {
                SimpleLogger.Log($"[HeaderContextMenu] Opened skipped: Window.DataContext={DataContext?.GetType().FullName ?? "<null>"}");
                return;
            }

            SimpleLogger.Log($"[HeaderContextMenu] Building snapshot VM for session '{session.Name}'.");
            TerminalHeaderContextMenuViewModel snapshot = new(viewModel, session);
            contextMenu.DataContext = snapshot;
            SimpleLogger.Log($"[HeaderContextMenu] Snapshot VM assigned for session '{session.Name}'. Items={snapshot.GroupedMenuTerminals.Count}");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "HeaderContextMenu_Opened");
        }
    }

    private void SessionHeader_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        try
        {
            string sessionName = (sender as FrameworkElement)?.DataContext is TerminalSession session
                ? session.Name
                : "<null-session>";
            string sourceType = e.OriginalSource?.GetType().FullName ?? "<null-source>";
            SimpleLogger.Log($"[HeaderContextMenu] PreviewMouseRightButtonUp Session='{sessionName}' Source={sourceType} Handled={e.Handled}");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "SessionHeader_PreviewMouseRightButtonUp");
        }
    }

    private void SessionHeader_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        try
        {
            FrameworkElement? element = sender as FrameworkElement;
            string sessionName = element?.DataContext is TerminalSession session
                ? session.Name
                : "<null-session>";
            string contextMenuType = element?.ContextMenu?.GetType().FullName ?? "<null-context-menu>";
            SimpleLogger.Log($"[HeaderContextMenu] ContextMenuOpening Session='{sessionName}' ContextMenuType={contextMenuType} Handled={e.Handled}");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "SessionHeader_ContextMenuOpening");
        }
    }

    private void HeaderContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not ContextMenu contextMenu)
            {
                SimpleLogger.Log("[HeaderContextMenu] Closed skipped: sender is not ContextMenu.");
                return;
            }

            SimpleLogger.Log("[HeaderContextMenu] Closed fired. Clearing snapshot DataContext.");
            contextMenu.DataContext = null;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "HeaderContextMenu_Closed");
        }
    }

    /// <summary>
    /// 获取或创建 ClipboardService 实例（延迟初始化）
    /// </summary>
    private ClipboardService GetClipboardService()
    {
        if (_clipboardService == null)
        {
            var configService = new ClipboardConfigService(
                () => Core.Config.ConfigManager.Instance.Config);
            _clipboardService = new ClipboardService(configService);
        }
        return _clipboardService;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = TerminalShell.Core.Config.ConfigManager.Instance;
            config.Load();

            var workingAreas = System.Windows.Forms.Screen.AllScreens
                .Select(screen => new Rect(
                    screen.WorkingArea.Left,
                    screen.WorkingArea.Top,
                    screen.WorkingArea.Width,
                    screen.WorkingArea.Height))
                .ToList();
            Rect primaryWorkingArea = System.Windows.Forms.Screen.PrimaryScreen is { } primaryScreen
                ? new Rect(
                    primaryScreen.WorkingArea.Left,
                    primaryScreen.WorkingArea.Top,
                    primaryScreen.WorkingArea.Width,
                    primaryScreen.WorkingArea.Height)
                : Rect.Empty;
            MainWindowBoundsRepairResult resolvedBounds = MainWindowLayout.NormalizeSavedBounds(
                config.Config.WindowLeft,
                config.Config.WindowTop,
                config.Config.WindowWidth,
                config.Config.WindowHeight,
                workingAreas,
                primaryWorkingArea);

            if (resolvedBounds.WasRepaired)
            {
                SimpleLogger.Log(
                    $"[LAYOUT] Main window bounds repaired. Reason={resolvedBounds.Reason} " +
                    $"Old=[{config.Config.WindowLeft},{config.Config.WindowTop} {config.Config.WindowWidth}x{config.Config.WindowHeight}] " +
                    $"New=[{resolvedBounds.Bounds.Left},{resolvedBounds.Bounds.Top} {resolvedBounds.Bounds.Width}x{resolvedBounds.Bounds.Height}]");

                config.Config.WindowLeft = resolvedBounds.Bounds.Left;
                config.Config.WindowTop = resolvedBounds.Bounds.Top;
                config.Config.WindowWidth = resolvedBounds.Bounds.Width;
                config.Config.WindowHeight = resolvedBounds.Bounds.Height;
                config.Save();
            }

            this.Left = resolvedBounds.Bounds.Left;
            this.Top = resolvedBounds.Bounds.Top;
            this.Width = resolvedBounds.Bounds.Width;
            this.Height = resolvedBounds.Bounds.Height;
            UpdateLastNormalVisibleBounds();
        }
        catch (System.Exception ex) { SimpleLogger.LogError(ex, "MainWindow_Loaded"); }
    }

    private void MainWindow_LocationChanged(object? sender, EventArgs e)
    {
        UpdateLastNormalVisibleBounds();
        RefreshOpenCommandPopupLayouts();
    }

    private void MainWindow_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateLastNormalVisibleBounds();
        RefreshOpenCommandPopupLayouts();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (Interlocked.CompareExchange(ref _shutdownSequenceCompleted, 0, 0) != 0)
        {
            return;
        }

        App? app = System.Windows.Application.Current as App;

        if (app?.IsFatalShutdownInProgress == true)
        {
            SimpleLogger.Log("[SHUTDOWN] MainWindow_Closing bypassed because fatal shutdown is already in progress.");
            return;
        }

        if (app?.IsSystemSessionEnding == true)
        {
            try
            {
                SavePersistableWindowBounds();
                SimpleLogger.Log("[SHUTDOWN] MainWindow_Closing allowing Windows session ending without tray interception.");
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "MainWindow_Closing SessionEnding");
            }

            return;
        }

        if (ShouldHideCloseRequestToTray(app))
        {
            e.Cancel = true;
            HideMainWindowToTray("CloseToTrayInsteadOfExit");
            return;
        }

        if (DataContext is MainViewModel confirmationVm
            && confirmationVm.TryBuildShutdownConfirmationMessage(out string confirmationMessage))
        {
            MessageBoxResult confirmationResult = System.Windows.MessageBox.Show(
                this,
                confirmationMessage,
                "Confirm Close",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirmationResult != MessageBoxResult.Yes)
            {
                app?.CancelExplicitExitRequest();
                e.Cancel = true;
                return;
            }
        }

        if (Interlocked.Exchange(ref _shutdownSequenceStarted, 1) != 0)
        {
            e.Cancel = true;
            return;
        }

        try
        {
            e.Cancel = true;

            SavePersistableWindowBounds();

            System.Windows.Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            this.IsEnabled = false;
            this.ShowInTaskbar = false;
            this.Hide();

            _ = CompleteShutdownAsync();
        }
        catch (System.Exception ex) { SimpleLogger.LogError(ex, "MainWindow_Closing"); }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            UpdateLastNormalVisibleBounds();
            return;
        }

        if (Interlocked.CompareExchange(ref _shutdownSequenceStarted, 0, 0) != 0)
        {
            return;
        }

        try
        {
            if (!TerminalShell.Core.Config.ConfigManager.Instance.Config.MinimizeMainWindowToTray)
            {
                return;
            }

            HideMainWindowToTray("MinimizeToTray");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "MainWindow_StateChanged");
        }
    }

    private bool ShouldHideCloseRequestToTray(App? app)
    {
        if (app?.IsExplicitExitRequested == true)
        {
            return false;
        }

        return TerminalShell.Core.Config.ConfigManager.Instance.Config.CloseMainWindowToTrayInsteadOfExit;
    }

    private void HideMainWindowToTray(string reason)
    {
        SimpleLogger.Log($"[TRAY] Hiding main window to tray. Reason={reason} State={WindowState} Visibility={Visibility}");
        ShowInTaskbar = false;
        Hide();
    }

    internal void HideToTrayForStartupLaunch()
    {
        if (Interlocked.CompareExchange(ref _shutdownSequenceStarted, 0, 0) != 0)
        {
            return;
        }

        ShowInTaskbar = false;
        HideMainWindowToTray("StartupLaunchToTray");
    }

    private void UpdateLastNormalVisibleBounds()
    {
        if (!IsLoaded || Visibility != Visibility.Visible || WindowState != WindowState.Normal)
        {
            return;
        }

        _lastNormalVisibleBounds = new Rect(Left, Top, Width, Height);
        _hasLastNormalVisibleBounds = true;
    }

    private Rect GetPersistableWindowBounds()
    {
        if (_hasLastNormalVisibleBounds)
        {
            return _lastNormalVisibleBounds;
        }

        return new Rect(Left, Top, Width, Height);
    }

    private void SavePersistableWindowBounds()
    {
        Rect bounds = GetPersistableWindowBounds();
        var config = TerminalShell.Core.Config.ConfigManager.Instance;

        config.Config.WindowLeft = bounds.Left;
        config.Config.WindowTop = bounds.Top;
        config.Config.WindowWidth = bounds.Width;
        config.Config.WindowHeight = bounds.Height;
        config.Save();
    }

    private async Task CompleteShutdownAsync()
    {
        try
        {
            if (this.DataContext is MainViewModel vm)
            {
                await vm.CleanupAsync();
            }
        }
        catch (System.Exception ex)
        {
            SimpleLogger.LogError(ex, "CompleteShutdownAsync");
        }
        finally
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Interlocked.Exchange(ref _shutdownSequenceCompleted, 1);
                System.Windows.Application.Current.Shutdown();
            });
        }
    }

    // ========== Input Resize Handle Drag Logic ==========

    /// <summary>
    /// 判断鼠标事件源是否位于拖拽手柄 (Tag="ResizeHandle") 内
    /// </summary>
    private static bool IsResizeHandle(DependencyObject? obj)
    {
        DependencyObject? current = obj;
        while (current != null)
        {
            if (current is FrameworkElement fe && fe.Tag is string tag && tag == "ResizeHandle")
                return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        if (!IsResizeHandle(e.OriginalSource as DependencyObject)) return;

        _isDraggingInputResize = true;
        _dragStartY = e.GetPosition(this).Y;
        _dragStartHeight = (DataContext is MainViewModel vm) ? vm.InputMinHeight : MinimumInputHeight;
        (e.OriginalSource as UIElement)?.CaptureMouse();
        e.Handled = true;
    }

    protected override void OnPreviewMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (!_isDraggingInputResize) return;

        double delta = _dragStartY - e.GetPosition(this).Y; // 向上拖为正（增高）
        double newHeight = Math.Clamp(_dragStartHeight + delta, MinimumInputHeight, 500.0);

        if (DataContext is MainViewModel vm)
            vm.InputMinHeight = newHeight;
    }

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (!_isDraggingInputResize) return;

        _isDraggingInputResize = false;
        Mouse.Capture(null); // 释放鼠标捕获

        // 持久化到 config
        if (DataContext is MainViewModel vm)
        {
            try
            {
                Core.Config.ConfigManager.Instance.Config.InputMinHeight = vm.InputMinHeight;
                Core.Config.ConfigManager.Instance.Save();
            }
            catch (System.Exception ex) { SimpleLogger.LogError(ex, "[ResizeHandle] 保存 InputMinHeight 失败"); }
        }
    }
    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TerminalShell.Models.TerminalSession session)
        {
            session.SendCommand();
        }
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TerminalShell.Models.TerminalSession session)
        {
            if (_historyWindows.TryGetValue(session, out HistoryWindow? existingWindow))
            {
                if (existingWindow.WindowState == WindowState.Minimized)
                {
                    existingWindow.WindowState = WindowState.Normal;
                }

                existingWindow.Topmost = this.Topmost;
                existingWindow.Activate();
                return;
            }

            var historyWindow = new HistoryWindow(session)
            {
                Owner = this,
                Topmost = this.Topmost
            };

            historyWindow.Closed += (_, _) =>
            {
                _historyWindows.Remove(session);
            };

            _historyWindows[session] = historyWindow;
            historyWindow.Show();
            historyWindow.Activate();
        }
    }

    // Track last Enter key press for Double-Enter detection
    private DateTime _lastEnterTime = DateTime.MinValue;

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var textBox = sender as System.Windows.Controls.TextBox;
        var session = (sender as FrameworkElement)?.DataContext as TerminalShell.Models.TerminalSession;
        if (textBox == null || session == null) return;

        ModifierKeys modifiers = Keyboard.Modifiers;

        string? functionCategoryKey = GetFunctionCategoryKey(e.Key);
        if (functionCategoryKey != null && modifiers == ModifierKeys.None)
        {
            OpenFunctionCategoryPopup(textBox, session, functionCategoryKey);
            e.Handled = true;
            return;
        }

        // ========== IntelliSense Intercept ==========
        if (session.IsCommandPopupOpen)
        {
            if (session.CommandPopupMode == CommandPopupMode.FunctionCategory && e.Key == System.Windows.Input.Key.Back)
            {
                if (!string.IsNullOrEmpty(session.CommandPopupFilterText))
                {
                    session.CommandPopupFilterText = session.CommandPopupFilterText[..^1];
                    RefreshFunctionCategoryPopup(textBox, session);
                }
                else
                {
                    session.IsCommandPopupOpen = false;
                }

                e.Handled = true;
                return;
            }

            if (e.Key == System.Windows.Input.Key.Up)
            {
                if (session.SelectedCommandIndex > 0) session.SelectedCommandIndex--;
                e.Handled = true;
                return;
            }
            if (e.Key == System.Windows.Input.Key.Down)
            {
                if (session.SelectedCommandIndex < session.FilteredCommands.Count - 1) session.SelectedCommandIndex++;
                e.Handled = true;
                return;
            }
            if (e.Key == System.Windows.Input.Key.Tab && (modifiers & ModifierKeys.Shift) != 0)
            {
                SimpleLogger.Log($"[CommandPopup] ShiftTabBypass Session='{session.Name}' Mode={session.CommandPopupMode}");
                return;
            }
            if ((e.Key == System.Windows.Input.Key.Tab && modifiers == ModifierKeys.None) || e.Key == System.Windows.Input.Key.Enter)
            {
                if (session.SelectedCommandIndex >= 0 && session.SelectedCommandIndex < session.FilteredCommands.Count)
                {
                    SimpleLogger.Log($"[CommandPopup] Commit Session='{session.Name}' Key={e.Key} Index={session.SelectedCommandIndex}");
                    CommitPopupSelection(textBox, session, session.FilteredCommands[session.SelectedCommandIndex]);
                }
                else if (session.CommandPopupMode == CommandPopupMode.FunctionCategory)
                {
                    SimpleLogger.Log($"[CommandPopup] CloseWithoutSelection Session='{session.Name}' Key={e.Key}");
                    session.IsCommandPopupOpen = false;
                }
                e.Handled = true;
                return;
            }
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                session.IsCommandPopupOpen = false;
                e.Handled = true;
                return;
            }
        }
        // ========== Ctrl+X 剪切拦截 ==========
        if (e.Key == System.Windows.Input.Key.X && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
        {
            if (textBox.SelectionLength > 0)
            {
                try 
                { 
                    _ = GetClipboardService().SetTextAsync(textBox.SelectedText);
                    textBox.SelectedText = ""; 
                } 
                catch (Exception ex) { SimpleLogger.LogError(ex, "[Cut] 设置剪贴板失败"); }
            }
            e.Handled = true;
            return;
        }

        // ========== Ctrl+C 复制拦截 ==========
        if (e.Key == System.Windows.Input.Key.C && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
        {
            if (textBox.SelectionLength > 0)
            {
                try { _ = GetClipboardService().SetTextAsync(textBox.SelectedText); } 
                catch (Exception ex) { SimpleLogger.LogError(ex, "[Copy] 设置剪贴板失败"); }
            }
            e.Handled = true;
            return;
        }

        // ========== Ctrl+V 粘贴拦截 ==========
        if (e.Key == System.Windows.Input.Key.V &&
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
        {
            SimpleLogger.Log("[Paste] ========== Ctrl+V 按键检测到 ==========");

            try
            {
                var config = Core.Config.ConfigManager.Instance.Config;
                SimpleLogger.Log($"[Paste] ClipboardConversionEnabled = {config.ClipboardConversionEnabled}");

                if (!config.ClipboardConversionEnabled)
                {
                    SimpleLogger.Log("[Paste] 功能未启用，放行默认粘贴");
                    return; // 放行
                }

                // 同步检测剪贴板内容类型
                var detector = new Services.Clipboard.ClipboardDetector();
                var contentType = detector.GetPrimaryContentType();
                SimpleLogger.Log($"[Paste] 检测到内容类型: {contentType}");

                bool needConvert = contentType == Models.ClipboardContentType.Image ||
                                   contentType == Models.ClipboardContentType.Html ||
                                   contentType == Models.ClipboardContentType.RichText ||
                                   contentType == Models.ClipboardContentType.Files;

                if (!needConvert)
                {
                    SimpleLogger.Log("[Paste] 不需要转换（纯文本或未知），放行默认粘贴");
                    return; // 放行
                }

                // 拦截默认粘贴行为
                e.Handled = true;
                SimpleLogger.Log("[Paste] 已拦截 Ctrl+V，开始异步转换...");

                if (textBox == null)
                {
                    SimpleLogger.Log("[Paste] sender 不是 TextBox，退出");
                    return;
                }

                // 同步记录光标状态
                var caretIndex = textBox.CaretIndex;
                var selectionStart = textBox.SelectionStart;
                var selectionLength = textBox.SelectionLength;
                var currentText = textBox.Text ?? string.Empty;
                SimpleLogger.Log($"[Paste] 光标: {caretIndex}, 选中: {selectionStart}+{selectionLength}, 文本长度: {currentText.Length}");

                // 异步执行转换
                _ = ConvertAndInsertAsync(textBox, currentText, caretIndex, selectionStart, selectionLength);
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "[Paste] Ctrl+V 处理异常");
            }

            return; // 不继续处理 Enter 逻辑
        }

        // ========== Submit Trigger Mode (Single Enter vs Double Enter) ==========
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            // 1. Shift+Enter -> ALWAYS Newline. Let it fall through, do not trigger submit.
            if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0)
            {
                // Optionally reset double enter tracker just in case
                _lastEnterTime = DateTime.MinValue;
                return; // Normal TextBox behavior (Newline)
            }

            var configMode = Core.Config.ConfigManager.Instance.Config.SubmitTriggerMode;

            if (configMode == "SingleEnter")
            {
                if ((sender as FrameworkElement)?.DataContext is TerminalShell.Models.TerminalSession localSession)
                {
                    e.Handled = true; // Prevent the TextBox from inserting a newline
                    localSession.SendCommand();
                }
            }
            else // Default to DoubleEnter
            {
                var now = DateTime.Now;
                double diff = (now - _lastEnterTime).TotalMilliseconds;
                bool isDoubleEnter = diff < 400;
                
                _lastEnterTime = now;

                if (isDoubleEnter)
                {
                    if ((sender as FrameworkElement)?.DataContext is TerminalShell.Models.TerminalSession localSession)
                    {
                        // Clean up: Remove the trailing newline added by the *first* Enter of the double-tap
                        if (!string.IsNullOrEmpty(localSession.InputBuffer))
                        {
                            localSession.InputBuffer = localSession.InputBuffer.TrimEnd('\r', '\n');
                        }
                        
                        localSession.SendCommand();
                    }
                    e.Handled = true; // Suppress the *second* newline
                }
                // Else: Let it fall through -> specific behavior:
                // Single Enter -> TextBox processes it as a standard Newline (AcceptsReturn=True)
            }
        }
    }

    private void InputBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var textBox = sender as System.Windows.Controls.TextBox;
        var session = (sender as FrameworkElement)?.DataContext as TerminalShell.Models.TerminalSession;
        if (textBox == null || session == null || !session.IsCommandPopupOpen || session.CommandPopupMode != CommandPopupMode.FunctionCategory)
        {
            return;
        }

        if (!string.IsNullOrEmpty(e.Text))
        {
            session.CommandPopupFilterText += e.Text;
            RefreshFunctionCategoryPopup(textBox, session);
            e.Handled = true;
        }
    }

    /// <summary>
    /// 异步转换剪贴板内容并插入到 TextBox
    /// </summary>
    private async Task ConvertAndInsertAsync(
        System.Windows.Controls.TextBox textBox,
        string currentText,
        int caretIndex,
        int selectionStart,
        int selectionLength)
    {
        try
        {
            SimpleLogger.Log("[Paste] ConvertAndInsertAsync 开始执行");

            var service = GetClipboardService();
            SimpleLogger.Log("[Paste] ClipboardService 已获取");

            var convertedText = await service.GetConvertedContentAsync();
            SimpleLogger.Log($"[Paste] 转换结果: 长度={convertedText?.Length ?? 0}, 内容前100字={convertedText?.Substring(0, Math.Min(100, convertedText?.Length ?? 0))}");

            if (string.IsNullOrEmpty(convertedText))
            {
                SimpleLogger.Log("[Paste] 转换结果为空，不执行插入");
                return;
            }

            // 回到 UI 线程执行插入
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var insertText = currentText;
                    var insertIndex = caretIndex;

                    if (selectionLength > 0)
                    {
                        insertText = insertText.Remove(selectionStart, selectionLength);
                        insertIndex = selectionStart;
                    }

                    textBox.Text = insertText.Insert(insertIndex, convertedText);
                    textBox.CaretIndex = insertIndex + convertedText.Length;

                    SimpleLogger.Log($"[Paste] 插入完成，{convertedText.Length} 字符到位置 {insertIndex}");
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError(ex, "[Paste] UI线程插入异常");
                }
            });
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "[Paste] ConvertAndInsertAsync 异常");
        }
    }

    // ========== IntelliSense / Slash Commands System ==========

    private void InputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var textBox = sender as System.Windows.Controls.TextBox;
        var session = (sender as FrameworkElement)?.DataContext as TerminalShell.Models.TerminalSession;
        if (textBox == null || session == null) return;

        if (session.IsAutoDraftQueueActive && textBox.IsKeyboardFocusWithin)
        {
            if (!session.TryConsumeTextEditNotificationSuppression())
            {
                session.NotifyTextEditedByUser();
            }
        }

        if (session.CommandPopupMode == CommandPopupMode.FunctionCategory)
        {
            return;
        }

        string text = textBox.Text;
        int caretIndex = textBox.CaretIndex;

        // Find the last '/' before CaretIndex
        int slashIndex = -1;
        for (int i = caretIndex - 1; i >= 0; i--)
        {
            if (text[i] == '/')
            {
                slashIndex = i;
                break;
            }
            if (char.IsWhiteSpace(text[i])) // Break if there is space between caret and slash
            {
                break;
            }
        }

        if (slashIndex >= 0)
        {
            string searchPrefix = text.Substring(slashIndex + 1, caretIndex - slashIndex - 1);
            RefreshSlashCommandPopup(textBox, session, searchPrefix);
        }
        else
        {
            session.IsCommandPopupOpen = false;
        }
    }

    private void CommitPopupSelection(System.Windows.Controls.TextBox textBox, TerminalShell.Models.TerminalSession session, TerminalShell.Models.CustomCommandItem item)
    {
        // 宏解析: 将 [return] 标签替换为真正的换行符
        string command = item.FullText.Replace("[return]", "\r\n");

        if (session.CommandPopupMode == CommandPopupMode.FunctionCategory)
        {
            InsertCommandAtCaret(textBox, command);
        }
        else
        {
            InsertSlashCommand(textBox, command);
        }

        session.IsCommandPopupOpen = false;
    }

    private void InsertSlashCommand(System.Windows.Controls.TextBox textBox, string command)
    {
        string text = textBox.Text;
        int caretIndex = textBox.CaretIndex;

        int slashIndex = -1;
        for (int i = caretIndex - 1; i >= 0; i--)
        {
            if (text[i] == '/')
            {
                slashIndex = i;
                break;
            }
        }

        if (slashIndex >= 0)
        {
            string newText = text.Remove(slashIndex, caretIndex - slashIndex);
            newText = newText.Insert(slashIndex, command);
            
            textBox.Text = newText;
            textBox.CaretIndex = slashIndex + command.Length;
        }
    }

    private static void InsertCommandAtCaret(System.Windows.Controls.TextBox textBox, string command)
    {
        string currentText = textBox.Text ?? string.Empty;
        int selectionStart = textBox.SelectionStart;
        int selectionLength = textBox.SelectionLength;

        if (selectionLength > 0)
        {
            currentText = currentText.Remove(selectionStart, selectionLength);
        }

        currentText = currentText.Insert(selectionStart, command);
        textBox.Text = currentText;
        textBox.CaretIndex = selectionStart + command.Length;
    }

    private void CommandPopup_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.ListBox listBox && 
            listBox.SelectedItem is TerminalShell.Models.CustomCommandItem item && 
            listBox.DataContext is TerminalShell.Models.TerminalSession session)
        {
            // Traverse up to find the Popup housing this ListBox
            DependencyObject parent = listBox;
            while (parent != null && !(parent is System.Windows.Controls.Primitives.Popup))
            {
                parent = LogicalTreeHelper.GetParent(parent) ?? System.Windows.Media.VisualTreeHelper.GetParent(parent);
            }
            
            if (parent is System.Windows.Controls.Primitives.Popup popup && popup.PlacementTarget is System.Windows.Controls.TextBox textBox)
            {
                CommitPopupSelection(textBox, session, item);
                textBox.Focus();
                e.Handled = true;
            }
        }
    }

    private void RefreshSlashCommandPopup(System.Windows.Controls.TextBox textBox, TerminalSession session, string searchPrefix)
    {
        var config = Core.Config.ConfigManager.Instance.Config;
        var parsedCommands = CustomCommandParser.Parse(config.CustomCommandsString);
        var matches = parsedCommands
            .Where(cmd => cmd.CommandText.Contains(searchPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!matches.Any())
        {
            session.IsCommandPopupOpen = false;
            return;
        }

        session.CommandPopupMode = CommandPopupMode.SlashSearch;
        session.CommandPopupCategoryKey = string.Empty;
        session.CommandPopupFilterText = string.Empty;
        UpdateCommandPopupMaxHeight(textBox, session);
        PopulateCommandPopup(session, matches, includeCategoryPrefix: true, keepOpenWhenEmpty: false, config.CustomCommandMaxDisplayLength);
    }

    private void OpenFunctionCategoryPopup(System.Windows.Controls.TextBox textBox, TerminalSession session, string categoryKey)
    {
        session.CommandPopupMode = CommandPopupMode.FunctionCategory;
        session.CommandPopupCategoryKey = categoryKey;
        session.CommandPopupFilterText = string.Empty;
        RefreshFunctionCategoryPopup(textBox, session);
    }

    private void RefreshFunctionCategoryPopup(System.Windows.Controls.TextBox textBox, TerminalSession session)
    {
        if (string.IsNullOrWhiteSpace(session.CommandPopupCategoryKey))
        {
            session.IsCommandPopupOpen = false;
            return;
        }

        var config = Core.Config.ConfigManager.Instance.Config;
        var parsedCommands = CustomCommandParser.Parse(config.CustomCommandsString);
        var matches = parsedCommands
            .Where(cmd => string.Equals(cmd.CategoryKey, session.CommandPopupCategoryKey, StringComparison.OrdinalIgnoreCase))
            .Where(cmd => string.IsNullOrWhiteSpace(session.CommandPopupFilterText) || cmd.CommandText.Contains(session.CommandPopupFilterText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        bool keepOpenWhenEmpty = !string.IsNullOrWhiteSpace(session.CommandPopupFilterText);
        if (matches.Count == 0 && !keepOpenWhenEmpty)
        {
            session.IsCommandPopupOpen = false;
            return;
        }

        UpdateCommandPopupMaxHeight(textBox, session);
        PopulateCommandPopup(session, matches, includeCategoryPrefix: false, keepOpenWhenEmpty: keepOpenWhenEmpty, config.CustomCommandMaxDisplayLength);
    }

    private static void PopulateCommandPopup(
        TerminalSession session,
        List<ParsedCustomCommand> matches,
        bool includeCategoryPrefix,
        bool keepOpenWhenEmpty,
        int maxDisplayLength)
    {
        int effectiveMaxLength = maxDisplayLength > 0 ? maxDisplayLength : 100;

        session.FilteredCommands.Clear();
        foreach (var match in matches)
        {
            string display = includeCategoryPrefix && !string.IsNullOrWhiteSpace(match.CategoryKey)
                ? $"[{match.CategoryKey}] {match.CommandText}"
                : match.CommandText;

            if (display.Length > effectiveMaxLength)
            {
                display = display.Substring(0, effectiveMaxLength) + "...";
            }

            session.FilteredCommands.Add(new TerminalShell.Models.CustomCommandItem
            {
                FullText = match.CommandText,
                DisplayText = display,
                CategoryKey = match.CategoryKey
            });
        }

        if (matches.Count == 0 && !keepOpenWhenEmpty)
        {
            session.IsCommandPopupOpen = false;
            return;
        }

        session.IsCommandPopupOpen = true;
        session.SelectedCommandIndex = matches.Count > 0 ? 0 : -1;
    }

    private static string? GetFunctionCategoryKey(Key key)
    {
        return key switch
        {
            Key.F1 => "F1",
            Key.F2 => "F2",
            Key.F3 => "F3",
            Key.F4 => "F4",
            Key.F5 => "F5",
            Key.F6 => "F6",
            Key.F7 => "F7",
            Key.F8 => "F8",
            Key.F9 => "F9",
            Key.F10 => "F10",
            Key.F11 => "F11",
            Key.F12 => "F12",
            _ => null
        };
    }

    private void InputBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox
            || textBox.DataContext is not TerminalSession session)
        {
            return;
        }

        _sessionInputTextBoxes[session] = textBox;
        if (session.IsCommandPopupOpen)
        {
            UpdateCommandPopupMaxHeight(textBox, session);
        }
    }

    private void InputBox_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox
            || textBox.DataContext is not TerminalSession session)
        {
            return;
        }

        if (_sessionInputTextBoxes.TryGetValue(session, out System.Windows.Controls.TextBox? existingTextBox)
            && ReferenceEquals(existingTextBox, textBox))
        {
            _sessionInputTextBoxes.Remove(session);
        }
    }

    private void InputBox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox
            || textBox.DataContext is not TerminalSession session
            || !session.IsCommandPopupOpen)
        {
            return;
        }

        UpdateCommandPopupMaxHeight(textBox, session);
    }

    private void CommandPopup_Opened(object sender, EventArgs e)
    {
        if (sender is not System.Windows.Controls.Primitives.Popup popup
            || popup.PlacementTarget is not System.Windows.Controls.TextBox textBox
            || textBox.DataContext is not TerminalSession session)
        {
            return;
        }

        UpdateCommandPopupMaxHeight(textBox, session);
    }

    private void RefreshOpenCommandPopupLayouts()
    {
        foreach ((TerminalSession session, System.Windows.Controls.TextBox textBox) in _sessionInputTextBoxes)
        {
            if (session.IsCommandPopupOpen && textBox.IsLoaded)
            {
                UpdateCommandPopupMaxHeight(textBox, session);
            }
        }
    }

    private void UpdateCommandPopupMaxHeight(System.Windows.Controls.TextBox textBox, TerminalSession session)
    {
        const double fallbackHeight = 300.0;

        try
        {
            if (!textBox.IsLoaded)
            {
                session.CommandPopupMaxHeight = fallbackHeight;
                return;
            }

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero || !Interop.NativeMethods.GetWindowRect(hwnd, out Interop.NativeMethods.RECT rect))
            {
                session.CommandPopupMaxHeight = fallbackHeight;
                return;
            }

            PresentationSource? source = PresentationSource.FromVisual(this);
            Matrix toDevice = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            double pixelsPerDipY = toDevice.M22 > 0 ? toDevice.M22 : 1.0;

            System.Windows.Point textBoxOriginDip = textBox.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));
            System.Windows.Point textBoxOriginPx = toDevice.Transform(textBoxOriginDip);

            double anchorLeftPx = rect.Left + textBoxOriginPx.X;
            double anchorTopPx = rect.Top + textBoxOriginPx.Y;
            var anchorPoint = new System.Drawing.Point(
                (int)Math.Round(anchorLeftPx),
                (int)Math.Round(anchorTopPx));

            var screen = System.Windows.Forms.Screen.FromPoint(anchorPoint);
            session.CommandPopupMaxHeight = CommandPopupLayoutCalculator.CalculateTopPlacementMaxHeight(
                anchorTopPx,
                screen.WorkingArea.Top,
                pixelsPerDipY,
                CommandPopupTopSafetyMargin,
                CommandPopupPlacementGap);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "UpdateCommandPopupMaxHeight");
            session.CommandPopupMaxHeight = fallbackHeight;
        }
    }
}
