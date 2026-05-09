using System;
using System.Globalization;
using System.Windows.Data;

namespace TerminalShell.Converters
{
    public class DynamicStringFormatConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return string.Empty;

            var format = values[0] as string;
            if (string.IsNullOrEmpty(format))
                return string.Empty;

            var terminalName = values[1]?.ToString() ?? "Unknown";
            var terminalVoiceName = values.Length > 2 ? values[2]?.ToString() ?? string.Empty : string.Empty;
            var statusSuffix = values.Length > 3 ? values[3]?.ToString() ?? string.Empty : string.Empty;
            var effectiveTerminalName = string.IsNullOrWhiteSpace(terminalVoiceName) ? terminalName : terminalVoiceName.Trim();

            // Watermark terminal-name placeholders should all prefer TerminalVoiceName first.
            // Use {RawTerminalName} only when the original terminal Name is explicitly required.
            var rendered = format.Replace("{RawTerminalName}", terminalName, StringComparison.OrdinalIgnoreCase)
                                 .Replace("{TerminalVoiceName_or_TerminalName}", effectiveTerminalName, StringComparison.OrdinalIgnoreCase)
                                 .Replace("{TerminalName}", effectiveTerminalName, StringComparison.OrdinalIgnoreCase)
                                 .Replace("{0}", effectiveTerminalName, StringComparison.OrdinalIgnoreCase);

            return string.IsNullOrWhiteSpace(statusSuffix)
                ? rendered
                : $"{rendered} {statusSuffix.Trim()}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
