using System;
using System.Globalization;
using System.Windows.Data;
using TerminalShell.Models;

namespace TerminalShell.Converters;

public class IsMaximizedSessionConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2) return false;

        var currentSession = values[0] as TerminalSession;
        var maximizedSession = values[1] as TerminalSession;

        if (maximizedSession != null && currentSession == maximizedSession)
        {
            return true;
        }

        return false;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
