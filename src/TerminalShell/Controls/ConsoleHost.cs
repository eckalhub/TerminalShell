using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TerminalShell.Interop;
using TerminalShell.Core;
using TerminalShell.Models;
using System.Runtime.InteropServices;
using System.Linq;

namespace TerminalShell.Controls;

/// <summary>
/// v2.0 - Overlay Architecture (彻底解决 RDP 白屏/黑屏问题)
/// 
/// 核心思路：终端窗口 **永远不做子窗口**。
/// 它保持独立的顶层窗口身份，仅通过定位叠放在此 WPF 控件位置上。
/// conhost.exe 的渲染完全独立于 WPF，RDP 断开重连后由 DWM 自动恢复。
/// 
/// 不再需要: SetParent, Buffer Jiggle, ForceRefresh, RedrawWindow, 
///           WindowsFormsHost, WTS Session Notifications 等一切 hack。
/// </summary>
public class ConsoleHost : Border
{
    private const int WM_WINDOWPOSCHANGED = 0x0047;
    private static readonly TimeSpan GeometryTransitionSettleDelay = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan StableBufferSyncDebounceDelay = TimeSpan.FromMilliseconds(200);
    private const int MinimumStableBufferSyncPixels = 50;

    private IntPtr _childHwnd = IntPtr.Zero;
    private Window? _parentWindow;
    private HwndSource? _hwndSource; // [v4.3] Cache the hook source so we can safely detach it during teardown
    private bool _isRdpReconnecting = false; // [v3.6] Ignore fake coordinates during RDP transition
    private bool _isGeometrySyncQueued;
    private bool _isGeometryTransitionActive;
    private bool _daemonCorrectionsEnabled;
    private string _queuedGeometryReason = "Initial";
    private ChildWindowBounds _pendingTransitionBounds;
    private bool _hasPendingTransitionBounds;
    private IntPtr _lastInsertAfter = IntPtr.Zero;
    private bool _hasLastInsertAfter;
    private readonly DispatcherTimer _geometryTransitionSettleTimer;
    private readonly DispatcherTimer _stableBufferSyncTimer;
    private DispatcherTimer? _locationRetryTimer;
    private string _pendingBufferSyncReason = "Initial";
    private int _pendingBufferSyncWidth;
    private int _pendingBufferSyncHeight;
    private bool _isStableBufferSyncRunning;
    private bool _stableBufferSyncRerunRequested;
    private bool _isTopologyTransitionHidden;
    private bool _hasDeferredTopologyBufferSync;
    private bool _isTopologyTransitionReleaseRunning;
    private int _topologyTransitionGeneration;
    private int _lastX;
    private int _lastY;
    private int _lastW;
    private int _lastH;

    private readonly struct ChildWindowBounds : IEquatable<ChildWindowBounds>
    {
        public ChildWindowBounds(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }

        public NativeMethods.RECT ToRect() => new()
        {
            Left = X,
            Top = Y,
            Right = X + Width,
            Bottom = Y + Height
        };

        public bool Equals(ChildWindowBounds other) =>
            X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

        public override bool Equals(object? obj) => obj is ChildWindowBounds other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);
    }

    public static readonly DependencyProperty AppProcessProperty =
        DependencyProperty.Register(nameof(AppProcess), typeof(Process), typeof(ConsoleHost),
            new PropertyMetadata(null, OnProcessChanged));

    public Process? AppProcess
    {
        get => (Process?)GetValue(AppProcessProperty);
        set => SetValue(AppProcessProperty, value);
    }

    private static void OnProcessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ConsoleHost host && e.NewValue is Process process)
        {
            // Check if TerminalSession already has a cached HWND (WPF DataTemplate recreated us)
            var session = host.DataContext as TerminalSession;
            if (session != null && session.CachedWindowHandle != IntPtr.Zero)
            {
                SimpleLogger.Log($"ConsoleHost: Restoring overlay from cached HWND {session.CachedWindowHandle}");
                host._childHwnd = session.CachedWindowHandle;
                host.SetupOwner();
                host.InvalidateAppliedBounds();
                host.RequestGeometrySync("ProcessChanged.RestoreCached", immediate: true);
                // v2.9: Removed explicit ShowChild(true) here. 
                // Let UpdateChildPosition decide based on IsVisible/Size.
                // host.ShowChild(true); 
            }
            else
            {
                host.SetupOverlay(process);
            }
        }
    }

    public ConsoleHost()
    {
        _geometryTransitionSettleTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = GeometryTransitionSettleDelay
        };
        _geometryTransitionSettleTimer.Tick += GeometryTransitionSettleTimer_Tick;

        _stableBufferSyncTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = StableBufferSyncDebounceDelay
        };
        _stableBufferSyncTimer.Tick += StableBufferSyncTimer_Tick;

        // v2.6: Restore default background (Black)
        this.Background = System.Windows.Media.Brushes.Black;
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
        this.SizeChanged += (s, e) => RequestGeometrySync("HostSizeChanged");
        
        // [v4.6] Critical Fix: Handle WPF DataTemplate Virtualization/Recycling
        // When uniformly shifting grid items, WPF reuses ConsoleHost but swaps its DataContext!
        // We MUST detect this swap and physically migrate the underlying HWND to match the new session context.
        this.DataContextChanged += ConsoleHost_DataContextChanged;
        
        // v2.9: Handle Tab Switching & Visibility
        // If we are in a background tab, IsVisible becomes false. We MUST hide the overlay.
        this.IsVisibleChanged += (s, e) => 
        {
            if ((bool)e.NewValue) 
            {
                 // Becoming visible: Position first, and only show if topology hold is not active.
                 RequestGeometrySync("IsVisibleChanged.Visible", immediate: true);
            }
            else 
            {
                 // Becoming invisible: Hide
                 StopStableBufferSync();
                 CancelGeometryTransition();
                 ShowChild(false);
            }
        };
    }

    private void ConsoleHost_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // [v4.6] If the data context changes, it means WPF recycled this UI component for a different Terminal!
        // We must IMMEDIATELY swap out the underlying _childHwnd, otherwise this UI block will mathematically
        // track and animate the wrong terminal process, resulting in overlapping black screens.
        
        // Safety: Hide whatever HWND we were pointing to previously so it doesn't get stuck drawn on top
        if (_childHwnd != IntPtr.Zero && e.OldValue is TerminalSession oldSession)
        {
             // Optional: ShowChild(false); but let's let the new host handle it
        }

        if (e.NewValue is TerminalSession newSession && newSession.CachedWindowHandle != IntPtr.Zero)
        {
            SimpleLogger.Log($"[RECYCLE] ConsoleHost {this.GetHashCode()} DataContext swapped. Inheriting HWND {newSession.CachedWindowHandle}");
            _childHwnd = newSession.CachedWindowHandle;
            
            // Re-bind win32 ownership and force a layout flush
            SetupOwner();
            InvalidateAppliedBounds();
            RequestGeometrySync("DataContextChanged", immediate: true);
            if (IsVisible)
            {
               ShowChild(true);
            }
        }
    }

    private void ParentWindow_LocationChanged(object? sender, EventArgs e) => RequestGeometrySync("ParentWindow.LocationChanged");
    private void ParentWindow_Activated(object? sender, EventArgs e) => BringChildToFront();
    private void ParentWindow_StateChanged(object? sender, EventArgs e)
    {
        if (_parentWindow == null)
        {
            return;
        }

        SimpleLogger.Log($"[GEOMETRY] ParentWindow.StateChanged -> {_parentWindow.WindowState}");

        if (_parentWindow.WindowState == WindowState.Minimized)
        {
            StopStableBufferSync();
            CancelGeometryTransition();
            ShowChild(false);
            return;
        }

        EnterGeometryTransition("ParentWindow.StateChanged");
        RequestGeometrySync("ParentWindow.StateChanged");
    }

    private void This_LayoutUpdated(object? sender, EventArgs e) => RequestGeometrySync("LayoutUpdated");

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ConsoleTopologyTransitionCoordinator.Register(this);

        Microsoft.Win32.SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        Microsoft.Win32.SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;

        _parentWindow = Window.GetWindow(this);
        if (_parentWindow != null)
        {
            _parentWindow.LocationChanged -= ParentWindow_LocationChanged;
            _parentWindow.LocationChanged += ParentWindow_LocationChanged;

            _parentWindow.Activated -= ParentWindow_Activated;
            _parentWindow.Activated += ParentWindow_Activated;

            _parentWindow.StateChanged -= ParentWindow_StateChanged;
            _parentWindow.StateChanged += ParentWindow_StateChanged;

            _parentWindow.Closing -= ParentWindow_Closing; // [v4.4] Intercept application close to kill the necromancer daemon
            _parentWindow.Closing += ParentWindow_Closing;

            // [v4.3] HwndSource Hooking: Native Win32 Message Pump Interception for perfect 1:1 animation sync
            _hwndSource = PresentationSource.FromVisual(_parentWindow) as HwndSource;
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(HwndMessageHook);
                _hwndSource.AddHook(HwndMessageHook);
            }
        }
        
        this.LayoutUpdated -= This_LayoutUpdated;
        this.LayoutUpdated += This_LayoutUpdated;

        // Restore if WPF DataTemplate recreated us
        if (_childHwnd == IntPtr.Zero)
        {
            var session = DataContext as TerminalSession;
            if (session != null && session.CachedWindowHandle != IntPtr.Zero)
            {
                _childHwnd = session.CachedWindowHandle;
                
                // v2.3: Robust Recovery
                // 1. Re-assert Owner
                SetupOwner();
                
                // 2. Re-assert Style (Just in case)
                int style = NativeMethods.GetWindowLong(_childHwnd, NativeMethods.GWL_STYLE);
                if ((style & NativeMethods.WS_POPUP) == 0)
                {
                    style |= NativeMethods.WS_POPUP;
                    style &= ~NativeMethods.WS_CAPTION;
                    style &= ~NativeMethods.WS_THICKFRAME;
                    NativeMethods.SetWindowLong(_childHwnd, NativeMethods.GWL_STYLE, style);
                }

                SimpleLogger.Log($"ConsoleHost: OnLoaded - restored HWND {_childHwnd}");
            }
        }

        if (_childHwnd != IntPtr.Zero)
        {
            // [v4.7] CRITICAL FIX: Always ensure Ownership on Load!
            // If DataContextChanged populated _childHwnd early when _parentWindow was null,
            // SetupOwner failed. Now that we are Loaded, re-assert Ownership so the window doesn't fall behind WPF!
            SetupOwner();

            InvalidateAppliedBounds();
            RequestGeometrySync("OnLoaded", immediate: true);
            TryScheduleBufferSyncForCurrentHostSize("OnLoaded");
            
            // v2.3: Force Jiggle/Redraw on Restore
            if (IsVisible && !_isTopologyTransitionHidden)
            {
                ForceRedraw(_childHwnd);
            }

            // [v4.2] RDP RECOVERY FIX: 
            // WPF's RDP visual tree rebuild fires Unload (which kills the Daemon) and then Load. 
            // We MUST revive the Async Daemon here, otherwise the terminal is orphaned in space!
            StartDaemon();
        }
    }

    private async void SystemEvents_SessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
    {
        SimpleLogger.Log($"[RDP_TRACE] SessionSwitch FIRED: Reason={e.Reason}");
        if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock ||
            e.Reason == Microsoft.Win32.SessionSwitchReason.RemoteConnect)
        {
            SimpleLogger.Log($"[RDP_TRACE] Session event {e.Reason} matched, locking layout for 5s.");
            await HandleRdpTransitionAsync();
        }
    }

    private async void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        SimpleLogger.Log("[RDP_TRACE] DisplaySettingsChanged FIRED, locking layout for 5s.");
        await HandleRdpTransitionAsync();
    }

    private async Task HandleRdpTransitionAsync()
    {
        if (_isRdpReconnecting) return;
        
        try
        {
            _isRdpReconnecting = true;
            SimpleLogger.Log("[RDP_TRACE] ___RDP TRANSITION LOCK ENGAGED___ (Hide-Sync-Show Strategy)");

            // [v4.1] Strategy 1: Hide immediately to prevent the "falling out of container" glitch
            if (_childHwnd != IntPtr.Zero)
            {
                ShowChild(false);
            }

            // [v4.1] Reduced lock time. 5 seconds was too long. 1.2s is enough for DWM to settle.
            await Task.Delay(1200); 

            SimpleLogger.Log("[RDP_TRACE] ___RDP TRANSITION LOCK RELEASED___ Resuming layout updates.");
        }
        finally
        {
            _isRdpReconnecting = false;
            
            // Force a hard resync of coordinates
            await Dispatcher.InvokeAsync(() => 
            {
                InvalidateAppliedBounds();
                RequestGeometrySync("RdpTransitionReleased", immediate: true);
            });
            
            // Un-hide the window now that it's in the correct position
            if (_childHwnd != IntPtr.Zero && IsVisible)
            {
                 ShowChild(true);
                 ForceRedraw(_childHwnd);
            }
        }
    }


    private void ParentWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SimpleLogger.Log($"[SHUTDOWN] ParentWindow_Closing triggered for ConsoleHost {this.GetHashCode()}");
        StopStableBufferSync();
        CancelGeometryTransition();
        
        // [v4.4] The Async Daemon is a necromancer. It must be killed immediately on window close,
        // otherwise it will keep reviving hidden dying consoles every 100ms!
        _daemonCts?.Cancel();
        
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(HwndMessageHook);
            _hwndSource = null;
        }

        ShowChild(false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SimpleLogger.Log($"[RDP_TRACE] ConsoleHost {this.GetHashCode()} Unloaded, killing daemon.");
        ConsoleTopologyTransitionCoordinator.Unregister(this);
        StopStableBufferSync();
        _daemonCts?.Cancel(); // [v3.6] Kill zombie async daemons to stop RDP layout pulling and memory leaks
        
        Microsoft.Win32.SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;

        if (_parentWindow != null)
        {
            _parentWindow.LocationChanged -= ParentWindow_LocationChanged;
            _parentWindow.Activated -= ParentWindow_Activated;
            _parentWindow.StateChanged -= ParentWindow_StateChanged;
            _parentWindow.Closing -= ParentWindow_Closing;
        }

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(HwndMessageHook);
            _hwndSource = null;
        }
        
        this.LayoutUpdated -= This_LayoutUpdated;
        CancelGeometryTransition();

        // [v4.5] BUG FIX: DO NOT call ShowChild(false) here!
        // When WPF reorders elements in a Grid (e.g. Sessions.Move), it fires Unloaded then Loaded synchronously.
        // Hiding the window here causes DWM to glitch out and permanently black-screen the console on some OS builds.
        // Logical hiding is already handled purely by IsVisibleChanged.
    }

    private IntPtr HwndMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // [v4.3] Teardown Firewall: If the WPF visual tree is collapsing, IGNORE EVERYTHING.
        // This prevents the dying window's movement from resurrecting our hidden terminals as zombies!
        if (!this.IsLoaded) return IntPtr.Zero;

        if (msg == WM_WINDOWPOSCHANGED)
        {
            // The physical window rect has changed (moving, dragging, or maximizing animation).
            // This fires synchronously with the DWM rendering frame!
            // We force our black box to catch up in the exact same render pass.
            if (_parentWindow != null && _parentWindow.WindowState == WindowState.Minimized)
            {
                 ShowChild(false);
            }
            else
            {
                 RequestGeometrySync("WM_WINDOWPOSCHANGED");
            }
        }
        return IntPtr.Zero;
    }

    // [v4.3] Removed brutally unoptimized polling hacks:
    // private void OnWindowStateChanged(object? sender, EventArgs e) ...
    // private async Task SyncPositionDuringAnimation() ...

    private void ShowChild(bool show)
    {
        if (_childHwnd == IntPtr.Zero) return;
        if (show && _isTopologyTransitionHidden)
        {
            return;
        }

        if (!show)
        {
            _daemonCorrectionsEnabled = false;
        }

        // v4.5: Changed from SW_RESTORE to SW_SHOWNOACTIVATE. SW_RESTORE steals focus and causes visual glitches
        // when rapidly applied during WPF layout shifts.
        NativeMethods.ShowWindow(_childHwnd, show ? NativeMethods.SW_SHOWNOACTIVATE : NativeMethods.SW_HIDE);
    }

    private void BringChildToFront()
    {
        if (_childHwnd == IntPtr.Zero) return;
        RestackChildWindow();
    }

    private void ForceRedraw(IntPtr hwnd)
    {
         if (hwnd == IntPtr.Zero) return;
         
         // Diagnostic: Check Visibility & Rect BEFORE Jiggle
         bool visibleBefore = NativeMethods.IsWindowVisible(hwnd);
         bool iconic = NativeMethods.IsIconic(hwnd);
         NativeMethods.RECT rectBefore;
         NativeMethods.GetWindowRect(hwnd, out rectBefore);
         SimpleLogger.Log($"ForceRedraw (Pre): HWND={hwnd} Visible={visibleBefore} Iconic={iconic} Rect=[{rectBefore.Left},{rectBefore.Top},{rectBefore.Right},{rectBefore.Bottom}]");

         // v2.5 Fix: Force Restore if Minimized (Rect -32000)
         if (iconic || rectBefore.Left <= -32000)
         {
             NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
             SimpleLogger.Log($"ForceRedraw: Window was minimized, called SW_RESTORE.");
         }

         // [v4.2 RDP FIX]: REMOVED the hardcoded NativeMethods.MoveWindow(hwnd, 0, 0, ...)
         // Reason: We are rendering a TOP-LEVEL window now (not a Child). 
         // ForceRedraw should ONLY invalidate/redraw, NEVER move coordinates to 0,0. 
         // Moving it to 0,0 throws the independent float window to the extreme top-left of the monitor!
         
         // Keep the console above its owner but below any visible owned dialogs such as Settings.
         RestackChildWindow();

         // Redraw
         // [v4.8] Removed RDW_FRAME (0x0400). Forcing non-client frame redraw on modern DWM 
         // completely destroys the DirectWrite Swapchain of a visible conhost, resulting in a blue screen!
         NativeMethods.RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, 
            NativeMethods.RDW_INVALIDATE | NativeMethods.RDW_UPDATENOW | NativeMethods.RDW_ALLCHILDREN);
            
         // Diagnostic: Check Visibility & Rect AFTER
         bool visibleAfter = NativeMethods.IsWindowVisible(hwnd);
         NativeMethods.RECT rectAfter;
         NativeMethods.GetWindowRect(hwnd, out rectAfter);
         SimpleLogger.Log($"ForceRedraw (Post): HWND={hwnd} Visible={visibleAfter} Rect=[{rectAfter.Left},{rectAfter.Top},{rectAfter.Right},{rectAfter.Bottom}]");
    }

    private void GeometryTransitionSettleTimer_Tick(object? sender, EventArgs e)
    {
        _geometryTransitionSettleTimer.Stop();
        _isGeometryTransitionActive = false;

        if (_hasPendingTransitionBounds)
        {
            ChildWindowBounds bounds = _pendingTransitionBounds;
            _hasPendingTransitionBounds = false;
            SimpleLogger.Log($"[GEOMETRY] TransitionSettled Reason={_queuedGeometryReason} Rect=[{bounds.X},{bounds.Y} {bounds.Width}x{bounds.Height}]");
            _lastX = _lastY = _lastW = _lastH = -1;
            UpdateChildPosition();
            return;
        }

        SimpleLogger.Log($"[GEOMETRY] TransitionSettled Reason={_queuedGeometryReason} without cached bounds. Recomputing.");
        RequestGeometrySync("TransitionSettledFallback", immediate: true);
    }

    private void StableBufferSyncTimer_Tick(object? sender, EventArgs e)
    {
        _stableBufferSyncTimer.Stop();
        _ = RunStableBufferSyncAsync();
    }

    private void EnterGeometryTransition(string reason)
    {
        if (!_isGeometryTransitionActive)
        {
            SimpleLogger.Log($"[GEOMETRY] TransitionStart Reason={reason}");
        }

        _isGeometryTransitionActive = true;
        _daemonCorrectionsEnabled = false;
        RestartGeometryTransitionTimer();
    }

    private void RestartGeometryTransitionTimer()
    {
        _geometryTransitionSettleTimer.Stop();
        _geometryTransitionSettleTimer.Start();
    }

    private void CancelGeometryTransition()
    {
        _geometryTransitionSettleTimer.Stop();
        _isGeometryTransitionActive = false;
        _hasPendingTransitionBounds = false;
    }

    internal void EnterTopologyTransitionHold(int generation, string reason)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => EnterTopologyTransitionHold(generation, reason)));
            return;
        }

        if (generation <= 0)
        {
            return;
        }

        if (_isTopologyTransitionHidden && generation == _topologyTransitionGeneration)
        {
            return;
        }

        _topologyTransitionGeneration = generation;
        _isTopologyTransitionHidden = true;
        _daemonCorrectionsEnabled = false;
        StopStableBufferSync();
        CancelGeometryTransition();

        if (_childHwnd != IntPtr.Zero)
        {
            ShowChild(false);
        }

        SimpleLogger.Log($"[TOPOLOGY] HoldEnter Generation={generation} Host={GetHashCode()} Reason={reason}");
    }

    internal void ClearStaleTopologyTransitionHold()
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(ClearStaleTopologyTransitionHold));
            return;
        }

        if (!_isTopologyTransitionHidden || _isTopologyTransitionReleaseRunning)
        {
            return;
        }

        _isTopologyTransitionHidden = false;
        _topologyTransitionGeneration = 0;

        if (_childHwnd != IntPtr.Zero && IsLoaded && IsVisible && _parentWindow?.WindowState != WindowState.Minimized)
        {
            RequestGeometrySync("TopologyStaleHoldCleared", immediate: true);
        }

        SimpleLogger.Log($"[TOPOLOGY] Cleared stale hold for Host={GetHashCode()}");
    }

    internal void ReleaseTopologyTransition(int generation, string reason)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => ReleaseTopologyTransition(generation, reason)));
            return;
        }

        _ = ReleaseTopologyTransitionAsync(generation, reason);
    }

    private async Task ReleaseTopologyTransitionAsync(int generation, string reason)
    {
        if (!_isTopologyTransitionHidden || generation < _topologyTransitionGeneration || _isTopologyTransitionReleaseRunning)
        {
            return;
        }

        _isTopologyTransitionReleaseRunning = true;

        try
        {
            if (generation != _topologyTransitionGeneration)
            {
                return;
            }

            SimpleLogger.Log($"[TOPOLOGY] HoldReleaseStart Generation={generation} Host={GetHashCode()} Reason={reason}");
            StopStableBufferSync();
            CancelGeometryTransition();

            if (_childHwnd != IntPtr.Zero && IsLoaded)
            {
                InvalidateAppliedBounds();
                UpdateChildPosition();
            }

            if (_hasDeferredTopologyBufferSync)
            {
                string deferredReason = string.IsNullOrWhiteSpace(_pendingBufferSyncReason)
                    ? "Deferred"
                    : _pendingBufferSyncReason;
                int targetWidth = _pendingBufferSyncWidth > 0 ? _pendingBufferSyncWidth : _lastW;
                int targetHeight = _pendingBufferSyncHeight > 0 ? _pendingBufferSyncHeight : _lastH;
                _hasDeferredTopologyBufferSync = false;
                await ApplyStableBufferSyncAsync($"TopologyRelease:{deferredReason}", targetWidth, targetHeight, allowWhileTopologyTransitionHidden: true);
            }

            if (generation != _topologyTransitionGeneration)
            {
                return;
            }

            _isTopologyTransitionHidden = false;
            _topologyTransitionGeneration = 0;

            if (_childHwnd != IntPtr.Zero && IsLoaded && IsVisible && _parentWindow?.WindowState != WindowState.Minimized)
            {
                ShowChild(true);
                RequestGeometrySync("TopologyRelease.Show", immediate: true);
            }

            SimpleLogger.Log($"[TOPOLOGY] HoldReleaseComplete Generation={generation} Host={GetHashCode()} Reason={reason}");
        }
        finally
        {
            _isTopologyTransitionReleaseRunning = false;
        }
    }

    private void TryScheduleBufferSyncForCurrentHostSize(string reason)
    {
        int width = (int)Math.Ceiling(ActualWidth);
        int height = (int)Math.Ceiling(ActualHeight);
        if (width <= 0 || height <= 0)
        {
            return;
        }

        ScheduleStableBufferSync(reason, width, height);
    }

    private void ScheduleStableBufferSync(string reason, int width, int height)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => ScheduleStableBufferSync(reason, width, height)));
            return;
        }

        _pendingBufferSyncReason = reason;
        _pendingBufferSyncWidth = width;
        _pendingBufferSyncHeight = height;

        if (_isTopologyTransitionHidden)
        {
            _hasDeferredTopologyBufferSync = true;
            SimpleLogger.Log($"[BUFFER_SYNC] Deferred by topology hold. Reason={reason} Target={width}x{height} Host={GetHashCode()}");
            return;
        }

        if (_isStableBufferSyncRunning)
        {
            _stableBufferSyncRerunRequested = true;
        }

        _stableBufferSyncTimer.Stop();
        _stableBufferSyncTimer.Start();
    }

    private void StopStableBufferSync()
    {
        _stableBufferSyncTimer.Stop();
        _stableBufferSyncRerunRequested = false;
    }

    private bool TryGetStableBufferSyncContext(int targetWidth, int targetHeight, bool allowWhileTopologyTransitionHidden, out TerminalSession session, out IntPtr hwnd, out string skipReason)
    {
        session = null!;
        hwnd = _childHwnd;

        if (hwnd == IntPtr.Zero)
        {
            skipReason = "NoChildHwnd";
            return false;
        }

        if (!IsLoaded)
        {
            skipReason = "HostNotLoaded";
            return false;
        }

        if (!IsVisible)
        {
            skipReason = "HostNotVisible";
            return false;
        }

        if (_parentWindow == null)
        {
            skipReason = "ParentWindowMissing";
            return false;
        }

        if (_parentWindow.WindowState == WindowState.Minimized)
        {
            skipReason = "ParentWindowMinimized";
            return false;
        }

        if (_isRdpReconnecting)
        {
            skipReason = "RdpTransitionActive";
            return false;
        }

        if (_isGeometryTransitionActive)
        {
            skipReason = "GeometryTransitionActive";
            return false;
        }

        if (_isTopologyTransitionHidden && !allowWhileTopologyTransitionHidden)
        {
            skipReason = "TopologyTransitionHidden";
            return false;
        }

        if (targetWidth < MinimumStableBufferSyncPixels || targetHeight < MinimumStableBufferSyncPixels)
        {
            skipReason = $"HostTooSmall({targetWidth}x{targetHeight})";
            return false;
        }

        if (DataContext is not TerminalSession localSession)
        {
            skipReason = "TerminalSessionMissing";
            return false;
        }

        session = localSession;
        skipReason = string.Empty;
        return true;
    }

    private async Task RunStableBufferSyncAsync()
    {
        if (_isStableBufferSyncRunning)
        {
            _stableBufferSyncRerunRequested = true;
            return;
        }

        string reason = _pendingBufferSyncReason;
        int targetWidth = _pendingBufferSyncWidth > 0 ? _pendingBufferSyncWidth : _lastW;
        int targetHeight = _pendingBufferSyncHeight > 0 ? _pendingBufferSyncHeight : _lastH;

        await ApplyStableBufferSyncAsync(reason, targetWidth, targetHeight, allowWhileTopologyTransitionHidden: false);

        if (_stableBufferSyncRerunRequested)
        {
            _stableBufferSyncRerunRequested = false;
            ScheduleStableBufferSync("StableBufferSyncRerun", _pendingBufferSyncWidth, _pendingBufferSyncHeight);
        }
    }

    private async Task ApplyStableBufferSyncAsync(string reason, int targetWidth, int targetHeight, bool allowWhileTopologyTransitionHidden)
    {
        if (!TryGetStableBufferSyncContext(targetWidth, targetHeight, allowWhileTopologyTransitionHidden, out TerminalSession session, out IntPtr hwnd, out string skipReason))
        {
            SimpleLogger.Log($"[BUFFER_SYNC] Skipped. Reason={skipReason} RequestedBy={reason} Target={targetWidth}x{targetHeight}");
            return;
        }

        _isStableBufferSyncRunning = true;

        try
        {
            NativeMethods.ShowScrollBar(hwnd, NativeMethods.SB_HORZ, false);
            await Task.Run(session.ResizeConsoleBuffer);

            if (_childHwnd == hwnd)
            {
                NativeMethods.ShowScrollBar(hwnd, NativeMethods.SB_HORZ, false);
            }

            SimpleLogger.Log($"[BUFFER_SYNC] Applied. Reason={reason} Target={targetWidth}x{targetHeight} HWND={hwnd}");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, $"[BUFFER_SYNC] Failed Reason={reason} Target={targetWidth}x{targetHeight}");
        }
        finally
        {
            _isStableBufferSyncRunning = false;
        }
    }

    private bool TryGetHostBounds(out ChildWindowBounds hostBounds)
    {
        hostBounds = default;

        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget == null)
        {
            SimpleLogger.Log("[RDP_TRACE] TryGetHostBounds skipped - Source/CompositionTarget is null (Tree not connected?)");
            StartLocationRetryTimer();
            return false;
        }

        _locationRetryTimer?.Stop();

        var screenPoint = PointToScreen(new System.Windows.Point(0, 0));
        int x = (int)screenPoint.X;
        int y = (int)screenPoint.Y;

        var transform = source.CompositionTarget.TransformToDevice;
        int w = (int)(ActualWidth * transform.M11);
        int h = (int)(ActualHeight * transform.M22);
        if (w <= 1 || h <= 1)
        {
            SimpleLogger.Log($"[ZERO-FLICKER] TryGetHostBounds skipped because ActualWidth/Height = {ActualWidth}x{ActualHeight} -> {w}x{h}");
            return false;
        }

        hostBounds = new ChildWindowBounds(x, y, w, h);
        return true;
    }

    private void InvalidateAppliedBounds()
    {
        _hasLastInsertAfter = false;
        _lastX = _lastY = _lastW = _lastH = -1;
    }

    private void RequestGeometrySync(string reason, bool immediate = false)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => RequestGeometrySync(reason, immediate)));
            return;
        }

        if (_childHwnd == IntPtr.Zero)
        {
            return;
        }

        _queuedGeometryReason = reason;

        if (immediate && !_isGeometryTransitionActive)
        {
            UpdateChildPosition();
            return;
        }

        if (_isGeometrySyncQueued)
        {
            return;
        }

        _isGeometrySyncQueued = true;
        _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(ProcessQueuedGeometrySync));
    }

    private void ProcessQueuedGeometrySync()
    {
        _isGeometrySyncQueued = false;
        UpdateChildPosition();
    }

    /// <summary>
    /// Track WPF control's screen position and sync the console window to overlay it.
    /// v2.3: Reverted to MoveWindow (proven to work in v2.0)
    /// </summary>
    private void UpdateChildPosition()
    {
        if (_childHwnd == IntPtr.Zero) return;
        
        if (!IsLoaded)
        {
            SimpleLogger.Log("[RDP_TRACE] UpdateChildPosition skipped - !IsLoaded");
            return;
        }

        if (_isRdpReconnecting)
        {
            // IMPORTANT: DWM is chaotic right now. Ignore all coordinates WPF gives us.
            return;
        }

        // v2.9: If not visible (e.g. background tab), HIDE IT.
        if (!IsVisible)
        {
            ShowChild(false);
            return;
        }

        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            SimpleLogger.Log($"[RDP_TRACE] UpdateChildPosition HIDING - Size={ActualWidth}x{ActualHeight}. Visible={IsVisible}");
            ShowChild(false); // v2.9: Ensure hidden if size invalid
            StartLocationRetryTimer();
            // [v3.91 Zero-Flicker Protection]
            // If WPF hasn't assigned us a size yet, DO NOT move the console window.
            // Otherwise it gets thrown from X=32000 to X=0,Y=0 (top left tail) and stays there until WPF layout updates.
            if (ActualWidth <= 1 || ActualHeight <= 1)
            {
                 SimpleLogger.Log($"[ZERO-FLICKER] UpdateChildPosition SKIPPED because ActualWidth/Height = {ActualWidth}x{ActualHeight}");
                 return;
            }
        }

        try
        {
            if (!TryGetHostBounds(out ChildWindowBounds hostBounds))
            {
                return;
            }

            int x = hostBounds.X;
            int y = hostBounds.Y;
            int w = hostBounds.Width;
            int h = hostBounds.Height;

            int prevX = _lastX;
            int prevY = _lastY;
            int prevW = _lastW;
            int prevH = _lastH;

            // Skip if nothing changed (LayoutUpdated fires very frequently)
            if (x == prevX && y == prevY && w == prevW && h == prevH)
            {
                if (_isGeometryTransitionActive)
                {
                    RestartGeometryTransitionTimer();
                    return;
                }

                IntPtr currentInsertAfter = GetDesiredInsertAfter();
                if (_hasLastInsertAfter && currentInsertAfter == _lastInsertAfter)
                {
                    return;
                }

                SyncChildTopmostBand();
                NativeMethods.SetWindowPos(
                    _childHwnd,
                    currentInsertAfter,
                    0,
                    0,
                    0,
                    0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
                _lastInsertAfter = currentInsertAfter;
                _hasLastInsertAfter = true;
                _daemonCorrectionsEnabled = !_isTopologyTransitionHidden;
                return;
            }

            bool sizeChanged = (w != prevW || h != prevH);
            ChildWindowBounds bounds = new(x, y, w, h);

            if (_isGeometryTransitionActive)
            {
                _pendingTransitionBounds = bounds;
                _hasPendingTransitionBounds = true;
                _daemonCorrectionsEnabled = false;
                RestartGeometryTransitionTimer();
                return;
            }

            _lastX = x;
            _lastY = y;
            _lastW = w;
            _lastH = h;

            // v3.2: Update Cache for Async Daemon (Thread-Safe)
            // [v3.92] ZERO-FLICKER: DO NOT cache invalid targets! If w/h are zero, it tricks the daemon 
            // into pulling the off-screen window back to [0,0]!
            if (w > 1 && h > 1)
            {
                lock (_rectLock)
                {
                    _cachedTargetRect.Left = x;
                    _cachedTargetRect.Top = y;
                    _cachedTargetRect.Right = x + w;
                    _cachedTargetRect.Bottom = y + h;
                }
                SimpleLogger.Log($"[RDP_TRACE] Setup TargetRect Cache -> [{x},{y} {w}x{h}]");
            }
            else
            {
                SimpleLogger.Log($"[ZERO-FLICKER] Setup TargetRect Cache SKIPPED (Invalid Size): [{x},{y} {w}x{h}]");
            }

            IntPtr insertAfter = GetDesiredInsertAfter();

            // During topology transitions, keep the real console hidden while committing the final geometry.
            SyncChildTopmostBand();
            if (!_isTopologyTransitionHidden)
            {
                ShowChild(true);
            }

            if (sizeChanged)
            {
                NativeMethods.SetWindowPos(
                    _childHwnd,
                    insertAfter,
                    x,
                    y,
                    w,
                    h,
                    NativeMethods.SWP_NOACTIVATE);
            }
            else
            {
                NativeMethods.SetWindowPos(
                    _childHwnd,
                    insertAfter,
                    x,
                    y,
                    0,
                    0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }

            _lastInsertAfter = insertAfter;
            _hasLastInsertAfter = true;
            _daemonCorrectionsEnabled = !_isTopologyTransitionHidden;

            // Sync console buffer width when size changes (eliminates horizontal scrollbar)
            if (sizeChanged)
            {
                NativeMethods.ShowScrollBar(_childHwnd, NativeMethods.SB_HORZ, false);
                ScheduleStableBufferSync($"ResizeApply:{_queuedGeometryReason}", w, h);

                SimpleLogger.Log($"[GEOMETRY] ResizeApply Reason={_queuedGeometryReason} Rect=[{x},{y} {w}x{h}]");
            }
            else
            {
                SimpleLogger.Log($"[GEOMETRY] MoveOnlyApply Reason={_queuedGeometryReason} Rect=[{x},{y} {w}x{h}]");
            }
        }
        catch (Exception ex)
        {
             SimpleLogger.LogError(ex, "[RDP_TRACE] UpdateChildPosition Error");
        }
    }

    private void StartLocationRetryTimer()
    {
        if (_locationRetryTimer == null)
        {
            _locationRetryTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher);
            _locationRetryTimer.Interval = TimeSpan.FromMilliseconds(200);
            _locationRetryTimer.Tick += (s, e) => RequestGeometrySync("LocationRetryTimer");
        }
        if (!_locationRetryTimer.IsEnabled)
        {
            _locationRetryTimer.Start();
            SimpleLogger.Log("ConsoleHost: Started location retry timer");
        }
    }

    /// <summary>
    /// Set the console window as an "owned" window of our main WPF window.
    /// Owned windows: minimize with owner, stay on top of owner, but render independently.
    /// </summary>
    private void SetupOwner()
    {
        if (_childHwnd == IntPtr.Zero || _parentWindow == null) return;

        var mainHwnd = new WindowInteropHelper(_parentWindow).Handle;
        if (mainHwnd == IntPtr.Zero) return;

        // [v4.8] Optimization & Fix: Only update if the owner actually changed.
        // GetWindow(GW_OWNER) is the reliable Win32 way to retrieve the true owner.
        IntPtr currentOwner = NativeMethods.GetWindow(_childHwnd, NativeMethods.GW_OWNER);
        if (currentOwner == mainHwnd)
        {
            SyncChildTopmostBand();
            DeleteTaskbarTab(_childHwnd);
            return;
        }

        NativeMethods.SetWindowLongPtr(_childHwnd, NativeMethods.GWLP_HWNDPARENT, mainHwnd);
        SimpleLogger.Log($"ConsoleHost: Set owner {mainHwnd} for HWND {_childHwnd}");

        SyncChildTopmostBand();

        // [v4.8] Taskbar Reappearance Fix:
        // Changing the owner dynamically forces Windows Shell to re-evaluate the window's taskbar visibility.
        // We must explicitly suppress it from the taskbar again right after taking ownership.
        // Runtime owner rebinds happen while conhost may already be visible, so COM DeleteTab is the only safe path.
        DeleteTaskbarTab(_childHwnd);
    }

    private IntPtr GetDesiredInsertAfter()
    {
        IntPtr blockerHwnd = GetVisibleOwnedDialogHandle();
        if (blockerHwnd != IntPtr.Zero)
        {
            return blockerHwnd;
        }

        if (_parentWindow?.Topmost == true)
        {
            return NativeMethods.HWND_TOPMOST;
        }

        return NativeMethods.HWND_TOP;
    }

    private void SyncChildTopmostBand()
    {
        if (_childHwnd == IntPtr.Zero || _parentWindow == null) return;

        int exStyle = NativeMethods.GetWindowLong(_childHwnd, NativeMethods.GWL_EXSTYLE);
        bool childIsTopmost = (exStyle & NativeMethods.WS_EX_TOPMOST) != 0;
        bool parentIsTopmost = GetDesiredTopmostBand();

        if (childIsTopmost == parentIsTopmost) return;

        NativeMethods.SetWindowPos(
            _childHwnd,
            parentIsTopmost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private void RestackChildWindow()
    {
        if (_childHwnd == IntPtr.Zero)
        {
            return;
        }

        SyncChildTopmostBand();
        NativeMethods.SetWindowPos(
            _childHwnd,
            GetDesiredInsertAfter(),
            0,
            0,
            0,
            0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    private bool GetDesiredTopmostBand()
    {
        IntPtr blockerHwnd = GetVisibleOwnedDialogHandle();
        if (blockerHwnd != IntPtr.Zero)
        {
            int blockerExStyle = NativeMethods.GetWindowLong(blockerHwnd, NativeMethods.GWL_EXSTYLE);
            return (blockerExStyle & NativeMethods.WS_EX_TOPMOST) != 0;
        }

        return _parentWindow?.Topmost == true;
    }

    private IntPtr GetVisibleOwnedDialogHandle()
    {
        if (_parentWindow == null || System.Windows.Application.Current == null)
        {
            return IntPtr.Zero;
        }

        Window? activeOwnedWindow = System.Windows.Application.Current.Windows
            .OfType<Window>()
            .Where(window =>
                window != _parentWindow
                && window.Owner == _parentWindow
                && window.IsVisible
                && window.WindowState != WindowState.Minimized)
            .OrderByDescending(window => window.IsActive)
            .ThenByDescending(window => window.Topmost)
            .FirstOrDefault();

        if (activeOwnedWindow == null)
        {
            return IntPtr.Zero;
        }

        return new WindowInteropHelper(activeOwnedWindow).Handle;
    }

    private void ApplyInitialTaskbarStealth(IntPtr childHwnd)
    {
        if (childHwnd == IntPtr.Zero) return;

        // Enforce ToolWindow style to guarantee taskbar evasion unconditionally (Server 2012/Win8 necessity)
        int exStyle = NativeMethods.GetWindowLong(childHwnd, NativeMethods.GWL_EXSTYLE);
        exStyle &= ~NativeMethods.WS_EX_APPWINDOW;
        exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLong(childHwnd, NativeMethods.GWL_EXSTYLE, exStyle);

        // FLUSH FRAME CACHE (CRITICAL FOR SERVER 2012/WIN8):
        // Note: Doing this at startup is safe because the window is not yet fully visible/drawn.
        NativeMethods.SetWindowPos(childHwnd, IntPtr.Zero, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER |
            NativeMethods.SWP_FRAMECHANGED | NativeMethods.SWP_NOACTIVATE);

        DeleteTaskbarTab(childHwnd);
    }

    private void DeleteTaskbarTab(IntPtr childHwnd)
    {
        if (childHwnd == IntPtr.Zero) return;

        // Double Guarantee: Hide from taskbar using COM ITaskbarList (works cleanly on Win10/11)
        try
        {
            var taskbarList = new NativeMethods.TaskbarList();
            var taskbarListInterface = (NativeMethods.ITaskbarList)taskbarList;
            taskbarListInterface.HrInit();
            taskbarListInterface.DeleteTab(childHwnd);
            Marshal.ReleaseComObject(taskbarListInterface);
            Marshal.ReleaseComObject(taskbarList);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "Failed to use ITaskbarList COM object, relying on EX_TOOLWINDOW fallback.");
        }
    }

    /// <summary>
    /// Initial setup: wait for console HWND, strip borders, set owner, position as overlay.
    /// NO SetParent call anywhere - the console stays independent!
    /// </summary>
    private async void SetupOverlay(Process process)
    {
        // [v4.7] Safety: if another subsystem (like DataContext) already populated the HWND, abort duplicate setup.
        if (_childHwnd != IntPtr.Zero) return;

        IntPtr childHwnd = IntPtr.Zero;

        await Task.Run(() =>
        {
            int retry = 0;
            while (retry < 50)
            {
                try
                {
                    process.Refresh();
                    if (process.HasExited) return;
                    childHwnd = process.MainWindowHandle;
                    if (childHwnd != IntPtr.Zero) break;
                }
                catch { }
                Thread.Sleep(100);
                retry++;
            }
        });

        // [v4.7] Re-check after async delay. DataContext might have propagated while we were polling!
        if (_childHwnd != IntPtr.Zero) return;

        if (childHwnd == IntPtr.Zero)
        {
            SimpleLogger.Log($"ConsoleHost: SetupOverlay failed - no HWND for PID {process.Id}");
            return;
        }

        SimpleLogger.Log($"ConsoleHost: Setting up overlay for HWND {childHwnd}");

        // 1. Remove borders and set POPUP style (Corrects visibility issue from v2.1)
        int style = NativeMethods.GetWindowLong(childHwnd, NativeMethods.GWL_STYLE);
        style &= ~NativeMethods.WS_CAPTION;
        style &= ~NativeMethods.WS_THICKFRAME;
        style &= ~NativeMethods.WS_CHILD;
        style |= NativeMethods.WS_POPUP; // Restore this! Borderless top-level needs POPUP.
        NativeMethods.SetWindowLong(childHwnd, NativeMethods.GWL_STYLE, style);

        // 2 & 3 & 4. Enforce Taskbar Stealth (ToolWindow + DeleteTab)
        ApplyInitialTaskbarStealth(childHwnd);
        
        // 4. Store HWND before owner binding so recovery paths can see the real console handle.
        _childHwnd = childHwnd;
        var session = DataContext as TerminalSession;
        if (session != null)
            session.CachedWindowHandle = childHwnd;

        // 5. Set owner while the console is still in its hidden bootstrap phase.
        SetupOwner();

        // 6. Only show after initial stealth + ownership are in place.
        NativeMethods.ShowWindow(childHwnd, _isTopologyTransitionHidden || !IsVisible ? NativeMethods.SW_HIDE : NativeMethods.SW_SHOWNOACTIVATE);

        // 7. Update Position (Will Show if Visible, Hide if not)
        InvalidateAppliedBounds();
        RequestGeometrySync("SetupOverlay", immediate: true);

        // 8. Force Initial Jiggle & Redraw & Diagnostic Check
        if (IsVisible && !_isTopologyTransitionHidden)
        {
            ForceRedraw(_childHwnd);
        }

        // v2.7: Startup Layout Fixup Loop (Keep this for immediate fix)
        _ = StartupLayoutFixup();
        
        // v3.0: Startup Layout Daemon (Long-term guard for 15s)
        StartDaemon();

        // 7. Hide horizontal scrollbar
        NativeMethods.ShowScrollBar(childHwnd, NativeMethods.SB_HORZ, false);
        // ... (rest of method)


        NativeMethods.ShowScrollBar(childHwnd, NativeMethods.SB_HORZ, false);

        SimpleLogger.Log($"ConsoleHost: Overlay ready. HWND={childHwnd} (independent top-level window)");
    }

    private async Task StartupLayoutFixup()
    {
        // Phase 1: Wait for IsLoaded (Max 5s)
        // If SetupOverlay is called before OnLoaded, we need to wait.
        int waitCount = 0;
        while (!IsLoaded && waitCount < 50)
        {
            await Task.Delay(100);
            waitCount++;
        }

        if (!IsLoaded)
        {
            SimpleLogger.Log("[RDP_TRACE] StartupLayoutFixup timed out waiting for IsLoaded.");
            return;
        }

        SimpleLogger.Log("[RDP_TRACE] StartupLayoutFixup Phase 2: Starting 3-second sync loop.");
        // Phase 2: Force Sync (3s) - Slower for Server 2012 reliability
        // Ensure layout settles even if initial frames are wrong
        for (int i = 0; i < 30; i++) 
        {
            await Task.Delay(100); // 100ms interval for reliable layout update
            RequestGeometrySync("StartupLayoutFixup");
        }
        SimpleLogger.Log("[RDP_TRACE] StartupLayoutFixup Phase 2: Completed 3-second sync loop.");
    }

    // v3.2: Non-blocking Async Daemon
    private NativeMethods.RECT _cachedTargetRect = new NativeMethods.RECT();
    private readonly object _rectLock = new();
    private System.Threading.CancellationTokenSource? _daemonCts;

    private void StartDaemon()
    {
        _daemonCts?.Cancel();
        _daemonCts = new System.Threading.CancellationTokenSource();
        var token = _daemonCts.Token;

        Task.Run(async () => 
        {
            SimpleLogger.Log($"ConsoleHost {this.GetHashCode()}: Async Daemon Started (Non-blocking)");
            while (!token.IsCancellationRequested)
            {
                try 
                {
                    await Task.Delay(100, token); // [v4.1] Extremely fast 100ms interval for near-instant RDP drift recovery
                    EnsurePositionAsync();
                }
                catch (TaskCanceledException) { break; }
                catch (Exception ex)
                {
                    SimpleLogger.LogError(ex, "Daemon Loop Error");
                }
            }
            SimpleLogger.Log($"ConsoleHost {this.GetHashCode()}: Async Daemon Stopped");
        }, token);
    }



    private void EnsurePositionAsync()
    {
        if (_isRdpReconnecting || !_daemonCorrectionsEnabled || _isGeometryTransitionActive)
        {
            return; // Do not try to correct position during transition or while corrections are suppressed.
        }

        // 1. Check if we have a valid target (Thread-safe read)
        NativeMethods.RECT target;
        lock (_rectLock) { target = _cachedTargetRect; }

        if (target.Right == 0 && target.Bottom == 0) return; // Not initialized yet

        // 2. Get Actual Rect (Background thread safe)
        IntPtr hwnd = _childHwnd; // Atomic read of IntPtr
        if (hwnd == IntPtr.Zero) return;

        NativeMethods.RECT actual;
        if (!NativeMethods.GetWindowRect(hwnd, out actual)) return;

        // 3. Compare (Tolerance 5px)
        int targetW = target.Right - target.Left;
        int targetH = target.Bottom - target.Top;
        int actualW = actual.Right - actual.Left;
        int actualH = actual.Bottom - actual.Top;

        bool xDrift = Math.Abs(actual.Left - target.Left) > 5;
        bool yDrift = Math.Abs(actual.Top - target.Top) > 5;
        bool wDrift = Math.Abs(actualW - targetW) > 5;
        bool hDrift = Math.Abs(actualH - targetH) > 5;

        // Diagnostic log every second just to see if it's trying:
        // SimpleLogger.Log($"[Daemon] Heartbeat... Target=[{target.Left},{target.Top} {targetW}x{targetH}]");

        if (xDrift || yDrift || wDrift || hDrift)
        {
            SimpleLogger.Log($"[RDP_TRACE][AsyncDaemon] Drift! Actual=[{actual.Left},{actual.Top} {actualW}x{actualH}] Target=[{target.Left},{target.Top} {targetW}x{targetH}]");

            if (wDrift || hDrift)
            {
                NativeMethods.SetWindowPos(
                    hwnd,
                    IntPtr.Zero,
                    target.Left,
                    target.Top,
                    targetW,
                    targetH,
                    NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER);
                NativeMethods.ShowScrollBar(hwnd, NativeMethods.SB_HORZ, false);
                ScheduleStableBufferSync("AsyncDaemonResize", targetW, targetH);
            }
            else
            {
                NativeMethods.SetWindowPos(
                    hwnd,
                    IntPtr.Zero,
                    target.Left,
                    target.Top,
                    0,
                    0,
                    NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOZORDER);
            }
        }
    }


}
