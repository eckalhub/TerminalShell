using System.IO;
using System.Text.RegularExpressions;

namespace TerminalShell.Services;

public sealed class ParsedCustomCommand
{
    public string CommandText { get; init; } = string.Empty;
    public string CategoryKey { get; init; } = string.Empty;
}

public sealed class CustomCommandValidationResult
{
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;

    public string BuildMessage()
    {
        if (IsValid)
        {
            return string.Empty;
        }

        return "Custom command block tags are not balanced.\n\n" + string.Join("\n", Errors);
    }
}

public static class CustomCommandParser
{
    private static readonly Regex BlockOpenRegex = new(@"^\s*\[(F(?:[1-9]|1[0-2]))\]\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BlockCloseRegex = new(@"^\s*\[/\s*(F(?:[1-9]|1[0-2]))\]\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static List<ParsedCustomCommand> Parse(string? rawText)
    {
        List<ParsedCustomCommand> commands = new();
        string currentCategory = string.Empty;

        using StringReader reader = new(rawText ?? string.Empty);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            Match openMatch = BlockOpenRegex.Match(trimmed);
            if (openMatch.Success)
            {
                currentCategory = openMatch.Groups[1].Value.ToUpperInvariant();
                continue;
            }

            if (BlockCloseRegex.IsMatch(trimmed))
            {
                currentCategory = string.Empty;
                continue;
            }

            commands.Add(new ParsedCustomCommand
            {
                CommandText = trimmed,
                CategoryKey = currentCategory
            });
        }

        return commands;
    }

    public static CustomCommandValidationResult Validate(string? rawText)
    {
        CustomCommandValidationResult result = new();
        string? currentCategory = null;
        int currentCategoryLine = 0;

        using StringReader reader = new(rawText ?? string.Empty);
        string? line;
        int lineNumber = 0;
        while ((line = reader.ReadLine()) != null)
        {
            lineNumber++;
            string trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            Match openMatch = BlockOpenRegex.Match(trimmed);
            if (openMatch.Success)
            {
                string openTag = openMatch.Groups[1].Value.ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(currentCategory))
                {
                    result.Errors.Add($"Line {lineNumber}: found [{openTag}] before [{currentCategory}] opened at line {currentCategoryLine} was closed with [/{currentCategory}].");
                }

                currentCategory = openTag;
                currentCategoryLine = lineNumber;
                continue;
            }

            Match closeMatch = BlockCloseRegex.Match(trimmed);
            if (closeMatch.Success)
            {
                string closeTag = closeMatch.Groups[1].Value.ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(currentCategory))
                {
                    result.Errors.Add($"Line {lineNumber}: found closing tag [/{closeTag}] without a matching open tag [{closeTag}].");
                    continue;
                }

                if (!string.Equals(currentCategory, closeTag, StringComparison.Ordinal))
                {
                    result.Errors.Add($"Line {lineNumber}: expected [/{currentCategory}] to close [{currentCategory}] opened at line {currentCategoryLine}, but found [/{closeTag}].");
                    currentCategory = null;
                    currentCategoryLine = 0;
                    continue;
                }

                currentCategory = null;
                currentCategoryLine = 0;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentCategory))
        {
            result.Errors.Add($"Line {currentCategoryLine}: [{currentCategory}] is not closed. Expected closing tag [/{currentCategory}].");
        }

        return result;
    }
}
