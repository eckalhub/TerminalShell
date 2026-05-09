using System.Text;

namespace TerminalShell.Services;

public enum ShutdownGuardTerminalStatus
{
    Submitted,
    Busy,
    WaitingForInput,
    PendingCompletion
}

public sealed record ShutdownGuardTerminalActivity(string TerminalName, ShutdownGuardTerminalStatus Status)
{
    public string StatusLabel => Status switch
    {
        ShutdownGuardTerminalStatus.Submitted => "Submitted",
        ShutdownGuardTerminalStatus.Busy => "Busy",
        ShutdownGuardTerminalStatus.WaitingForInput => "Waiting for input",
        ShutdownGuardTerminalStatus.PendingCompletion => "Pending completion",
        _ => Status.ToString()
    };
}

public static class ShutdownGuardEvaluator
{
    private static readonly string[] BusyMarkers =
    {
        "Working (",
        "Working...",
        "Working…",
        "esc to interrupt",
        "Orbiting",
        "thinking"
    };

    private static readonly string[] CompletionMarkers =
    {
        "Worked for "
    };

    private static readonly string[] PromptMarkers =
    {
        ">",
        "›"
    };

    public static ShutdownGuardTerminalStatus? EvaluateStatus(
        bool isProcessAlive,
        bool isFailed,
        bool isWaitingForUserInput,
        bool hasSeenBusyState,
        DateTimeOffset? lastCommandUtc,
        DateTimeOffset lastSnapshotChangedUtc,
        string? snapshot,
        DateTimeOffset now,
        TimeSpan submittedGracePeriod,
        TimeSpan stableThreshold)
    {
        if (!isProcessAlive || isFailed)
        {
            return null;
        }

        string normalizedSnapshot = NormalizeSnapshot(snapshot);

        if (isWaitingForUserInput)
        {
            return ShutdownGuardTerminalStatus.WaitingForInput;
        }

        if (ContainsBusyMarker(normalizedSnapshot))
        {
            return ShutdownGuardTerminalStatus.Busy;
        }

        if (lastCommandUtc.HasValue
            && !hasSeenBusyState
            && now - lastCommandUtc.Value < submittedGracePeriod)
        {
            return ShutdownGuardTerminalStatus.Submitted;
        }

        if (!hasSeenBusyState)
        {
            return null;
        }

        TimeSpan stableDuration = now - lastSnapshotChangedUtc;
        bool highConfidenceCompleted = stableDuration >= stableThreshold
            && (ContainsCompletionMarker(normalizedSnapshot)
                || EndsWithPromptMarker(normalizedSnapshot)
                || stableDuration >= stableThreshold + stableThreshold);

        if (highConfidenceCompleted)
        {
            return null;
        }

        return ShutdownGuardTerminalStatus.PendingCompletion;
    }

    public static string BuildConfirmationMessage(IReadOnlyList<ShutdownGuardTerminalActivity> activities, int maxItems = 6)
    {
        if (activities == null || activities.Count == 0)
        {
            return string.Empty;
        }

        int effectiveMaxItems = Math.Max(1, maxItems);
        StringBuilder builder = new();
        builder.AppendLine("Some terminals still appear to have unfinished work:");
        builder.AppendLine();

        foreach (ShutdownGuardTerminalActivity activity in activities.Take(effectiveMaxItems))
        {
            builder.AppendLine($"- {activity.TerminalName} ({activity.StatusLabel})");
        }

        if (activities.Count > effectiveMaxItems)
        {
            builder.AppendLine($"- +{activities.Count - effectiveMaxItems} more");
        }

        builder.AppendLine();
        builder.Append("Close TerminalShell anyway?");
        return builder.ToString();
    }

    private static bool ContainsBusyMarker(string snapshot)
    {
        return BusyMarkers.Any(marker => snapshot.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsCompletionMarker(string snapshot)
    {
        return CompletionMarkers.Any(marker => snapshot.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EndsWithPromptMarker(string snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return false;
        }

        string? lastNonEmptyLine = snapshot
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .LastOrDefault(line => !string.IsNullOrWhiteSpace(line));

        if (string.IsNullOrWhiteSpace(lastNonEmptyLine))
        {
            return false;
        }

        return PromptMarkers.Any(marker => string.Equals(lastNonEmptyLine, marker, StringComparison.Ordinal));
    }

    private static string NormalizeSnapshot(string? snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return string.Empty;
        }

        List<string> lines = snapshot
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        return string.Join("\n", lines);
    }
}
