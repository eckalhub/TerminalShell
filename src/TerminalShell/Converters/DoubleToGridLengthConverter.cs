using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TerminalShell.Converters;

[ValueConversion(typeof(double), typeof(GridLength))]
public class DoubleToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double val)
            return new GridLength(val, GridUnitType.Pixel);
        return new GridLength(250, GridUnitType.Pixel);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is GridLength gl)
            return gl.Value;
        return 250.0;
    }
}
