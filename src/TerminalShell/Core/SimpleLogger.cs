using System;
using System.Text;

namespace TerminalShell.Core
{
    public static class SimpleLogger
    {
        public static void Log(string message)
        {
            _ = message;
        }

        public static void LogError(Exception ex, string context = "")
        {
            _ = ex;
            _ = context;
        }

        public static string BuildLogEntry(string message)
        {
            return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
        }

        public static string BuildErrorEntry(Exception ex, string context = "")
        {
            StringBuilder builder = new();
            builder.Append(BuildLogEntry($"[ERROR] {context}".TrimEnd()));
            builder.AppendLine(ex.ToString());
            builder.AppendLine(new string('-', 80));
            return builder.ToString();
        }
    }
}
