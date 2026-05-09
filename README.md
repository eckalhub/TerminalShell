# TerminalShell

TerminalShell 是一个面向 Windows 的多终端宿主工具，基于 `.NET 8` 和 WPF 开发。它把多个原生 `cmd.exe` / `powershell.exe` / [`pwsh.exe`](https://github.com/PowerShell/PowerShell)（PowerShell）控制台嵌入到同一个桌面窗口中，并围绕命令行重度使用、AI CLI 工作流、远程查看和批量任务接力做了专门优化。

它适合 Claude Code、Codex CLI、Gemini CLI 等 AI CLI 终端使用；TerminalShell 不改变这些工具的内核工作流程，而是在保留原始能力的基础上附加更多扩展功能，可以理解为面向 AI CLI 工作流的终端增强版本。它尤其适合重度使用 CLI 的用户，是多开终端管理的理想选择。

## 文档

- [中文说明](docs/UserGuide_%E4%B8%AD%E6%96%87.md)
- [English User Guide](docs/UserGuide_EN.md)

## 核心特性

- 多终端同屏管理：在一个窗口中管理多个原生控制台会话。
- AI CLI 友好：适配 Claude Code、Codex CLI、Gemini CLI 等交互式终端工作流。
- 稳定输入发送：支持多行输入、单回车/双回车提交、`Shift+Enter` 换行和发送延迟。
- 快捷命令系统：支持 `/` 全局命令搜索和 `F1` 到 `F12` 分类命令块。
- 历史与草稿：按终端保存历史，支持草稿队列和任务完成后的自动接力。
- 剪贴板转换：支持图片、HTML、RTF、文件列表到文本或 Markdown 的转换。
- 任务提醒：检测忙碌、完成、失败、等待用户输入等状态，并支持语音或弹窗提醒。
- 远程 Web Console：通过浏览器查看终端状态、尾部输出并发送命令。

## 运行环境

- Windows 桌面环境。
- 开发构建需要 `.NET 8 SDK`。
- framework-dependent 发布包需要目标机器安装 `.NET 8 Desktop Runtime`。
- 推荐安装 PowerShell 7，以便使用 [`pwsh.exe`](https://github.com/PowerShell/PowerShell)。

TerminalShell 当前是 Windows-only WPF 应用，不是 macOS/Linux 原生终端模拟器。

## 从源码运行

```powershell
dotnet restore src\TerminalShell\TerminalShell.csproj
dotnet build src\TerminalShell\TerminalShell.csproj -c Release
dotnet run --project src\TerminalShell\TerminalShell.csproj
```

也可以用 Visual Studio 打开：

```text
src\TerminalShell.sln
```

## 发布

项目发布配置默认只保留简体中文 `zh-Hans` satellite resources，避免输出多余语言资源目录。

日常调试或普通发布只生成 framework-dependent 单文件目录：

```powershell
dotnet restore src\TerminalShell\TerminalShell.csproj -r win-x64
dotnet publish src\TerminalShell\TerminalShell.csproj -c Release --no-restore -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugSymbols=true -p:DebugType=portable -o src\TerminalShell\bin\Release\net8.0-windows\win-x64\publish_single_file
```

正式发布到 GitHub 时，使用发布脚本同时生成 framework-dependent 和 self-contained 两个目录：

```powershell
.\scripts\release-github.ps1 -Tag vX.YY -CommitMessage "Release vX.YY"
```

脚本会先运行测试和 Release 构建，然后生成：

```text
src\TerminalShell\bin\Release\net8.0-windows\win-x64\publish_single_file\TerminalShell.exe
src\TerminalShell\bin\Release\net8.0-windows\win-x64\publish_single_file_self_contained\TerminalShell.exe
```

不需要提交或推送时，可以只验证两个发布目录：

```powershell
.\scripts\release-github.ps1 -Tag vX.YY -SkipGit
```

正式脚本会要求你确认后才执行 `git commit`、`git tag` 和 `git push`。推送 `v*` tag 后，GitHub Actions 会自动创建或更新 GitHub Release，并上传两个 zip 包。发布产物不提交到源码仓库。

GitHub Release 附件名：

```text
TerminalShell-win-x64-dotnet8-dependent.zip
TerminalShell-win-x64-allinone.zip
```

## 开源注意事项

本仓库的 `.gitignore` 会排除本地运行配置、历史记录、备份、调试日志和发布产物。提交前仍建议检查一次：

```powershell
git status --short
```

不要提交：

- `work_home/`
- `work_service4app/`
- `specs.md`
- `todo.md`
- `.agent/`
- `bin/`
- `obj/`
- `*.exe`
- `*.pdb`
- `config.json`
- `history/`
- `config_bak/`

## License

TerminalShell is released under the [MIT License](LICENSE).
