using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TerminalShell.Core;
using TerminalShell.Core.Config;
using TerminalShell.ViewModels;
using TerminalShell.Models;

namespace TerminalShell.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow(TerminalSession session)
    {
        InitializeComponent();
        RuntimeAppIdentity.ApplyWindowIcon(this);
        
        var viewModel = new HistoryViewModel(session);
        viewModel.CloseAction = new System.Action(this.Close);
        this.DataContext = viewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RestoreSavedLayout();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        SaveCurrentLayout();
    }

    private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (this.DataContext is HistoryViewModel vm)
        {
            vm.RestoreHistoryCommand.Execute(null);
        }
    }

    private void RestoreSavedLayout()
    {
        try
        {
            AppConfig config = ConfigManager.Instance.Config;
            Width = HistoryWindowLayout.ClampWindowWidth(config.HistoryWindowWidth);
            Height = HistoryWindowLayout.ClampWindowHeight(config.HistoryWindowHeight);

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                UpdateLayout();

                double totalAvailableWidth = SplitViewGrid.ActualWidth > 0
                    ? SplitViewGrid.ActualWidth
                    : HistoryWindowLayout.DefaultWindowWidth;
                double splitterWidth = SplitterColumn.ActualWidth > 0
                    ? SplitterColumn.ActualWidth
                    : HistoryWindowLayout.DefaultSplitterWidth;
                double leftPaneWidth = HistoryWindowLayout.ClampRestoredLeftPaneWidth(
                    config.HistoryWindowLeftPaneWidth,
                    totalAvailableWidth,
                    splitterWidth);

                LeftPaneColumn.Width = new GridLength(leftPaneWidth, GridUnitType.Pixel);
            }));
        }
        catch
        {
        }
    }

    private void SaveCurrentLayout()
    {
        try
        {
            AppConfig config = ConfigManager.Instance.Config;
            Rect windowBounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, Width, Height)
                : RestoreBounds;
            double measuredLeftPaneWidth = LeftPaneColumn.ActualWidth > 0
                ? LeftPaneColumn.ActualWidth
                : config.HistoryWindowLeftPaneWidth;

            config.HistoryWindowWidth = HistoryWindowLayout.ClampWindowWidth(windowBounds.Width);
            config.HistoryWindowHeight = HistoryWindowLayout.ClampWindowHeight(windowBounds.Height);
            config.HistoryWindowLeftPaneWidth = HistoryWindowLayout.ClampSavedLeftPaneWidth(measuredLeftPaneWidth);

            ConfigManager.Instance.Save();
        }
        catch
        {
        }
    }
}
