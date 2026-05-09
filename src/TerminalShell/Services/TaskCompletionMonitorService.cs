using System.IO;
using System.Windows;
using System.Windows.Threading;
using TerminalShell.Core;
using TerminalShell.Core.Config;
using TerminalShell.Models;
using TerminalShell.Views;

namespace TerminalShell.Services;

public sealed class TaskCompletionMonitorService : IDisposable
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

    private readonly IConfigManager _configManager;
    private readonly ITaskCompletionSpeechService _speechService;
    private readonly AiCompletionInboxService _completionInboxService;
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _allIdleVoiceRepeatTimer;
    private readonly Dictionary<TerminalSession, SessionMonitorState> _sessionStates = new();
    private readonly Dictionary<string, IdleSnapshotState> _idleSnapshotStates = new(StringComparer.OrdinalIgnoreCase);

    private bool _isPolling;
    private bool _enableWaitForUserInputGuard;
    private bool _allIdleWaveActive;
    private bool _allIdleWaveAcknowledged;
    private AllTerminalsIdleAlertWindow? _allIdleAlertWindow;
    private IReadOnlyList<string> _waitForUserInputKeywords = Array.Empty<string>();
    private IReadOnlyList<string> _failureKeywords = Array.Empty<string>();

    public TaskCompletionMonitorService(IConfigManager configManager)
    {
        _configManager = configManager;
        _waitForUserInputKeywords = UserInputWaitGuard.ParseKeywords(_configManager.Config.WaitForUserInputKeywords);
        _failureKeywords = TaskFailureGuard.ParseKeywords(_configManager.Config.TaskFailureKeywords);
        _speechService = new TaskCompletionSpeechService();
        _completionInboxService = new AiCompletionInboxService();
        _completionInboxService.CompletionSignalReceived += CompletionInboxService_CompletionSignalReceived;

        _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(Math.Clamp(_configManager.Config.TaskCompletionCheckIntervalSeconds, 1, 3600))
        };
        _pollTimer.Tick += PollTimer_Tick;
        _pollTimer.Start();

        _allIdleVoiceRepeatTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(Math.Clamp(_configManager.Config.AllTerminalsIdleVoiceRepeatMinutes, 1, 1440))
        };
        _allIdleVoiceRepeatTimer.Tick += AllIdleVoiceRepeatTimer_Tick;
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _pollTimer.Tick -= PollTimer_Tick;

        _allIdleVoiceRepeatTimer.Stop();
        _allIdleVoiceRepeatTimer.Tick -= AllIdleVoiceRepeatTimer_Tick;

        _completionInboxService.CompletionSignalReceived -= CompletionInboxService_CompletionSignalReceived;
        _completionInboxService.Dispose();

        foreach (TerminalSession session in _sessionStates.Keys.ToList())
        {
            session.UserCommandSent -= Session_UserCommandSent;
            session.UserInteractionOccurred -= Session_UserInteractionOccurred;
        }

        if (_allIdleAlertWindow != null)
        {
            _allIdleAlertWindow.AlertDismissed -= AllIdleAlertWindow_AlertDismissed;
            _allIdleAlertWindow.Close();
            _allIdleAlertWindow = null;
        }

        _speechService.Dispose();
    }

    public void RefreshConfiguration()
    {
        TimeSpan pollInterval = TimeSpan.FromSeconds(Math.Clamp(_configManager.Config.TaskCompletionCheckIntervalSeconds, 1, 3600));
        if (_pollTimer.Interval != pollInterval)
        {
            _pollTimer.Interval = pollInterval;
        }

        _enableWaitForUserInputGuard = _configManager.Config.EnableWaitForUserInputGuard;
        _waitForUserInputKeywords = UserInputWaitGuard.ParseKeywords(_configManager.Config.WaitForUserInputKeywords);
        _failureKeywords = TaskFailureGuard.ParseKeywords(_configManager.Config.TaskFailureKeywords);

        TimeSpan repeatInterval = TimeSpan.FromMinutes(Math.Clamp(_configManager.Config.AllTerminalsIdleVoiceRepeatMinutes, 1, 1440));
        if (_allIdleVoiceRepeatTimer.Interval != repeatInterval)
        {
            _allIdleVoiceRepeatTimer.Interval = repeatInterval;
        }

        if (!ShouldMonitorSingleTerminalRounds())
        {
            foreach (SessionMonitorState state in _sessionStates.Values)
            {
                state.IsMonitoring = state.IsAutoDraftQueueActive && !state.IsFailed;
            }
        }

        if (!IsAnyAllIdleAlertEnabled())
        {
            EndAllIdleWave(dismissWindow: true);
        }
        else if (!IsAllIdleVoiceEnabled())
        {
            _allIdleVoiceRepeatTimer.Stop();
        }

        if (!_enableWaitForUserInputGuard)
        {
            foreach ((TerminalSession session, SessionMonitorState state) in _sessionStates)
            {
                SetWaitingForUserInputState(session, state, false, "guard-disabled");
            }
        }
    }

    public void UpdateSessions(IEnumerable<TerminalSession> sessions)
    {
        RefreshConfiguration();

        HashSet<TerminalSession> activeSessions = new(sessions);
        List<TerminalSession> removedSessions = _sessionStates.Keys.Where(session => !activeSessions.Contains(session)).ToList();
        foreach (TerminalSession session in removedSessions)
        {
            session.UserCommandSent -= Session_UserCommandSent;
            session.UserInteractionOccurred -= Session_UserInteractionOccurred;
            _sessionStates.Remove(session);
            _idleSnapshotStates.Remove(session.Name);
        }

        foreach (TerminalSession session in activeSessions)
        {
            if (_sessionStates.ContainsKey(session))
            {
                continue;
            }

            _sessionStates[session] = new SessionMonitorState();
            _idleSnapshotStates[session.Name] = new IdleSnapshotState();
            session.UserCommandSent += Session_UserCommandSent;
            session.UserInteractionOccurred += Session_UserInteractionOccurred;
        }

        foreach ((TerminalSession session, SessionMonitorState state) in _sessionStates)
        {
            RefreshAutoDraftQueueState(session, state, "session-refresh");
        }
    }

    private void Session_UserCommandSent(TerminalSession session, TerminalCommandSentEventArgs command)
    {
        SessionMonitorState state = GetOrCreateState(session);
        state.HasSeenBusyState = false;
        state.HasAnnounced = false;
        state.IsFailed = false;
        state.LastFailureKeyword = string.Empty;
        state.LastCommand = command.CommandText;
        state.LastCommandUtc = DateTimeOffset.UtcNow;
        state.RoundId++;
        SetWaitingForUserInputState(session, state, false, $"send:{command.Origin}");
        RefreshAutoDraftQueueState(session, state, $"send:{command.Origin}");
    }

    private void Session_UserInteractionOccurred(TerminalSession session, TerminalUserInteractionKind kind)
    {
        SessionMonitorState state = GetOrCreateState(session);
        RefreshAutoDraftQueueState(session, state, $"user:{kind}");
    }

    private void CompletionInboxService_CompletionSignalReceived(AiCompletionSignal signal)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => HandleCompletionSignal(signal)));
    }

    private void HandleCompletionSignal(AiCompletionSignal signal)
    {
        foreach ((TerminalSession session, SessionMonitorState state) in _sessionStates)
        {
            if (!state.IsMonitoring || state.HasAnnounced)
            {
                continue;
            }

            if (state.IsAutoDraftQueueActive && !state.HasSeenBusyState)
            {
                continue;
            }

            if (!SignalMatchesSession(signal, session))
            {
                continue;
            }

            int tailLineCount = Math.Clamp(_configManager.Config.TaskCompletionTailLineCount, 5, 200);
            string snapshot = NormalizeSnapshot(session.ReadTerminalTailSnapshot(tailLineCount));
            if (UpdateFailureState(session, state, snapshot, $"official:{signal.Source}:{signal.EventName}"))
            {
                continue;
            }

            if (UpdateWaitingForUserInputState(session, state, snapshot, $"official:{signal.Source}:{signal.EventName}"))
            {
                continue;
            }

            CompleteSession(session, state, $"official:{signal.Source}:{signal.EventName}");
            break;
        }
    }

    private async void PollTimer_Tick(object? sender, EventArgs e)
    {
        if (_isPolling)
        {
            return;
        }

        _isPolling = true;
        try
        {
            RefreshConfiguration();

            int tailLineCount = Math.Clamp(_configManager.Config.TaskCompletionTailLineCount, 5, 200);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            List<TerminalSession> sessions = _sessionStates.Keys.ToList();
            List<(TerminalSession Session, string Snapshot)> snapshots = new();

            foreach (TerminalSession session in sessions)
            {
                string rawSnapshot = await Task.Run(() => session.ReadTerminalTailSnapshot(tailLineCount));
                snapshots.Add((session, NormalizeSnapshot(rawSnapshot)));
            }

            foreach ((TerminalSession session, string snapshot) in snapshots)
            {
                UpdateIdleSnapshotState(session, snapshot, now);
                SessionMonitorState state = GetOrCreateState(session);
                bool isFailure = UpdateFailureState(session, state, snapshot, "poll");
                if (!isFailure)
                {
                    UpdateWaitingForUserInputState(session, state, snapshot, "poll");
                }

                EvaluateSessionCompletion(session, snapshot, now);
            }

            EvaluateAllIdleWave(sessions, now);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "TaskCompletionMonitorService.PollTimer_Tick");
        }
        finally
        {
            _isPolling = false;
        }
    }

    private void EvaluateSessionCompletion(TerminalSession session, string snapshot, DateTimeOffset now)
    {
        SessionMonitorState state = GetOrCreateState(session);
        if (!state.IsMonitoring || state.HasAnnounced)
        {
            return;
        }

        if (state.IsFailed)
        {
            return;
        }

        bool isBusy = ContainsBusyMarker(snapshot);
        if (isBusy)
        {
            state.HasSeenBusyState = true;
            SetWaitingForUserInputState(session, state, false, "busy");
            return;
        }

        if (!state.HasSeenBusyState)
        {
            return;
        }

        if (state.IsWaitingForUserInput)
        {
            return;
        }

        if (!_idleSnapshotStates.TryGetValue(session.Name, out IdleSnapshotState? idleState))
        {
            return;
        }

        TimeSpan stableThreshold = TimeSpan.FromMinutes(Math.Clamp(_configManager.Config.TaskCompletionStableThresholdMinutes, 1, 1440));
        TimeSpan stableDuration = now - idleState.LastChangedUtc;
        if (stableDuration < stableThreshold)
        {
            return;
        }

        bool hasHighConfidenceIdleMarker = ContainsCompletionMarker(snapshot) || EndsWithPromptMarker(snapshot);
        if (hasHighConfidenceIdleMarker)
        {
            CompleteSession(session, state, "fallback:stable-tail+idle-marker");
            return;
        }

        if (stableDuration >= stableThreshold + stableThreshold)
        {
            CompleteSession(session, state, "fallback:stable-tail-timeout");
        }
    }

    private void UpdateIdleSnapshotState(TerminalSession session, string snapshot, DateTimeOffset now)
    {
        if (!_idleSnapshotStates.TryGetValue(session.Name, out IdleSnapshotState? state))
        {
            state = new IdleSnapshotState();
            _idleSnapshotStates[session.Name] = state;
        }

        if (!string.Equals(state.LastSnapshot, snapshot, StringComparison.Ordinal))
        {
            state.LastSnapshot = snapshot;
            state.LastChangedUtc = now;
        }
    }

    private void EvaluateAllIdleWave(IReadOnlyCollection<TerminalSession> sessions, DateTimeOffset now)
    {
        if (!IsAnyAllIdleAlertEnabled())
        {
            EndAllIdleWave(dismissWindow: true);
            return;
        }

        if (sessions.Count == 0)
        {
            EndAllIdleWave(dismissWindow: true);
            return;
        }

        TimeSpan threshold = TimeSpan.FromMinutes(Math.Clamp(_configManager.Config.AllTerminalsIdleThresholdMinutes, 1, 1440));
        bool allIdle = sessions.All(session =>
        {
            if (_sessionStates.TryGetValue(session, out SessionMonitorState? monitorState)
                && (monitorState.IsWaitingForUserInput || monitorState.IsFailed))
            {
                return false;
            }

            if (!_idleSnapshotStates.TryGetValue(session.Name, out IdleSnapshotState? state))
            {
                return false;
            }

            return now - state.LastChangedUtc >= threshold;
        });

        if (allIdle)
        {
            if (!_allIdleWaveActive)
            {
                StartAllIdleWave();
            }
            return;
        }

        EndAllIdleWave();
    }

    private void StartAllIdleWave()
    {
        _allIdleWaveActive = true;
        _allIdleWaveAcknowledged = false;

        if (ShouldShowAllIdleWindow())
        {
            ShowOrActivateAllIdleWindow();
        }

        if (IsAllIdleVoiceEnabled())
        {
            AnnounceAllIdle();
            _allIdleVoiceRepeatTimer.Start();
        }
        else
        {
            _allIdleVoiceRepeatTimer.Stop();
        }
    }

    private void EndAllIdleWave(bool dismissWindow = false)
    {
        if (!_allIdleWaveActive)
        {
            if (dismissWindow)
            {
                DismissAllIdleWindow();
            }
            return;
        }

        _allIdleWaveActive = false;
        _allIdleVoiceRepeatTimer.Stop();

        if (dismissWindow)
        {
            DismissAllIdleWindow();
        }
    }

    private void AllIdleVoiceRepeatTimer_Tick(object? sender, EventArgs e)
    {
        if (!_allIdleWaveActive || _allIdleWaveAcknowledged || !IsAllIdleVoiceEnabled())
        {
            _allIdleVoiceRepeatTimer.Stop();
            return;
        }

        if (ShouldShowAllIdleWindow())
        {
            ShowOrActivateAllIdleWindow();
        }

        AnnounceAllIdle();
    }

    private void ShowOrActivateAllIdleWindow()
    {
        string message = RenderTemplate(_configManager.Config.AllTerminalsIdleTemplate);

        if (_allIdleAlertWindow == null)
        {
            _allIdleAlertWindow = new AllTerminalsIdleAlertWindow(message);
            _allIdleAlertWindow.AlertDismissed += AllIdleAlertWindow_AlertDismissed;
            _allIdleAlertWindow.Show();
        }
        else
        {
            _allIdleAlertWindow.UpdateMessage(message);
            if (!_allIdleAlertWindow.IsVisible)
            {
                _allIdleAlertWindow.Show();
            }
        }

        _allIdleAlertWindow.Activate();
    }

    private void AllIdleAlertWindow_AlertDismissed(object? sender, EventArgs e)
    {
        DismissAllIdleWindow();
        _allIdleWaveAcknowledged = true;
        _allIdleVoiceRepeatTimer.Stop();
    }

    private void AnnounceAllIdle()
    {
        string text = RenderTemplate(_configManager.Config.AllTerminalsIdleTemplate);
        _ = _speechService.SpeakAsync(text, BuildSpeechOptions());
    }

    private void CompleteSession(TerminalSession session, SessionMonitorState state, string source)
    {
        state.IsMonitoring = false;
        state.HasAnnounced = true;

        if (state.IsAutoDraftQueueActive)
        {
            if (session.HasInputDrafts)
            {
                bool autoSendSucceeded = session.TrySendNextDraftAutomatically();
                if (!autoSendSucceeded)
                {
                    StopAutoDraftQueue(session, state, "auto-send-failed");
                }
            }
            else
            {
                StopAutoDraftQueue(session, state, "queue-empty");
            }
        }

        if (IsSingleTerminalCompletionAnnouncementEnabled())
        {
            string text = RenderTemplate(_configManager.Config.TaskCompletionTtsTemplate, session);
            _ = _speechService.SpeakAsync(text, BuildSpeechOptions());
        }

        SimpleLogger.Log($"[TaskCompletion] Session '{session.Name}' completed via {source}");
    }

    private void FailSession(TerminalSession session, SessionMonitorState state, string failureKeyword, string source)
    {
        state.IsMonitoring = false;
        state.HasAnnounced = true;
        state.IsFailed = true;
        state.LastFailureKeyword = failureKeyword;
        SetWaitingForUserInputState(session, state, false, $"failure:{source}");

        if (state.IsAutoDraftQueueActive)
        {
            StopAutoDraftQueue(session, state, $"failure:{failureKeyword}");
        }

        if (IsSingleTerminalFailureAnnouncementEnabled())
        {
            string text = RenderTemplate(_configManager.Config.TaskFailureTtsTemplate, session, failureKeyword);
            _ = _speechService.SpeakAsync(text, BuildSpeechOptions());
        }

        SimpleLogger.Log($"[TaskFailure] Session '{session.Name}' failed via {source}. Keyword: {failureKeyword}");
    }

    private SessionMonitorState GetOrCreateState(TerminalSession session)
    {
        if (!_sessionStates.TryGetValue(session, out SessionMonitorState? state))
        {
            state = new SessionMonitorState();
            _sessionStates[session] = state;
            session.UserCommandSent += Session_UserCommandSent;
            session.UserInteractionOccurred += Session_UserInteractionOccurred;
        }

        return state;
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
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.Trim())
            .LastOrDefault(line => !string.IsNullOrWhiteSpace(line));

        if (string.IsNullOrWhiteSpace(lastNonEmptyLine))
        {
            return false;
        }

        return PromptMarkers.Any(marker => string.Equals(lastNonEmptyLine, marker, StringComparison.Ordinal));
    }

    private static string NormalizeSnapshot(string snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot))
        {
            return string.Empty;
        }

        List<string> lines = snapshot
            .Replace("\r\n", "\n")
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

    private static string RenderTemplate(string template, TerminalSession? session = null)
    {
        string terminalName = session?.Name ?? string.Empty;
        string terminalVoiceName = session?.TerminalVoiceName ?? string.Empty;
        string effectiveVoiceName = string.IsNullOrWhiteSpace(terminalVoiceName) ? terminalName : terminalVoiceName;

        return (template ?? string.Empty)
            .Replace("{TerminalVoiceName_or_TerminalName}", effectiveVoiceName, StringComparison.OrdinalIgnoreCase)
            .Replace("{TerminalName}", terminalName, StringComparison.OrdinalIgnoreCase);
    }

    private static string RenderTemplate(string template, TerminalSession? session, string failureKeyword)
    {
        return RenderTemplate(template, session)
            .Replace("{FailureKeyword}", failureKeyword ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private SpeechPlaybackOptions BuildSpeechOptions()
    {
        return new SpeechPlaybackOptions
        {
            VoiceName = _configManager.Config.TtsVoiceName,
            Rate = _configManager.Config.TtsRate,
            Volume = _configManager.Config.TtsVolume
        };
    }

    private bool SignalMatchesSession(AiCompletionSignal signal, TerminalSession session)
    {
        if (!string.IsNullOrWhiteSpace(signal.TerminalName) &&
            string.Equals(signal.TerminalName, session.Name, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(signal.WorkingDirectory) &&
            PathsMatch(signal.WorkingDirectory, session.WorkingDirectory))
        {
            return true;
        }

        return false;
    }

    private static bool PathsMatch(string left, string right)
    {
        try
        {
            string normalizedLeft = Path.GetFullPath(left).TrimEnd('\\', '/');
            string normalizedRight = Path.GetFullPath(right).TrimEnd('\\', '/');
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left.TrimEnd('\\', '/'), right.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool ShouldShowAllIdleWindow()
    {
        return IsAnyAllIdleAlertEnabled();
    }

    private bool IsSingleTerminalCompletionAnnouncementEnabled()
    {
        return _configManager.Config.EnableAllTaskAlerts
            && _configManager.Config.EnableTaskCompletionTts;
    }

    private bool IsSingleTerminalFailureAnnouncementEnabled()
    {
        return _configManager.Config.EnableAllTaskAlerts
            && _configManager.Config.EnableTaskFailureTts;
    }

    private bool ShouldMonitorSingleTerminalRounds()
    {
        return IsSingleTerminalCompletionAnnouncementEnabled()
            || IsSingleTerminalFailureAnnouncementEnabled();
    }

    private bool IsAnyAllIdleAlertEnabled()
    {
        return _configManager.Config.EnableAllTaskAlerts
            && (_configManager.Config.EnableAllTerminalsIdleVoiceAlert
                || _configManager.Config.EnableAllTerminalsIdlePopupAlert);
    }

    private bool IsAllIdleVoiceEnabled()
    {
        return _configManager.Config.EnableAllTaskAlerts
            && _configManager.Config.EnableAllTerminalsIdleVoiceAlert;
    }

    public IReadOnlyList<ShutdownGuardTerminalActivity> GetShutdownGuardActivities()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        TimeSpan submittedGracePeriod = TimeSpan.FromSeconds(Math.Max(30, _configManager.Config.TaskCompletionCheckIntervalSeconds));
        TimeSpan stableThreshold = TimeSpan.FromMinutes(Math.Clamp(_configManager.Config.TaskCompletionStableThresholdMinutes, 1, 1440));
        List<ShutdownGuardTerminalActivity> activities = new();

        foreach ((TerminalSession session, SessionMonitorState state) in _sessionStates)
        {
            if (!_idleSnapshotStates.TryGetValue(session.Name, out IdleSnapshotState? idleState))
            {
                continue;
            }

            bool isProcessAlive = false;
            try
            {
                isProcessAlive = session.AppProcess != null && !session.AppProcess.HasExited;
            }
            catch
            {
                isProcessAlive = false;
            }

            ShutdownGuardTerminalStatus? status = ShutdownGuardEvaluator.EvaluateStatus(
                isProcessAlive,
                state.IsFailed,
                state.IsWaitingForUserInput || session.IsAutoDraftQueueWaitingForUserInput,
                state.HasSeenBusyState,
                state.LastCommandUtc,
                idleState.LastChangedUtc,
                idleState.LastSnapshot,
                now,
                submittedGracePeriod,
                stableThreshold);

            if (status.HasValue)
            {
                activities.Add(new ShutdownGuardTerminalActivity(session.Name, status.Value));
            }
        }

        return activities;
    }

    private void DismissAllIdleWindow()
    {
        if (_allIdleAlertWindow == null)
        {
            return;
        }

        _allIdleAlertWindow.AlertDismissed -= AllIdleAlertWindow_AlertDismissed;
        _allIdleAlertWindow.Close();
        _allIdleAlertWindow = null;
    }

    private sealed class SessionMonitorState
    {
        public bool IsMonitoring { get; set; }
        public bool HasSeenBusyState { get; set; }
        public bool HasAnnounced { get; set; }
        public bool IsFailed { get; set; }
        public bool IsAutoDraftQueueActive { get; set; }
        public bool IsWaitingForUserInput { get; set; }
        public long RoundId { get; set; }
        public DateTimeOffset? LastCommandUtc { get; set; }
        public string LastCommand { get; set; } = string.Empty;
        public string LastFailureKeyword { get; set; } = string.Empty;
    }

    private sealed class IdleSnapshotState
    {
        public string LastSnapshot { get; set; } = string.Empty;
        public DateTimeOffset LastChangedUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    private void RefreshAutoDraftQueueState(TerminalSession session, SessionMonitorState state, string reason)
    {
        bool shouldActivateAutoQueue = ShouldActivateAutoDraftQueue(session, state);
        if (shouldActivateAutoQueue)
        {
            bool wasActive = state.IsAutoDraftQueueActive || session.IsAutoDraftQueueActive;
            state.IsAutoDraftQueueActive = true;
            state.IsMonitoring = true;
            ApplySessionVisualState(session, state);

            if (!wasActive)
            {
                SimpleLogger.Log($"[AutoDraftQueue] Session '{session.Name}' armed via {reason}");
            }

            return;
        }

        if (state.IsAutoDraftQueueActive || session.IsAutoDraftQueueActive)
        {
            StopAutoDraftQueue(session, state, reason);
            return;
        }

        ApplySessionVisualState(session, state);
        state.IsMonitoring = ShouldMonitorSingleTerminalRounds() && !state.HasAnnounced && !state.IsFailed;
    }

    private static bool ShouldActivateAutoDraftQueue(TerminalSession session, SessionMonitorState state)
    {
        return session.IsAutoDraftQueueEnabled
            && session.HasInputDrafts
            && !state.IsFailed
            && !state.HasAnnounced
            && !string.IsNullOrWhiteSpace(state.LastCommand);
    }

    private bool UpdateWaitingForUserInputState(TerminalSession session, SessionMonitorState state, string snapshot, string reason)
    {
        bool isWaitingForUserInput = _enableWaitForUserInputGuard
            && UserInputWaitGuard.IsMatch(snapshot, _waitForUserInputKeywords);

        SetWaitingForUserInputState(session, state, isWaitingForUserInput, reason);
        return isWaitingForUserInput;
    }

    private void SetWaitingForUserInputState(TerminalSession session, SessionMonitorState state, bool isWaitingForUserInput, string reason)
    {
        bool oldValue = state.IsWaitingForUserInput;
        state.IsWaitingForUserInput = isWaitingForUserInput;
        ApplySessionVisualState(session, state);

        if (oldValue == isWaitingForUserInput)
        {
            return;
        }

        string action = isWaitingForUserInput ? "paused for user input" : "resumed from user input wait";
        SimpleLogger.Log($"[AutoDraftQueue] Session '{session.Name}' {action} via {reason}");
    }

    private static void ApplySessionVisualState(TerminalSession session, SessionMonitorState state)
    {
        session.IsWaitingForUserInput = state.IsWaitingForUserInput;
        session.IsAutoDraftQueueActive = state.IsAutoDraftQueueActive;
        session.IsAutoDraftQueueWaitingForUserInput = state.IsWaitingForUserInput && state.IsAutoDraftQueueActive;
    }

    private void StopAutoDraftQueue(TerminalSession session, SessionMonitorState state, string reason)
    {
        if (!state.IsAutoDraftQueueActive && !session.IsAutoDraftQueueActive)
        {
            return;
        }

        state.IsAutoDraftQueueActive = false;
        ApplySessionVisualState(session, state);
        state.IsMonitoring = ShouldMonitorSingleTerminalRounds() && !state.HasAnnounced && !state.IsFailed;
        SimpleLogger.Log($"[AutoDraftQueue] Session '{session.Name}' stopped via {reason}");
    }

    private bool UpdateFailureState(TerminalSession session, SessionMonitorState state, string snapshot, string reason)
    {
        if (state.IsFailed)
        {
            return true;
        }

        if (!TaskFailureGuard.TryMatch(snapshot, _failureKeywords, out string matchedKeyword))
        {
            return false;
        }

        bool shouldAnnounceFailure = state.IsMonitoring
            && !state.HasAnnounced
            && (state.HasSeenBusyState
                || state.IsAutoDraftQueueActive
                || !string.IsNullOrWhiteSpace(state.LastCommand));

        state.IsFailed = true;
        state.LastFailureKeyword = matchedKeyword;

        if (shouldAnnounceFailure)
        {
            FailSession(session, state, matchedKeyword, reason);
        }
        else
        {
            SetWaitingForUserInputState(session, state, false, $"failure:{reason}");
            if (state.IsAutoDraftQueueActive)
            {
                StopAutoDraftQueue(session, state, $"failure:{matchedKeyword}");
            }
            else
            {
                state.IsMonitoring = false;
            }

            SimpleLogger.Log($"[TaskFailure] Session '{session.Name}' marked failed via {reason}. Keyword: {matchedKeyword}");
        }

        return true;
    }
}
