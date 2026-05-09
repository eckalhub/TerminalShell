using System.Windows;
using System.Threading.Tasks;
using System.Threading;
using TerminalShell.Core;
using System.Linq;
using TerminalShell.Services;
using System.Windows.Threading;

namespace TerminalShell;

public partial class App : System.Windows.Application
{
    private enum StartupWindowLaunchMode
    {
        Normal,
        StartupMinimizedToTaskbar,
        StartupHiddenToTray
    }

    private ITrayIconService? _trayService; // [NEW] Interface
    private ISingleInstanceService? _singleInstanceService; // [NEW]
    private int _fatalShutdownStarted;
    private int _explicitExitRequested;
    private int _systemSessionEndingStarted;
    private string? _pendingRestartProcessPath;

    internal bool IsFatalShutdownInProgress => Interlocked.CompareExchange(ref _fatalShutdownStarted, 0, 0) != 0;
    internal bool IsExplicitExitRequested => Interlocked.CompareExchange(ref _explicitExitRequested, 0, 0) != 0;
    internal bool IsSystemSessionEnding => Interlocked.CompareExchange(ref _systemSessionEndingStarted, 0, 0) != 0;

    // We keep a reference to MainWindow to avoid GC issues if needed
    
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SetupExceptionHandling();
        SimpleLogger.Log($"=== Application Started === Version={RuntimeAppIdentity.VersionText} ProcessPath={Environment.ProcessPath ?? "<null>"}");
        ApplyRuntimeWindowIcon();

        // --- 0. Single Instance Check (Limit 1 Instance) ---
        _singleInstanceService = new SingleInstanceService("TerminalShell");
        if (!_singleInstanceService.EnsureSingleInstance())
        {
            // Activate existing window
            // Note: We don't know the exact title if it has version number, but we search process
            _singleInstanceService.FocusExistingWindow("TerminalShell"); 
            Shutdown();
            return;
        }

        // --- 1. Initialize Config Manager (Async) ---
        await TerminalShell.Core.Config.ConfigManager.Instance.InitializeAsync();

        bool isStartupLaunch = StartupManager.IsStartupLaunch(e.Args);
        var config = TerminalShell.Core.Config.ConfigManager.Instance.Config;
        StartupWindowLaunchMode launchMode = ResolveStartupWindowLaunchMode(
            isStartupLaunch,
            config.StartMinimizedWhenLaunchedAtStartup,
            config.MinimizeMainWindowToTray);
        string launchArgsText = e.Args.Length == 0 ? "<none>" : string.Join(' ', e.Args);
        SimpleLogger.Log($"[STARTUP] Args={launchArgsText} IsStartupLaunch={isStartupLaunch} StartMinimizedWhenLaunchedAtStartup={config.StartMinimizedWhenLaunchedAtStartup} MinimizeMainWindowToTray={config.MinimizeMainWindowToTray} LaunchMode={launchMode}");

        // --- 2. Initialize Tray Service ---
        _trayService = new TrayIconService();
        _trayService.OpenSettingsRequested += OpenMainWindow;
        _trayService.ToggleRequested += OpenMainWindow;
        _trayService.OpenSettingsWindowRequested += OpenSettingsWindow;
        _trayService.RestartRequested += RestartApplication;
        _trayService.ExitRequested += RequestApplicationExit;
        
        _trayService.Initialize();

        // --- 3. Manually Create MainWindow ---
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        CreateMainWindow(launchMode);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SimpleLogger.Log("[SHUTDOWN] App.OnExit STARTED.");
        string? pendingRestartProcessPath = _pendingRestartProcessPath;
        try
        {
            // Also dispose tray service
            if (_trayService is IDisposable disposableTray)
            {
                disposableTray.Dispose();
            }
            // Dispose SingleInstanceService
            _singleInstanceService?.Dispose();
            TerminalProcessJob.Instance.Dispose();
        }
        catch (Exception ex)
        {
            // Log but don't crash, we are exiting anyway
            SimpleLogger.LogError(ex, "App.OnExit Cleanup");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(pendingRestartProcessPath))
            {
                try
                {
                    System.Diagnostics.Process.Start(pendingRestartProcessPath);
                    SimpleLogger.Log($"[RESTART] Restarted application from OnExit. Path={pendingRestartProcessPath}");
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError(ex, "RestartApplication OnExit Process.Start failed");
                }
            }

            SimpleLogger.Log("[SHUTDOWN] Calling base.OnExit(e)...");
            base.OnExit(e);
            SimpleLogger.Log("[SHUTDOWN] App.OnExit FINISHED.");
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        Interlocked.Exchange(ref _systemSessionEndingStarted, 1);
        Interlocked.Exchange(ref _explicitExitRequested, 1);
        _pendingRestartProcessPath = null;
        SimpleLogger.Log($"[SHUTDOWN] Session ending detected. Reason={e.ReasonSessionEnding}");
        base.OnSessionEnding(e);
    }

    private static StartupWindowLaunchMode ResolveStartupWindowLaunchMode(
        bool isStartupLaunch,
        bool startMinimizedWhenLaunchedAtStartup,
        bool minimizeMainWindowToTray)
    {
        if (!isStartupLaunch || !startMinimizedWhenLaunchedAtStartup)
        {
            return StartupWindowLaunchMode.Normal;
        }

        return minimizeMainWindowToTray
            ? StartupWindowLaunchMode.StartupHiddenToTray
            : StartupWindowLaunchMode.StartupMinimizedToTaskbar;
    }

    private void CreateMainWindow(StartupWindowLaunchMode launchMode = StartupWindowLaunchMode.Normal)
    {
        SimpleLogger.Log($"[APP] Creating main window. LaunchMode={launchMode}");
        var mainWindow = new TerminalShell.Views.MainWindow();
        RuntimeAppIdentity.ApplyWindowIcon(mainWindow);

        if (launchMode != StartupWindowLaunchMode.Normal)
        {
            mainWindow.ShowActivated = false;
        }

        switch (launchMode)
        {
            case StartupWindowLaunchMode.StartupMinimizedToTaskbar:
                ScheduleMainWindowStartupMinimize(mainWindow);
                break;
            case StartupWindowLaunchMode.StartupHiddenToTray:
                PrepareMainWindowForStartupTrayMode(mainWindow);
                break;
        }

        mainWindow.Show();
        SimpleLogger.Log("[APP] Main window shown.");
    }

    private static void PrepareMainWindowForStartupTrayMode(TerminalShell.Views.MainWindow mainWindow)
    {
        mainWindow.ShowInTaskbar = false;
        mainWindow.WindowState = WindowState.Minimized;
        ScheduleMainWindowStartupHideToTray(mainWindow);
        SimpleLogger.Log("[STARTUP] Prepared main window for startup tray mode.");
    }

    private static void ScheduleMainWindowStartupMinimize(Window mainWindow)
    {
        void MinimizeWindow()
        {
            if (mainWindow.WindowState == WindowState.Minimized)
            {
                return;
            }

            mainWindow.WindowState = WindowState.Minimized;
            SimpleLogger.Log($"[STARTUP] Main window auto-minimized. ShowInTaskbar={mainWindow.ShowInTaskbar} Visibility={mainWindow.Visibility}");
        }

        if (mainWindow.IsLoaded)
        {
            mainWindow.Dispatcher.BeginInvoke((Action)MinimizeWindow, DispatcherPriority.ApplicationIdle);
            return;
        }

        RoutedEventHandler? loadedHandler = null;
        loadedHandler = (_, _) =>
        {
            mainWindow.Loaded -= loadedHandler;
            mainWindow.Dispatcher.BeginInvoke((Action)MinimizeWindow, DispatcherPriority.ApplicationIdle);
        };

        mainWindow.Loaded += loadedHandler;
    }

    private static void ScheduleMainWindowStartupHideToTray(TerminalShell.Views.MainWindow mainWindow)
    {
        void HideToTray()
        {
            mainWindow.HideToTrayForStartupLaunch();
            SimpleLogger.Log($"[STARTUP] Main window hidden to tray after startup launch. ShowInTaskbar={mainWindow.ShowInTaskbar} Visibility={mainWindow.Visibility} State={mainWindow.WindowState}");
        }

        if (mainWindow.IsLoaded)
        {
            HideToTray();
            return;
        }

        RoutedEventHandler? loadedHandler = null;
        loadedHandler = (_, _) =>
        {
            mainWindow.Loaded -= loadedHandler;
            HideToTray();
        };

        mainWindow.Loaded += loadedHandler;
    }

    private void OpenMainWindow()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => 
        {
            SimpleLogger.Log("[TRAY] OpenMainWindow invoked.");
            var win = System.Windows.Application.Current.MainWindow;
            if (win == null)
            {
                SimpleLogger.Log("[TRAY] MainWindow missing, recreating.");
                CreateMainWindow();
            }
            else
            {
                win.ShowInTaskbar = true;
                if (win.Visibility == Visibility.Visible)
                {
                    if (win.WindowState == WindowState.Minimized)
                    {
                        win.WindowState = WindowState.Normal;
                    }
                    win.Activate();
                    SimpleLogger.Log($"[TRAY] MainWindow activated. State={win.WindowState} Visibility={win.Visibility}");
                }
                else
                {
                    win.Show();
                    win.WindowState = WindowState.Normal;
                    win.Activate();
                    SimpleLogger.Log("[TRAY] MainWindow restored from hidden state.");
                }
            }
        });
    }

    private void ApplyRuntimeWindowIcon()
    {
        if (RuntimeAppIdentity.WindowIcon == null)
        {
            return;
        }

        Style windowStyle = Resources.Contains(typeof(Window)) && Resources[typeof(Window)] is Style existingStyle
            ? existingStyle
            : new Style(typeof(Window));

        Setter? iconSetter = windowStyle.Setters
            .OfType<Setter>()
            .FirstOrDefault(setter => setter.Property == Window.IconProperty);

        if (iconSetter == null)
        {
            windowStyle.Setters.Add(new Setter(Window.IconProperty, RuntimeAppIdentity.WindowIcon));
        }
        else
        {
            iconSetter.Value = RuntimeAppIdentity.WindowIcon;
        }

        Resources[typeof(Window)] = windowStyle;
    }

    private void OpenSettingsWindow()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var win = System.Windows.Application.Current.MainWindow as TerminalShell.Views.MainWindow;
            if (win?.DataContext is TerminalShell.ViewModels.MainViewModel vm)
            {
                vm.OpenSettingsCommand.Execute(null);
            }
        });
    }

    private void RestartApplication()
    {
        string? processPath = Environment.ProcessPath;
        SimpleLogger.Log($"[RESTART] User requested app restart from system tray. Path={processPath ?? "<null>"}");
        RequestApplicationExitCore("TrayRestart", processPath);
    }

    private void RequestApplicationExit()
    {
        RequestApplicationExitCore("TrayExit", null);
    }

    internal void CancelExplicitExitRequest()
    {
        bool hadExplicitExitRequest = Interlocked.Exchange(ref _explicitExitRequested, 0) != 0;
        bool hadPendingRestart = !string.IsNullOrWhiteSpace(_pendingRestartProcessPath);
        _pendingRestartProcessPath = null;

        if (hadExplicitExitRequest || hadPendingRestart)
        {
            SimpleLogger.Log("[SHUTDOWN] Explicit exit request canceled.");
        }
    }

    private void RequestApplicationExitCore(string source, string? restartProcessPath)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => RequestApplicationExitCore(source, restartProcessPath));
            return;
        }

        Interlocked.Exchange(ref _explicitExitRequested, 1);
        _pendingRestartProcessPath = restartProcessPath;
        SimpleLogger.Log($"[SHUTDOWN] Explicit exit requested. Source={source} RestartPending={!string.IsNullOrWhiteSpace(restartProcessPath)}");

        if (Current.MainWindow is Window mainWindow)
        {
            mainWindow.Close();
            return;
        }

        Shutdown();
    }

    private void SetupExceptionHandling()
    {
        this.DispatcherUnhandledException += (s, args) =>
        {
            SimpleLogger.LogError(args.Exception, "DispatcherUnhandledException");
            args.Handled = true;
            BeginFatalShutdown("DispatcherUnhandledException");
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                SimpleLogger.LogError(ex, "AppDomain.UnhandledException");
            }

            try
            {
                TerminalProcessJob.Instance.Dispose();
            }
            catch
            {
            }
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            SimpleLogger.LogError(args.Exception, "TaskScheduler.UnobservedTaskException");
            args.SetObserved();
        };
    }

    private void BeginFatalShutdown(string context)
    {
        if (Interlocked.Exchange(ref _fatalShutdownStarted, 1) != 0)
        {
            return;
        }

        SimpleLogger.Log($"[SHUTDOWN] Fatal shutdown started. Context={context}");

        if (Dispatcher.CheckAccess())
        {
            _ = ShutdownAfterFatalExceptionAsync(context);
            return;
        }

        _ = Dispatcher.BeginInvoke(new Action(() => _ = ShutdownAfterFatalExceptionAsync(context)));
    }

    private async Task ShutdownAfterFatalExceptionAsync(string context)
    {
        try
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (Current.MainWindow is Window mainWindow)
            {
                mainWindow.IsEnabled = false;
                mainWindow.ShowInTaskbar = false;
                mainWindow.Hide();
            }

            if (Current.MainWindow is TerminalShell.Views.MainWindow window
                && window.DataContext is TerminalShell.ViewModels.MainViewModel vm)
            {
                await vm.CleanupAsync();
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, $"FatalShutdownCleanup:{context}");
        }
        finally
        {
            Shutdown();
        }
    }
}



