using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;

namespace TerminalShell.Services;

public sealed class AiCompletionSignal
{
    public string Source { get; init; } = string.Empty;
    public string EventName { get; init; } = string.Empty;
    public string TerminalName { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
    public string RawJson { get; init; } = string.Empty;
}

public sealed class AiCompletionInboxService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, byte> _pendingFiles = new(StringComparer.OrdinalIgnoreCase);

    public string InboxDirectory { get; }

    public event Action<AiCompletionSignal>? CompletionSignalReceived;

    public AiCompletionInboxService()
    {
        InboxDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ai_completion_events");
        Directory.CreateDirectory(InboxDirectory);

        _watcher = new FileSystemWatcher(InboxDirectory, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite
        };
        _watcher.Created += OnFileChanged;
        _watcher.Changed += OnFileChanged;
        _watcher.EnableRaisingEvents = true;

        foreach (string filePath in Directory.GetFiles(InboxDirectory, "*.json"))
        {
            _ = ProcessFileAsync(filePath);
        }
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnFileChanged;
        _watcher.Changed -= OnFileChanged;
        _watcher.Dispose();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _ = ProcessFileAsync(e.FullPath);
    }

    private async Task ProcessFileAsync(string filePath)
    {
        if (!_pendingFiles.TryAdd(filePath, 0))
        {
            return;
        }

        try
        {
            string? json = await ReadFileWithRetriesAsync(filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            AiCompletionSignal? signal = TryParseSignal(json);
            if (signal != null)
            {
                CompletionSignalReceived?.Invoke(signal);
            }

            TryDelete(filePath);
        }
        finally
        {
            _pendingFiles.TryRemove(filePath, out _);
        }
    }

    private static async Task<string?> ReadFileWithRetriesAsync(string filePath)
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return null;
                }

                return await File.ReadAllTextAsync(filePath);
            }
            catch (IOException)
            {
                await Task.Delay(100);
            }
            catch (UnauthorizedAccessException)
            {
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Core.SimpleLogger.LogError(ex, $"AiCompletionInboxService.ReadFileWithRetriesAsync ({filePath})");
                break;
            }
        }

        return null;
    }

    private static AiCompletionSignal? TryParseSignal(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            string source = GetString(root, "source");
            if (string.IsNullOrWhiteSpace(source))
            {
                source = GetString(root, "client");
            }

            string eventName = GetString(root, "event");
            if (string.IsNullOrWhiteSpace(eventName))
            {
                eventName = GetString(root, "eventName");
            }
            if (string.IsNullOrWhiteSpace(eventName))
            {
                eventName = GetString(root, "type");
            }

            source = source.Trim().ToLowerInvariant();
            eventName = string.IsNullOrWhiteSpace(eventName) ? "completed" : eventName.Trim().ToLowerInvariant();

            if (!IsCompletionEvent(source, eventName))
            {
                return null;
            }

            return new AiCompletionSignal
            {
                Source = source,
                EventName = eventName,
                TerminalName = GetString(root, "terminalName"),
                WorkingDirectory = GetString(root, "workingDirectory", "cwd"),
                RawJson = json
            };
        }
        catch (Exception ex)
        {
            Core.SimpleLogger.LogError(ex, "AiCompletionInboxService.TryParseSignal");
            return null;
        }
    }

    private static string GetString(JsonElement root, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            if (root.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static bool IsCompletionEvent(string source, string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return false;
        }

        return source switch
        {
            "claude" => eventName.Contains("stop", StringComparison.OrdinalIgnoreCase)
                || eventName.Contains("complete", StringComparison.OrdinalIgnoreCase),
            "codex" => eventName.Contains("notify", StringComparison.OrdinalIgnoreCase)
                || eventName.Contains("complete", StringComparison.OrdinalIgnoreCase),
            _ => eventName.Contains("stop", StringComparison.OrdinalIgnoreCase)
                || eventName.Contains("notify", StringComparison.OrdinalIgnoreCase)
                || eventName.Contains("complete", StringComparison.OrdinalIgnoreCase)
        };
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            Core.SimpleLogger.LogError(ex, $"AiCompletionInboxService.TryDelete ({filePath})");
        }
    }
}
