using TerminalShell.Services;

namespace TerminalShell.Tests.Services;

public class ShutdownGuardEvaluatorTests
{
    [Fact]
    public void EvaluateStatus_ShouldReturnBusy_WhenBusyMarkerExists()
    {
        ShutdownGuardTerminalStatus? status = ShutdownGuardEvaluator.EvaluateStatus(
            isProcessAlive: true,
            isFailed: false,
            isWaitingForUserInput: false,
            hasSeenBusyState: false,
            lastCommandUtc: null,
            lastSnapshotChangedUtc: DateTimeOffset.UtcNow,
            snapshot: "Working (3m 12s • esc to interrupt)",
            now: DateTimeOffset.UtcNow,
            submittedGracePeriod: TimeSpan.FromSeconds(60),
            stableThreshold: TimeSpan.FromMinutes(2));

        Assert.Equal(ShutdownGuardTerminalStatus.Busy, status);
    }

    [Fact]
    public void EvaluateStatus_ShouldReturnSubmitted_WhenCommandWasRecentlySent()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        ShutdownGuardTerminalStatus? status = ShutdownGuardEvaluator.EvaluateStatus(
            isProcessAlive: true,
            isFailed: false,
            isWaitingForUserInput: false,
            hasSeenBusyState: false,
            lastCommandUtc: now.AddSeconds(-10),
            lastSnapshotChangedUtc: now.AddSeconds(-10),
            snapshot: string.Empty,
            now: now,
            submittedGracePeriod: TimeSpan.FromSeconds(60),
            stableThreshold: TimeSpan.FromMinutes(2));

        Assert.Equal(ShutdownGuardTerminalStatus.Submitted, status);
    }

    [Fact]
    public void EvaluateStatus_ShouldReturnWaitingForInput_WhenUserInteractionIsRequired()
    {
        ShutdownGuardTerminalStatus? status = ShutdownGuardEvaluator.EvaluateStatus(
            isProcessAlive: true,
            isFailed: false,
            isWaitingForUserInput: true,
            hasSeenBusyState: true,
            lastCommandUtc: DateTimeOffset.UtcNow.AddMinutes(-1),
            lastSnapshotChangedUtc: DateTimeOffset.UtcNow.AddSeconds(-30),
            snapshot: "Implement this plan?",
            now: DateTimeOffset.UtcNow,
            submittedGracePeriod: TimeSpan.FromSeconds(60),
            stableThreshold: TimeSpan.FromMinutes(2));

        Assert.Equal(ShutdownGuardTerminalStatus.WaitingForInput, status);
    }

    [Fact]
    public void EvaluateStatus_ShouldReturnPendingCompletion_WhenBusyRoundHasNotSettledYet()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        ShutdownGuardTerminalStatus? status = ShutdownGuardEvaluator.EvaluateStatus(
            isProcessAlive: true,
            isFailed: false,
            isWaitingForUserInput: false,
            hasSeenBusyState: true,
            lastCommandUtc: now.AddMinutes(-3),
            lastSnapshotChangedUtc: now.AddSeconds(-20),
            snapshot: "I am still writing the patch summary",
            now: now,
            submittedGracePeriod: TimeSpan.FromSeconds(60),
            stableThreshold: TimeSpan.FromMinutes(2));

        Assert.Equal(ShutdownGuardTerminalStatus.PendingCompletion, status);
    }

    [Fact]
    public void EvaluateStatus_ShouldReturnNull_WhenBusyRoundAlreadyLooksCompleted()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        ShutdownGuardTerminalStatus? status = ShutdownGuardEvaluator.EvaluateStatus(
            isProcessAlive: true,
            isFailed: false,
            isWaitingForUserInput: false,
            hasSeenBusyState: true,
            lastCommandUtc: now.AddMinutes(-10),
            lastSnapshotChangedUtc: now.AddMinutes(-3),
            snapshot: "• Worked for 8m 45s\n\n>",
            now: now,
            submittedGracePeriod: TimeSpan.FromSeconds(60),
            stableThreshold: TimeSpan.FromMinutes(2));

        Assert.Null(status);
    }
}
