using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TerminalShell.Core;
using TerminalShell.Core.Config;

namespace TerminalShell.Services;

public class HistoryService
{
    private static readonly string HistoryDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history");

    public static void Save(string content, string terminalName)
    {
        Save(content, string.Empty, terminalName);
    }

    public static void Save(string content, string terminalHistorySaveFolder, string terminalName)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        // Fire and forget to avoid blocking the caller (shell input)
        Task.Run(() => PerformSaveAndCleanup(content, terminalHistorySaveFolder, terminalName));
    }

    private static void PerformSaveAndCleanup(string content, string terminalHistorySaveFolder, string terminalName)
    {
        string targetDirectory = BuildHistoryDirectory(terminalHistorySaveFolder, terminalName);
        EnsureDirectoryExists(targetDirectory);

        try
        {
            var config = ConfigManager.Instance.Config;
            
            // Calculate time based on timezone offset
            DateTime currentTime = DateTime.UtcNow.AddHours(config.TimeZoneOffset);
            string timeStamp = currentTime.ToString("yyyy-MM-dd_HHmmss");

            // Extract and clean prefix
            string prefix = ExtractCleanPrefix(content, config.HistoryFileNameMaxLength);

            string fileName = $"{timeStamp}{prefix}.txt";
            string filePath = Path.Combine(targetDirectory, fileName);

            try
            {
                File.WriteAllText(filePath, content);
            }
            catch (Exception ex)
            {
                // Fallback: If the constructed name still causes IO issues, use pure timestamp
                SimpleLogger.LogError(ex, $"HistoryService.Save fallback for {fileName}");
                string fallbackPath = Path.Combine(targetDirectory, $"{timeStamp}.txt");
                File.WriteAllText(fallbackPath, content);
            }

            CleanupOldHistories(targetDirectory, config.HistoryRetentionCount);
        }
        catch (Exception ex)
        {
            // Absolute catch-all firewall to prevent crashes from propagating
            SimpleLogger.LogError(ex, "HistoryService.PerformSaveAndCleanup fatal error");
        }
    }

    public static string BuildHistoryDirectory(string terminalHistorySaveFolder, string terminalName)
    {
        return Path.Combine(HistoryDirectory, GetSafeHistoryFolderName(terminalHistorySaveFolder, terminalName));
    }

    public static string GetSafeHistoryFolderName(string terminalHistorySaveFolder, string terminalName)
    {
        string effectiveFolder = string.IsNullOrWhiteSpace(terminalHistorySaveFolder) ? terminalName : terminalHistorySaveFolder;
        return string.IsNullOrWhiteSpace(effectiveFolder) ? "Default" : CleanDirectoryName(effectiveFolder);
    }

    private static string CleanDirectoryName(string name)
    {
        string pattern = @"[\\\/\:\*\?\""\<\>\|\r\n]";
        return Regex.Replace(name, pattern, "_");
    }

    private static string ExtractCleanPrefix(string content, int maxLength)
    {
        // 1. Trim length
        string prefix = content.Length <= maxLength ? content : content.Substring(0, maxLength);

        // 2. Remove problematic chars 
        // Windows invalid chars: \ / : * ? " < > |
        // Plus newlines \r \n
        string pattern = @"[\\\/\:\*\?\""\<\>\|\r\n]";
        prefix = Regex.Replace(prefix, pattern, "_");

        return prefix;
    }

    private static void EnsureDirectoryExists(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }
    }

    private static void CleanupOldHistories(string directoryPath, int maxCount)
    {
        try
        {
            if (maxCount <= 0) return; // 0 or negative could mean unlimited, but let's assume it should bound it. If 0 actually means disabled, we respect it. Let's assume > 0 means bound.

            var directoryInfo = new DirectoryInfo(directoryPath);
            if (!directoryInfo.Exists) return;

            var files = directoryInfo.GetFiles("*.txt")
                                     .OrderByDescending(f => f.LastWriteTime)
                                     .ToList();

            if (files.Count > maxCount)
            {
                var filesToDelete = files.Skip(maxCount);
                foreach (var file in filesToDelete)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.LogError(ex, $"HistoryService Cleanup failed to delete {file.Name}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "HistoryService.CleanupOldHistories");
        }
    }
}
