# TerminalShell User Guide

<p align="center">
  <a href="https://www.microsoft.com/windows"><img alt="Windows" src="https://img.shields.io/badge/platform-Windows-0078D4"></a>
  <a href="https://dotnet.microsoft.com/"><img alt=".NET 8" src="https://img.shields.io/badge/.NET-8.0-512BD4"></a>
  <a href="https://learn.microsoft.com/dotnet/desktop/wpf/"><img alt="WPF" src="https://img.shields.io/badge/UI-WPF-5C2D91"></a>
  <a href="https://github.com/eckalhub/TerminalShell/blob/main/LICENSE"><img alt="License: MIT" src="https://img.shields.io/badge/License-MIT-yellow.svg"></a>
  <a href="https://github.com/eckalhub/TerminalShell/releases"><img alt="Release" src="https://img.shields.io/github/v/release/eckalhub/TerminalShell?label=release"></a>
</p>

TerminalShell is a Windows multi-terminal host built with `.NET 8` and WPF. It embeds multiple native `cmd.exe`, `powershell.exe`, and [`pwsh.exe`](https://github.com/PowerShell/PowerShell) (PowerShell) console windows into one desktop application, then adds workflow features for heavy command-line users, AI CLI sessions, remote viewing, and queued task handoff. It is especially suited for AI CLI terminals such as Claude Code, Codex CLI, and Gemini CLI. TerminalShell does not change the core workflow of these tools; it preserves their original behavior while adding extra capabilities around them, making it an enhanced terminal layer for AI CLI workflows. It is a strong fit for CLI power users and an ideal choice for managing many terminal sessions at once.

## Key Features

- **Multi-terminal workspace**: Display several terminals in one main window, organize them into folders, reorder them by drag and drop, show or hide sessions, restart one terminal, or maximize one terminal temporarily.
- **Native console embedding**: Starts, captures, and hosts real Windows console windows through Win32 APIs, preserving shell behavior while reducing window switching.
- **Reliable input sending**: The input area supports multiline text, single-enter or double-enter submit modes, `Shift+Enter` for newline, and configurable send delay for interactive TUIs such as Codex, Claude Code, and Gemini CLI.
- **Custom command system**: Use `/` for global command search, or press `F1` through `F12` to open categorized command blocks. Commands can include the `[return]` macro to insert real line breaks.
- **History and drafts**: Commands are saved per terminal. History can be searched, previewed, and restored. Input can be saved as drafts, and draft queues can automatically continue after a task completes.
- **Clipboard conversion**: Pasting images, HTML, RTF, or file lists can automatically produce plain text or Markdown. Images are saved to a configured folder and inserted through a template.
- **Task alerts**: TerminalShell can detect busy, completed, failed, and waiting-for-user-input states from terminal output. It supports per-terminal completion/failure speech alerts and all-terminals-idle speech or popup alerts.
- **Remote Web Console**: Built-in HTTP web console at `/remote/` for viewing terminal lists, live tail output, sending commands, sending drafts, and toggling automatic draft queues.
- **System integration**: Single-instance guard, system tray, tray restore/settings/restart/exit, startup launch, minimize-to-tray, and close-main-window-to-tray behavior.
- **Theme customization**: Main-window colors, input font, watermark, buttons, menus, and command popup colors can be configured from Settings.

## Use Cases

TerminalShell is designed for Windows users who keep many command-line jobs running for a long time, for example:

- Running several project dev servers, build commands, and logs at the same time.
- Keeping multiple AI CLI sessions in one stable workspace.
- Checking terminal status from a phone or another device and sending short remote commands.
- Saving frequently used commands, long prompts, or multiline scripts for quick reuse.
- Automatically sending the next draft after the previous task finishes.

## Quick Start

### Requirements

- Windows desktop environment.
- `.NET 8 SDK` for development and building.
- `.NET 8 Desktop Runtime` on the target machine for framework-dependent releases.
- PowerShell 7 is recommended. If [`pwsh.exe`](https://github.com/PowerShell/PowerShell) (PowerShell) is unavailable, TerminalShell falls back to Windows PowerShell or `cmd.exe`.

### Run From Source

```powershell
dotnet restore src\TerminalShell\TerminalShell.csproj
dotnet build src\TerminalShell\TerminalShell.csproj -c Release
dotnet run --project src\TerminalShell\TerminalShell.csproj
```

You can also open the solution directly in Visual Studio:

```text
src\TerminalShell.sln
```

### Single-File Publish

The project uses framework-dependent single-file publishing by default, so the .NET runtime is not bundled:

```powershell
dotnet restore src\TerminalShell\TerminalShell.csproj -r win-x64
dotnet publish src\TerminalShell\TerminalShell.csproj -c Release --no-restore -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugSymbols=true -p:DebugType=portable -o src\TerminalShell\bin\Release\net8.0-windows\win-x64\publish_single_file
```

Main publish outputs:

```text
src\TerminalShell\bin\Release\net8.0-windows\win-x64\publish_single_file\TerminalShell.exe
src\TerminalShell\bin\Release\net8.0-windows\win-x64\publish_single_file\TerminalShell.pdb
```

## Basic Usage

### Main Window

The main window is made of terminal panels. Each panel contains:

- Header: terminal name and context menu.
- Middle area: embedded native console window.
- Bottom input area: input, paste, save draft, send command, and open history.

Common terminal header context menu actions:

- `Settings`: open the settings window.
- `Show Terminal`: restore a hidden terminal.
- `Restart This Terminal`: restart the current terminal process.
- `Open Working Directory`: open the terminal's configured working directory.
- `Hide`: remove the terminal from the main window and close its process.
- `Toggle Fullscreen`: maximize or restore the current terminal.
- `Always on Top`: toggle topmost mode for the main window.
- `Allow sound reminders after tasks are completed`: toggle the global task alert switch.

### Input and Submit

The bottom input box is the command sending entry point. It supports wrapping, draggable height, and configurable font.

There are two submit modes:

- `SingleEnter`: press `Enter` to send the current input immediately.
- `DoubleEnter`: the first `Enter` inserts a newline, and a second quick `Enter` sends the input.

In both modes, `Shift+Enter` inserts a newline. You can also click the `Send` button.

Successfully sent manual commands are saved to the current terminal history. Startup commands are not written to history.

### History

Click the `History` button on the right side of the input area to open the history window for the current terminal.

History features:

- Each terminal can use an independent history folder.
- Files are shown newest first.
- Search supports file names and file content.
- The right pane previews the full command text.
- Double-clicking an item restores it into the original terminal input box.

History is stored under the application runtime directory:

```text
history\<terminal history folder>
```

### Drafts and Automatic Draft Queue

Use `Save To Draft` from the input box context menu to save the current input as a draft. Drafts appear at the bottom of the input area. You can click a draft to load it, or right-click it to move or delete it.

When `Enable Auto Draft Queue On Completion` is enabled for a terminal, TerminalShell automatically sends the next draft after the previous task completes. It waits for the terminal to become busy again, then waits for the next completion cycle. It does not send the whole queue at once.

If recent terminal output matches a waiting-for-user-input keyword, the automatic draft queue pauses to avoid sending follow-up commands while confirmation or selection is required.

## Custom Commands

Custom commands are managed in `Settings -> Custom Commands`.

Regular commands use one non-empty line per command. Type `/` in the terminal input box to open global search, keep typing to filter, use arrow keys to select, then press `Enter` or `Tab` to insert.

You can also define category blocks:

```text
[F1]
What is the current progress, and what remains?
Confirm, proceed with your recommendation.
[/F1]

[F2]
dotnet build src\TerminalShell\TerminalShell.csproj -c Release
[/F2]
```

Press `F1` through `F12` in the terminal input box to open the matching category popup. While a category popup is open, typed text only filters that category and does not alter the real input box.

Use `[return]` to insert a real line break in a multiline command:

```text
cd src[return]dotnet build
```

## Clipboard Conversion

When `Ctrl+V` is pressed in the input box, TerminalShell detects clipboard content:

- Plain text: use the normal system paste behavior.
- Images: save the image to the configured directory and insert a formatted image path.
- HTML / RTF: clean and convert to plain text or Markdown.
- File lists: convert to file path text.

The output format can be switched from the input box context menu:

- `Paste Output: Markdown`
- `Paste Output: Raw Text`

It can also be configured in `Settings -> Global Settings -> Clipboard Conversion`:

- Enable or disable conversion.
- Output format.
- Image save directory.
- Image path output template.

Default image template:

```text
Image attached [{fullpath}{time}{-XXX}.{ext}]
```

## Task Alerts

TerminalShell periodically reads recent terminal output to infer task state.

Supported alert features:

- Per-terminal task completion TTS.
- Per-terminal task failure TTS.
- All-main-window-terminals idle speech alert.
- All-main-window-terminals idle popup alert.
- Failure keyword detection.
- Waiting-for-user-input keyword detection.

Configuration is available in `Settings -> Global Settings -> Task Completion Alerts`.

Configurable options include:

- Global alert switch.
- Scan interval.
- Stable tail threshold.
- Tail line count.
- Windows system voice, rate, and volume.
- Completion, failure, and all-idle TTS templates.
- Waiting-for-user-input keywords.
- Failure keywords.

Templates may use:

```text
{TerminalVoiceName_or_TerminalName}
{TerminalName}
{FailureKeyword}
```

## Remote Web Console

When enabled, TerminalShell starts a built-in HTTP web console.

Entry path:

```text
http://<machine-address>:<port>/remote/
```

Remote console capabilities:

- Password login.
- View the list of terminals currently shown in the main window.
- View live tail output for one terminal.
- Send commands.
- View, send, and delete drafts.
- Toggle automatic draft queue for the current terminal.
- Auto-refresh status through WebSocket.

Security design:

- The remote console will not start until an access password is configured.
- Only the password hash is stored in config; the plain password is not stored.
- Login attempt count and lockout duration are configurable.
- Cookies use locally verifiable signed tokens.

Current version notes:

- `HTTP` mode is available.
- `HTTPS` is reserved for a future version. The setting can be saved, but the HTTPS host will not start in the current build.
- The root path does not serve a page. Use `/remote/`.
- For cross-network access, use your own Tailscale, frp/frpc, SSH reverse tunnel, or similar setup.

## Settings

The settings window contains these main areas:

- `Global Settings`: window layout, input font, submit mode, default startup command, watermark, task alerts, remote console, history, clipboard, auto backup, startup launch, and tray behavior.
- `Custom Commands`: `/` and `F1-F12` shortcut command maintenance.
- `themes`: main-window theme colors.
- Folder and terminal configuration: terminal groups, terminal names, working directories, startup commands, shell type, history folder, notes, and visibility.

Terminal configuration fields:

- `Terminal Name`: display name.
- `Terminal Voice Name`: name used by speech alerts; falls back to terminal name when empty.
- `Message History Save to Folder`: history subfolder; falls back to terminal name when empty.
- `Shell Type`: `cmd.exe`, `powershell.exe`, or [`pwsh.exe`](https://github.com/PowerShell/PowerShell) (PowerShell).
- `Working Directory`: startup working directory for the terminal.
- `Startup Command`: command executed after terminal startup. Multiline commands and `[sleep=N]` delay are supported.
- `Show in Main Window`: whether this terminal is shown in the main window.
- `Enable Auto Submit Draft Queue On Completion`: whether this terminal can automatically send the next draft after completion.

Startup command example:

```text
[sleep=2000]
dotnet build
[sleep=1000]
dotnet test
```

## Configuration and Data Files

TerminalShell stores runtime configuration next to the running executable. Main files and directories:

```text
config.json          main configuration
config_draft.json    draft storage
config_bak\          automatic config backups
history\             command history
```

If `config.json` is missing or empty, TerminalShell uses default configuration and creates a default terminal.

The configuration loader parses fields defensively and clamps numeric ranges so that one broken field is less likely to prevent the application from starting. When auto backup is enabled, config changes are periodically copied to `config_bak`.

## Development Structure

Main paths:

```text
src\TerminalShell\          main application
src\TerminalShell.Tests\    xUnit test project
src\TerminalShell.sln       solution
docs\                       user documentation
```

Main technology stack:

- `.NET 8`
- WPF
- Windows Forms tray integration
- Embedded ASP.NET Core/Kestrel Remote Web Console
- CommunityToolkit.Mvvm
- ModernWpfUI
- HtmlAgilityPack
- ReverseMarkdown
- QRCoder

Run tests:

```powershell
dotnet test src\TerminalShell.Tests\TerminalShell.Tests.csproj -c Release
```

Build the main application:

```powershell
dotnet build src\TerminalShell\TerminalShell.csproj -c Release
```

## License

This project is licensed under the [MIT License](../LICENSE). You may use, copy, modify, distribute, and use it commercially, as long as the original copyright notice and license text are preserved.

## Project Status

TerminalShell is a productivity tool for Windows command-line power users and AI CLI users. It is not a cross-platform terminal emulator, and it is not meant to fully replace Windows Terminal. Its focus is hosting multiple native console sessions in one stable window, with enhancements for long-running tasks, quick input, history restore, remote viewing, and automatic queued handoff.
