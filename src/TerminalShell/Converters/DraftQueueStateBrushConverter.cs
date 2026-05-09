using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TerminalShell.Converters;

public sealed class DraftQueueStateBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool isWaiting = values.Length > 0 && values[0] is bool waiting && waiting;
        bool isActive = values.Length > 1 && values[1] is bool active && active;

        int targetIndex = isWaiting ? 4 : isActive ? 3 : 2;
        if (values.Length > targetIndex && values[targetIndex] is System.Windows.Media.Brush brush)
        {
            return brush;
        }

        return DependencyProperty.UnsetValue;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
