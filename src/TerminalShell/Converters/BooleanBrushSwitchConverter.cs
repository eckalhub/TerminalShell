using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TerminalShell.Converters;

public sealed class BooleanBrushSwitchConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool isActive = values.Length > 0 && values[0] is bool active && active;
        int targetIndex = isActive ? 2 : 1;

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
