namespace TerminalShell.Core;

public readonly record struct CustomCommandInsertResult(string Text, int CaretIndex);

public static class CustomCommandInsertHelper
{
    public const string ReturnToken = "[return]";

    public static CustomCommandInsertResult InsertMacroAtCaret(string? currentText, int caretIndex, string macroKey, string? newline = null)
    {
        string sourceText = currentText ?? string.Empty;
        int insertIndex = Math.Clamp(caretIndex, 0, sourceText.Length);

        string normalizedMacroKey = NormalizeMacroKey(macroKey);
        string effectiveNewline = newline ?? Environment.NewLine;

        string insertText;
        int caretOffset;

        if (string.Equals(normalizedMacroKey, ReturnToken, StringComparison.Ordinal))
        {
            insertText = ReturnToken;
            caretOffset = ReturnToken.Length;
        }
        else
        {
            string blockKey = NormalizeBlockKey(normalizedMacroKey);
            string openingTag = $"[{blockKey}]";
            string closingTag = $"[/{blockKey}]";
            insertText = $"{openingTag}{effectiveNewline}{effectiveNewline}{closingTag}";
            caretOffset = openingTag.Length + effectiveNewline.Length;
        }

        string mergedText = sourceText.Insert(insertIndex, insertText);
        return new CustomCommandInsertResult(mergedText, insertIndex + caretOffset);
    }

    private static string NormalizeMacroKey(string macroKey)
    {
        string trimmed = macroKey?.Trim() ?? string.Empty;
        if (string.Equals(trimmed, "return", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, ReturnToken, StringComparison.OrdinalIgnoreCase))
        {
            return ReturnToken;
        }

        if (trimmed.StartsWith("[", StringComparison.Ordinal)
            && trimmed.EndsWith("]", StringComparison.Ordinal)
            && trimmed.Length >= 3)
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static string NormalizeBlockKey(string blockKey)
    {
        if (string.IsNullOrWhiteSpace(blockKey))
        {
            return "F1";
        }

        return blockKey.ToUpperInvariant();
    }
}
