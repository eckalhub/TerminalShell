using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using TerminalShell.Models;
using TerminalShell.Core;
using TerminalShell.Services;

namespace TerminalShell.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly TerminalSession _session;
    private readonly string _historyDirectory;

    [ObservableProperty]
    private ObservableCollection<HistoryFileItem> _historyFiles = new();

    [ObservableProperty]
    private HistoryFileItem? _selectedFile;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _fileContent = string.Empty;

    public Action? CloseAction { get; set; }

    public HistoryViewModel(TerminalSession session)
    {
        _session = session;
        _historyDirectory = HistoryService.BuildHistoryDirectory(session.TerminalHistorySaveFolder, session.Name);
        
        LoadHistoryFiles();
    }

    private void LoadHistoryFiles()
    {
        HistoryFiles.Clear();
        FileContent = string.Empty;

        if (!Directory.Exists(_historyDirectory))
        {
            return;
        }

        try
        {
            var directoryInfo = new DirectoryInfo(_historyDirectory);
            var files = directoryInfo.GetFiles("*.txt")
                                     .OrderByDescending(f => f.LastWriteTime)
                                     .ToList();

            foreach (var file in files)
            {
                // Simple filter based on SearchQuery
                // We could search filename or content. Let's do filename first for speed.
                // If the user wants content search, it will require reading all files.
                // For a robust search, we will search filename and optionally content if filename doesn't match.
                
                bool matchesSearch = string.IsNullOrWhiteSpace(SearchQuery) || 
                                     file.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);

                if (!matchesSearch)
                {
                    // Fallback to content search (Caution: Can be slow for 1000s of files, but necessary for "content-based searching")
                    try
                    {
                        var content = File.ReadAllText(file.FullName);
                        if (content.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                        {
                            matchesSearch = true;
                        }
                    }
                    catch { }
                }

                if (matchesSearch)
                {
                    HistoryFiles.Add(new HistoryFileItem
                    {
                        FileName = file.Name,
                        FilePath = file.FullName,
                        LastWriteTime = file.LastWriteTime
                    });
                }
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "HistoryViewModel.LoadHistoryFiles");
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        LoadHistoryFiles();
    }

    partial void OnSelectedFileChanged(HistoryFileItem? value)
    {
        if (value != null && File.Exists(value.FilePath))
        {
            try
            {
                FileContent = File.ReadAllText(value.FilePath);
            }
            catch (Exception ex)
            {
                FileContent = $"Error reading file: {ex.Message}";
                SimpleLogger.LogError(ex, $"HistoryViewModel.OnSelectedFileChanged ({value.FileName})");
            }
        }
        else
        {
            FileContent = string.Empty;
        }
    }

    [RelayCommand]
    private void RestoreHistory()
    {
        if (SelectedFile != null && !string.IsNullOrEmpty(FileContent))
        {
            // Append content to existing input buffer
            if (string.IsNullOrEmpty(_session.InputBuffer))
            {
                _session.InputBuffer = FileContent;
            }
            else
            {
                // Ensure it's on a new line or separated and append
                _session.InputBuffer += "\r\n" + FileContent;
            }

            // Close Window
            CloseAction?.Invoke();
        }
    }
}

public class HistoryFileItem
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime LastWriteTime { get; set; }
}
