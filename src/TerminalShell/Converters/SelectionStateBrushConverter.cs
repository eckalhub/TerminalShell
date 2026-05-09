using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TerminalShell.Converters;

public sealed class SelectionStateBrushConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool isSelected = values.Length > 0 && values[0] is bool selected && selected;
        bool isMouseOver = values.Length > 1 && values[1] is bool mouseOver && mouseOver;

        int targetIndex = isSelected ? 4 : isMouseOver ? 3 : 2;
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
