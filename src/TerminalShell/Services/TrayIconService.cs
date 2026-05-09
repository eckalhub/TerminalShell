using System;
using System.Drawing;
using System.Windows.Forms;
using TerminalShell.Core;

namespace TerminalShell.Services
{
    public interface ITrayIconService : IDisposable
    {
        void Initialize();
        event Action OpenSettingsRequested;
        event Action OpenSettingsWindowRequested;
        event Action RestartRequested;
        event Action ExitRequested;
        event Action? ToggleRequested;
    }

    public class TrayIconService : ITrayIconService
    {
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;

        public event Action? OpenSettingsRequested;
        public event Action? OpenSettingsWindowRequested;
        public event Action? RestartRequested;
        public event Action? ExitRequested;

        public void Initialize()
        {
            if (_notifyIcon != null) return;

            _contextMenu = new ContextMenuStrip();
            var openItem = new ToolStripMenuItem("Open Main Window");
            openItem.Click += (s, e) => OpenSettingsRequested?.Invoke();
            openItem.Font = new Font(openItem.Font, FontStyle.Bold); // Make Open bold

            var settingsItem = new ToolStripMenuItem("Settings");
            settingsItem.Click += (s, e) => OpenSettingsWindowRequested?.Invoke();

            var restartItem = new ToolStripMenuItem(RuntimeAppIdentity.TrayRestartMenuText);
            restartItem.Click += (s, e) => RestartRequested?.Invoke();

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitRequested?.Invoke();

            _contextMenu.Items.Add(openItem);
            _contextMenu.Items.Add(settingsItem);
            _contextMenu.Items.Add(restartItem);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon
            {
                Icon = RuntimeAppIdentity.CreateTrayIcon(),
                Text = RuntimeAppIdentity.TrayTooltipText,
                ContextMenuStrip = _contextMenu
            };
            _notifyIcon.Visible = true;

            _notifyIcon.MouseDoubleClick += (s, e) => 
            {
                ToggleRequested?.Invoke();
            };
        }

        // Extra event for Toggle per spec
        public event Action? ToggleRequested;

        public void Dispose()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false; // Validates removal from tray immediately
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            _contextMenu?.Dispose();
        }
    }
}
