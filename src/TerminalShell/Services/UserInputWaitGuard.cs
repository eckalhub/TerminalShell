using System.Text.RegularExpressions;

namespace TerminalShell.Services;

public static class UserInputWaitGuard
{
    private static readonly char[] KeywordSeparators = ['\n', ',', ';', '|', '，', '；'];
    private static readonly Regex ChoiceLeadRegex = new(
        @"\b(?:next step|next steps|suggest|recommended|recommend|priority|choose|select|option|which one|which option|pick one|one of these|one of the following)\b|下一步|建议|优先|请选择|选择|选哪个|需要你选择|按.*顺序|先落|我可以直接给你改成|(?:这|以下).{0,8}(?:种|项|个|套|方案|做法).{0,6}(?:之一|里选|可选)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OptionLineRegex = new(
        @"^(?:\d+\.|[-*•]|[一二三四五六七八九十]+[、.])\s+\S",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StructuredChoiceRegex = new(
        @"\[[^\]]*(?:y\s*/\s*n|n\s*/\s*y|yes\s*/\s*no)[^\]]*\]|\([^\)]*(?:y\s*/\s*n|n\s*/\s*y|yes\s*/\s*no)[^\)]*\)|\b(?:yes\s*/\s*no|y\s*/\s*n|n\s*/\s*y)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ExplicitConfirmationInstructionRegex = new(
        @"\bpress\s+enter\s+to\s+confirm\b|\bpress\s+enter\s+to\s+continue\b|\besc(?:ape)?\s+to\s+go\s+back\b|按回车确认|按\s*enter\s*确认|按\s*esc\s*返回",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<string> ParseKeywords(string? rawKeywords)
    {
        if (string.IsNullOrWhiteSpace(rawKeywords))
        {
            return Array.Empty<string>();
        }

        return rawKeywords
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split(KeywordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsMatch(string? snapshot, IEnumerable<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return false;
        }

        List<string> recentLines = snapshot
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .TakeLast(10)
            .ToList();

        if (recentLines.Count == 0)
        {
            return false;
        }

        bool hasTrailingPrompt = recentLines.TakeLast(2).Any(IsPromptLine);
        List<string> candidateLines = recentLines
            .Where(line => !IsPromptLine(line))
            .TakeLast(4)
            .ToList();

        if (candidateLines.Count == 0)
        {
            return false;
        }

        if (recentLines.Any(line => ExplicitConfirmationInstructionRegex.IsMatch(line)))
        {
            return true;
        }

        if (candidateLines.Any(line => StructuredChoiceRegex.IsMatch(line)))
        {
            return true;
        }

        IReadOnlyList<string> keywordList = keywords as IReadOnlyList<string> ?? keywords.ToArray();
        if (HasChoiceListPrompt(recentLines, keywordList))
        {
            return true;
        }

        if (keywordList.Count == 0)
        {
            return false;
        }

        foreach (string line in candidateLines)
        {
            if (!ContainsKeyword(line, keywordList))
            {
                continue;
            }

            if (hasTrailingPrompt
                || line.Contains('?', StringComparison.Ordinal)
                || line.Contains('？', StringComparison.Ordinal)
                || line.Contains(':', StringComparison.Ordinal)
                || line.Contains('：', StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasChoiceListPrompt(IReadOnlyList<string> recentLines, IReadOnlyList<string> keywords)
    {
        for (int leadLineIndex = 0; leadLineIndex < recentLines.Count; leadLineIndex++)
        {
            string leadLine = recentLines[leadLineIndex];
            if (!ChoiceLeadRegex.IsMatch(leadLine)
                && !ContainsKeyword(leadLine, keywords)
                && !LooksLikeQuestionLeadLine(leadLine))
            {
                continue;
            }

            int firstOptionIndex = -1;
            int searchWindowEnd = Math.Min(recentLines.Count, leadLineIndex + 5);
            for (int i = leadLineIndex + 1; i < searchWindowEnd; i++)
            {
                if (OptionLineRegex.IsMatch(recentLines[i]))
                {
                    firstOptionIndex = i;
                    break;
                }
            }

            if (firstOptionIndex < 0)
            {
                continue;
            }

            int optionCount = 0;
            int trailingNonOptionLineCount = 0;
            for (int i = firstOptionIndex; i < recentLines.Count; i++)
            {
                if (OptionLineRegex.IsMatch(recentLines[i]))
                {
                    optionCount++;
                    trailingNonOptionLineCount = 0;
                    continue;
                }

                if (optionCount == 0)
                {
                    break;
                }

                trailingNonOptionLineCount++;
                if (trailingNonOptionLineCount > 1)
                {
                    break;
                }
            }

            if (optionCount >= 2)
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeQuestionLeadLine(string line)
    {
        return line.Contains('?', StringComparison.Ordinal)
            || line.Contains('？', StringComparison.Ordinal);
    }

    private static bool ContainsKeyword(string line, IReadOnlyList<string> keywords)
    {
        foreach (string keyword in keywords)
        {
            if (line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPromptLine(string line)
    {
        string trimmed = line.Trim();
        return string.Equals(trimmed, ">", StringComparison.Ordinal)
            || string.Equals(trimmed, "›", StringComparison.Ordinal);
    }
}
