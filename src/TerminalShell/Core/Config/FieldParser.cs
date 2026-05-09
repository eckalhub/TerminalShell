using System;
using System.Text.Json;

namespace TerminalShell.Core.Config;

// Skill: config_json_style (Level 3 Defense)
public static class FieldParser
{
    public static string ParseString(JsonElement root, string propertyName, string defaultValue)
    {
        if (root.TryGetProperty(propertyName, out var element))
        {
            return element.ToString() ?? defaultValue;
        }
        return defaultValue;
    }

    public static int ParseInt(JsonElement root, string propertyName, int defaultValue)
    {
        return ParseInt(root, propertyName, defaultValue, int.MinValue, int.MaxValue);
    }

    public static int ParseInt(JsonElement root, string propertyName, int defaultValue, int min, int max)
    {
        if (root.TryGetProperty(propertyName, out var element))
        {
            int value = defaultValue;
            bool success = false;

            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int iVal))
            {
                value = iVal;
                success = true;
            }
            else if (element.ValueKind == JsonValueKind.String && int.TryParse(element.ToString(), out int parsedValue))
            {
                value = parsedValue;
                success = true;
            }
            
            if (success)
            {
                return Math.Clamp(value, min, max);
            }
        }
        return defaultValue;
    }

    public static double ParseDouble(JsonElement root, string propertyName, double defaultValue)
    {
        return ParseDouble(root, propertyName, defaultValue, double.MinValue, double.MaxValue);
    }

    public static double ParseDouble(JsonElement root, string propertyName, double defaultValue, double min, double max)
    {
        if (root.TryGetProperty(propertyName, out var element))
        {
            double value = defaultValue;
            bool success = false;

            if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out double dVal))
            {
                value = dVal;
                success = true;
            }
            else if (element.ValueKind == JsonValueKind.String && double.TryParse(element.ToString(), out double parsedValue))
            {
                value = parsedValue;
                success = true;
            }

            if (success)
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) return defaultValue;
                return Math.Clamp(value, min, max);
            }
        }
        return defaultValue;
    }

    public static bool ParseBool(JsonElement root, string propertyName, bool defaultValue)
    {
        if (root.TryGetProperty(propertyName, out var element))
        {
            if (element.ValueKind == JsonValueKind.True) return true;
            if (element.ValueKind == JsonValueKind.False) return false;
            
            string sVal = element.ToString();
            if (bool.TryParse(sVal, out bool parsedValue)) return parsedValue;
            
            // Loose parsing for "1"/"0"/"yes"/"no" could be added here if needed
        }
        return defaultValue;
    }
}
