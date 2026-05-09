using System;
using System.Globalization;
using System.Windows.Data;

namespace TerminalShell.Converters;

public class PercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double parentSize && parameter is string percentageStr && double.TryParse(percentageStr, out double percentage))
        {
            return parentSize * (percentage / 100.0);
        }
        // Also maximize dynamic binding if parameter is not string constant but passed via Binding?
        // But for this use case, we might bind MaxHeight to WindowHeight * (Config.Percent / 100)
        // This requires Multibinding.
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class DynamicPercentageConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // values[0]: Parent Dimension (e.g. Window Height or Container Height)
        // values[1]: Percentage (0-100)
        
        if (values.Length >= 2 && 
            values[0] is double parentDimension && 
            values[1] is double percentage)
        {
            return parentDimension * (percentage / 100.0);
        }
        return Double.MaxValue; 
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
