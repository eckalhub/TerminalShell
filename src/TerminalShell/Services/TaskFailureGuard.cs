namespace TerminalShell.Services;

public static class TaskFailureGuard
{
    private static readonly char[] KeywordSeparators = ['\n', ',', ';', '|', '，', '；'];

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
            .OrderByDescending(keyword => keyword.Length)
            .ToArray();
    }

    public static bool TryMatch(string? snapshot, IEnumerable<string> keywords, out string matchedKeyword)
    {
        matchedKeyword = string.Empty;
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return false;
        }

        IReadOnlyList<string> keywordList = keywords as IReadOnlyList<string> ?? keywords.ToArray();
        if (keywordList.Count == 0)
        {
            return false;
        }

        List<string> recentLines = snapshot
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .TakeLast(12)
            .ToList();

        if (recentLines.Count == 0)
        {
            return false;
        }

        for (int lineIndex = recentLines.Count - 1; lineIndex >= 0; lineIndex--)
        {
            string line = recentLines[lineIndex];
            foreach (string keyword in keywordList)
            {
                if (!line.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matchedKeyword = keyword;
                return true;
            }
        }

        return false;
    }
}
