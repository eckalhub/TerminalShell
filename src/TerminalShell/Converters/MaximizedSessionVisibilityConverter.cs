using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TerminalShell.Models;

namespace TerminalShell.Converters;

public class MaximizedSessionVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // Value 0: The current TerminalSession (from DataContext)
        // Value 1: The MaximizedSession (from MainViewModel)
        
        if (values.Length < 2) return Visibility.Visible;

        var currentSession = values[0] as TerminalSession;
        var maximizedSession = values[1] as TerminalSession;

        // If no session is maximized, everyone is visible
        if (maximizedSession == null)
        {
            return Visibility.Visible;
        }

        // If a session IS maximized, only show that one
        if (currentSession == maximizedSession)
        {
            return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
