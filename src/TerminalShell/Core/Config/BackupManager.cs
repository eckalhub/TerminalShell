using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TerminalShell.Core.Config
{
    // Skill: config_json_style (Auto-Backup)
    public class BackupManager
    {
        private readonly string _configPath;
        private readonly string _backupDir;
        private string _lastContentHash = string.Empty;
        private DateTime _lastBackupTime = DateTime.MinValue;
        
        public BackupManager(string configPath)
        {
            _configPath = configPath;
            _backupDir = Path.Combine(Path.GetDirectoryName(configPath) ?? string.Empty, "config_bak");
            
            if (!Directory.Exists(_backupDir))
            {
                Directory.CreateDirectory(_backupDir);
            }
        }

        public void CheckAndBackup(string content, int maxRetained, int intervalMinutes)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content)) return;

                string currentHash = ComputeHash(content);

                // Only backup if content changed effectively
                if (currentHash != _lastContentHash)
                {
                    // Time-based throttle: skip if interval hasn't elapsed since last backup
                    if ((DateTime.Now - _lastBackupTime).TotalMinutes < intervalMinutes)
                    {
                        return;
                    }

                    PerformBackup(content);
                    _lastContentHash = currentHash;
                    _lastBackupTime = DateTime.Now;
                    
                    // Async cleanup to avoid blocking
                    Task.Run(() => CleanupOldBackups(maxRetained));
                }
            }
            catch (Exception ex)
            {
                // Silent fail for backup logic - never crash main app
                SimpleLogger.LogError(ex, "BackupManager.CheckAndBackup");
            }
        }

        private void PerformBackup(string content)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(_backupDir, $"config.json@{timestamp}.bak");
                
                File.WriteAllText(backupPath, content);
                // SimpleLogger.Log($"Config backed up to: {backupPath}");
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "BackupManager.PerformBackup");
            }
        }

        private void CleanupOldBackups(int maxRetained)
        {
            try
            {
                var files = Directory.GetFiles(_backupDir, "config.json@*.bak")
                                     .OrderByDescending(f => f) // Timestamp sort desc
                                     .ToList();

                if (files.Count > maxRetained)
                {
                    var toDelete = files.Skip(maxRetained);
                    foreach (var file in toDelete)
                    {
                        try
                        {
                            File.Delete(file);
                        }
                        catch { /* Ignore singular delete fail */ }
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "BackupManager.CleanupOldBackups");
            }
        }

        private string ComputeHash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
