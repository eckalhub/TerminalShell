using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using TerminalShell.Core;

namespace TerminalShell.Controls;

internal static class ConsoleTopologyTransitionCoordinator
{
    private static readonly object SyncRoot = new();
    private static readonly HashSet<ConsoleHost> RegisteredHosts = [];
    private static DispatcherTimer? _releaseTimer;
    private static int _generationCounter;
    private static int _activeGeneration;
    private static string _activeReason = string.Empty;
    private static string _pendingReleaseReason = string.Empty;

    private static readonly TimeSpan ReleaseDelay = TimeSpan.FromMilliseconds(220);

    public static void Register(ConsoleHost host)
    {
        if (host == null)
        {
            return;
        }

        if (!host.Dispatcher.CheckAccess())
        {
            _ = host.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => Register(host)));
            return;
        }

        int activeGeneration;
        string activeReason;

        lock (SyncRoot)
        {
            RegisteredHosts.Add(host);
            EnsureReleaseTimer(host.Dispatcher);
            activeGeneration = _activeGeneration;
            activeReason = _activeReason;
        }

        if (activeGeneration > 0)
        {
            host.EnterTopologyTransitionHold(activeGeneration, activeReason);
            return;
        }

        host.ClearStaleTopologyTransitionHold();
    }

    public static void Unregister(ConsoleHost host)
    {
        if (host == null)
        {
            return;
        }

        if (!host.Dispatcher.CheckAccess())
        {
            _ = host.Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => Unregister(host)));
            return;
        }

        lock (SyncRoot)
        {
            RegisteredHosts.Remove(host);
        }
    }

    public static void BeginTransition(string reason)
    {
        Dispatcher dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        if (!dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => BeginTransition(reason)));
            return;
        }

        List<ConsoleHost> hosts;
        int generation;
        string normalizedReason = string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason.Trim();

        lock (SyncRoot)
        {
            EnsureReleaseTimer(dispatcher);
            _releaseTimer?.Stop();
            generation = ++_generationCounter;
            _activeGeneration = generation;
            _activeReason = normalizedReason;
            _pendingReleaseReason = normalizedReason;
            hosts = RegisteredHosts.ToList();
        }

        SimpleLogger.Log($"[TOPOLOGY] Begin Generation={generation} Hosts={hosts.Count} Reason={normalizedReason}");
        foreach (ConsoleHost host in hosts)
        {
            host.EnterTopologyTransitionHold(generation, normalizedReason);
        }
    }

    public static void ScheduleRelease(string reason)
    {
        Dispatcher dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        if (!dispatcher.CheckAccess())
        {
            _ = dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => ScheduleRelease(reason)));
            return;
        }

        int generation;
        string normalizedReason = string.IsNullOrWhiteSpace(reason) ? "Unknown" : reason.Trim();

        lock (SyncRoot)
        {
            if (_activeGeneration == 0)
            {
                return;
            }

            EnsureReleaseTimer(dispatcher);
            generation = _activeGeneration;
            _pendingReleaseReason = normalizedReason;
            _releaseTimer?.Stop();
            _releaseTimer?.Start();
        }

        SimpleLogger.Log($"[TOPOLOGY] ReleaseScheduled Generation={generation} DelayMs={ReleaseDelay.TotalMilliseconds:0} Reason={normalizedReason}");
    }

    private static void EnsureReleaseTimer(Dispatcher dispatcher)
    {
        if (_releaseTimer != null)
        {
            return;
        }

        _releaseTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = ReleaseDelay
        };
        _releaseTimer.Tick += ReleaseTimer_Tick;
    }

    private static void ReleaseTimer_Tick(object? sender, EventArgs e)
    {
        _releaseTimer?.Stop();

        List<ConsoleHost> hosts;
        int generation;
        string reason;

        lock (SyncRoot)
        {
            generation = _activeGeneration;
            if (generation == 0)
            {
                return;
            }

            reason = _pendingReleaseReason;
            hosts = RegisteredHosts.ToList();
            _activeGeneration = 0;
            _activeReason = string.Empty;
            _pendingReleaseReason = string.Empty;
        }

        SimpleLogger.Log($"[TOPOLOGY] ReleaseBegin Generation={generation} Hosts={hosts.Count} Reason={reason}");
        foreach (ConsoleHost host in hosts)
        {
            host.ReleaseTopologyTransition(generation, reason);
        }
    }
}
