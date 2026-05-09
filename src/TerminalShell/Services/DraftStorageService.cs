using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using TerminalShell.Core;
using TerminalShell.Models;

namespace TerminalShell.Services;

public interface IDraftStorageService
{
    IReadOnlyList<TerminalDraft> GetDrafts(string draftStorageKey);
    TerminalDraft? SaveDraft(string draftStorageKey, string text);
    bool DeleteDraft(string draftStorageKey, string draftId);
    bool MoveDraft(string draftStorageKey, string draftId, int offset);
}

public sealed class DraftStorageService : IDraftStorageService
{
    private readonly object _syncRoot = new();
    private readonly string _draftConfigPath;
    private DraftStorageDocument _document = new();

    public DraftStorageService()
    {
        _draftConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config_draft.json");
        Load();
    }

    public IReadOnlyList<TerminalDraft> GetDrafts(string draftStorageKey)
    {
        if (string.IsNullOrWhiteSpace(draftStorageKey))
        {
            return Array.Empty<TerminalDraft>();
        }

        lock (_syncRoot)
        {
            if (!_document.DraftsByTerminal.TryGetValue(draftStorageKey, out List<TerminalDraft>? drafts))
            {
                return Array.Empty<TerminalDraft>();
            }

            return drafts
                .Select(draft => draft.Clone())
                .ToList();
        }
    }

    public TerminalDraft? SaveDraft(string draftStorageKey, string text)
    {
        if (string.IsNullOrWhiteSpace(draftStorageKey) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        lock (_syncRoot)
        {
            if (!_document.DraftsByTerminal.TryGetValue(draftStorageKey, out List<TerminalDraft>? drafts))
            {
                drafts = new List<TerminalDraft>();
                _document.DraftsByTerminal[draftStorageKey] = drafts;
            }

            TerminalDraft draft = new()
            {
                Id = Guid.NewGuid().ToString("N"),
                Text = text,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            drafts.Add(draft);
            if (!TrySaveLocked())
            {
                drafts.RemoveAll(item => string.Equals(item.Id, draft.Id, StringComparison.Ordinal));
                if (drafts.Count == 0)
                {
                    _document.DraftsByTerminal.Remove(draftStorageKey);
                }

                return null;
            }

            return draft.Clone();
        }
    }

    public bool DeleteDraft(string draftStorageKey, string draftId)
    {
        if (string.IsNullOrWhiteSpace(draftStorageKey) || string.IsNullOrWhiteSpace(draftId))
        {
            return false;
        }

        lock (_syncRoot)
        {
            if (!_document.DraftsByTerminal.TryGetValue(draftStorageKey, out List<TerminalDraft>? drafts))
            {
                return false;
            }

            int removedCount = drafts.RemoveAll(draft => string.Equals(draft.Id, draftId, StringComparison.Ordinal));
            if (removedCount == 0)
            {
                return false;
            }

            bool removeTerminalBucket = drafts.Count == 0;
            if (removeTerminalBucket)
            {
                _document.DraftsByTerminal.Remove(draftStorageKey);
            }

            if (TrySaveLocked())
            {
                return true;
            }

            Load();
            return false;
        }
    }

    public bool MoveDraft(string draftStorageKey, string draftId, int offset)
    {
        if (string.IsNullOrWhiteSpace(draftStorageKey) || string.IsNullOrWhiteSpace(draftId) || offset == 0)
        {
            return false;
        }

        lock (_syncRoot)
        {
            if (!_document.DraftsByTerminal.TryGetValue(draftStorageKey, out List<TerminalDraft>? drafts))
            {
                return false;
            }

            int currentIndex = drafts.FindIndex(draft => string.Equals(draft.Id, draftId, StringComparison.Ordinal));
            if (currentIndex < 0)
            {
                return false;
            }

            int targetIndex = Math.Clamp(currentIndex + offset, 0, drafts.Count - 1);
            if (targetIndex == currentIndex)
            {
                return false;
            }

            TerminalDraft targetDraft = drafts[currentIndex];
            drafts.RemoveAt(currentIndex);
            drafts.Insert(targetIndex, targetDraft);

            if (TrySaveLocked())
            {
                return true;
            }

            Load();
            return false;
        }
    }

    private void Load()
    {
        lock (_syncRoot)
        {
            try
            {
                if (!File.Exists(_draftConfigPath))
                {
                    _document = new DraftStorageDocument();
                    return;
                }

                string json = File.ReadAllText(_draftConfigPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    _document = new DraftStorageDocument();
                    return;
                }

                DraftStorageDocument? document = JsonSerializer.Deserialize<DraftStorageDocument>(json);
                _document = document ?? new DraftStorageDocument();
            }
            catch (Exception ex)
            {
                _document = new DraftStorageDocument();
                SimpleLogger.LogError(ex, "DraftStorageService.Load");
            }
        }
    }

    private bool TrySaveLocked()
    {
        try
        {
            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string json = JsonSerializer.Serialize(_document, options);
            File.WriteAllText(_draftConfigPath, json);
            return true;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "DraftStorageService.TrySaveLocked");
            return false;
        }
    }

    private sealed class DraftStorageDocument
    {
        public Dictionary<string, List<TerminalDraft>> DraftsByTerminal { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
