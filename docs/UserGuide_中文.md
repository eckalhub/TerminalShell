# TerminalShell 中文说明

TerminalShell 是一个面向 Windows 的多终端宿主工具，基于 `.NET 8` 和 WPF 开发。它把多个原生 `cmd.exe` / `powershell.exe` / [`pwsh.exe`](https://github.com/PowerShell/PowerShell)（PowerShell）控制台嵌入到同一个桌面窗口中，并围绕命令行重度使用、AI CLI 工作流、远程查看和批量任务接力做了专门优化。它适合 Claude Code、Codex CLI、Gemini CLI 等 AI CLI 终端使用；TerminalShell 不改变 Claude Code 等工具的内核工作流程，而是在保留原始能力的基础上附加更多扩展功能，可以理解为面向 AI CLI 工作流的终端增强版本。它尤其适合重度使用 CLI 的用户，是多开终端管理的理想选择。

## 核心特性

- **多终端同屏管理**：在一个主窗口中同时显示多个终端，可按文件夹分组、拖拽排序、显示/隐藏、重启单个终端，也可以把某个终端临时最大化。
- **原生控制台嵌入**：通过 Win32 API 启动、捕获并托管原生控制台窗口，保留真实 shell 行为，同时减少多窗口切换成本。
- **稳定的输入发送**：输入区支持多行内容、单回车/双回车提交模式、`Shift+Enter` 换行、可配置发送延迟，适配 Codex、Claude Code、Gemini CLI 等交互式 TUI。
- **快捷命令系统**：支持 `/` 全局搜索命令，也支持 `F1` 到 `F12` 打开分类命令块；命令中可使用 `[return]` 宏插入真实换行。
- **历史与草稿**：按终端隔离保存命令历史，支持搜索、预览、双击回填；输入内容可保存为草稿，并支持自动草稿队列接力发送。
- **剪贴板转换**：粘贴图片、HTML、RTF 或文件列表时，可自动转换为纯文本或 Markdown；图片会保存到配置目录，并按模板插入路径。
- **任务提醒**：可监测终端输出的忙碌、完成、失败和等待用户输入状态，支持单终端完成/失败语音提醒，以及全部终端空闲后的语音或弹窗提醒。
- **远程 Web Console**：内置 HTTP Web 控制台，可通过浏览器访问 `/remote/` 查看终端列表、实时尾部输出、发送命令、发送草稿和切换自动草稿队列。
- **系统集成**：支持单实例运行、系统托盘、托盘恢复/设置/重启/退出、开机启动、最小化到托盘、关闭主窗口隐藏到托盘。
- **主题与外观**：主窗口主题色、输入框字体、水印、按钮、菜单和补全弹窗颜色都可以在设置中调整。

## 适用场景

TerminalShell 适合需要长期同时运行多个命令行任务的 Windows 用户，例如：

- 同时管理多个项目的开发服务器、构建命令和日志输出。
- 把多个 AI CLI 会话固定在同一个窗口内，减少任务栏和窗口切换。
- 通过手机或另一台设备临时查看本机终端状态，并远程发送简短命令。
- 保存高频命令、长提示词或多行脚本，并用快捷键快速回填。
- 让多个草稿命令在上一轮任务完成后自动接力执行。

## 快速开始

### 运行环境

- Windows 桌面环境。
- 开发构建需要 `.NET 8 SDK`。
- 运行 framework-dependent 发布包需要目标机器已安装 `.NET 8 Desktop Runtime`。
- 推荐安装 PowerShell 7，这样 [`pwsh.exe`](https://github.com/PowerShell/PowerShell)（PowerShell）终端会优先可用；未安装时会回退到 Windows PowerShell 或 `cmd.exe`。

### 从源码运行

```powershell
dotnet restore src\TerminalShell\TerminalShell.csproj
dotnet build src\TerminalShell\TerminalShell.csproj -c Release
dotnet run --project src\TerminalShell\TerminalShell.csproj
```

也可以直接用 Visual Studio 打开：

```text
src\TerminalShell.sln
```

### 单文件发布

项目默认按 framework-dependent 单文件发布，不把 .NET 运行时打包进去：

```powershell
dotnet restore src\TerminalShell\TerminalShell.csproj -r win-x64
dotnet publish src\TerminalShell\TerminalShell.csproj -c Release --no-restore -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugSymbols=true -p:DebugType=portable -o src\TerminalShell\bin\Release\net8.0-windows\win-x64\publish_single_file
```

发布后主要产物：

```text
src\TerminalShell\bin\Release\net8.0-windows\win-x64\publish_single_file\TerminalShell.exe
src\TerminalShell\bin\Release\net8.0-windows\win-x64\publish_single_file\TerminalShell.pdb
```

## 基础使用

### 主界面

主窗口由多个终端面板组成，每个面板包含：

- 顶部标题栏：显示终端名称，并提供右键菜单。
- 中间区域：嵌入的原生控制台窗口。
- 底部输入区：输入、粘贴、保存草稿、发送命令和打开历史。

终端标题栏右键菜单常用操作：

- `Settings`：打开设置窗口。
- `Show Terminal`：从隐藏终端列表中恢复终端。
- `Restart This Terminal`：重启当前终端进程。
- `Open Working Directory`：打开当前终端配置的工作目录。
- `Hide`：从主界面隐藏当前终端并关闭对应进程。
- `Toggle Fullscreen`：最大化或还原当前终端。
- `Always on Top`：切换主窗口置顶。
- `Allow sound reminders after tasks are completed`：切换任务提醒总开关。

### 输入与提交

底部输入框是所有命令发送入口。输入区支持自动换行、可拖拽高度和自定义字体。

提交模式有两种：

- `SingleEnter`：按 `Enter` 立即发送当前输入。
- `DoubleEnter`：第一次 `Enter` 插入换行，短时间内第二次 `Enter` 才发送。

无论使用哪种模式，`Shift+Enter` 都用于输入换行。也可以点击右侧 `Send` 按钮手动发送。

发送成功的手动命令会写入当前终端的历史记录；启动命令不会写入历史。

### 历史记录

点击输入区右侧 `History` 按钮可以打开当前终端的历史窗口。

历史窗口特点：

- 每个终端可配置独立历史目录。
- 左侧按时间倒序显示历史文件。
- 支持按文件名和内容搜索。
- 右侧预览完整命令内容。
- 双击历史项会把内容回填到原终端输入框。

历史保存位置在程序运行目录下的 `history\<终端历史目录>`。

### 草稿与自动草稿队列

在输入框右键菜单中选择 `Save To Draft` 可以把当前输入保存为草稿。草稿会显示在输入区底部，可点击回填，也可右键移动顺序或删除。

启用 `Enable Auto Draft Queue On Completion` 后，当前终端会在上一轮任务完成后自动发送下一条草稿。系统会等待终端重新进入忙碌状态，再等待下一次完成闭环，不会一次性连发全部草稿。

如果终端输出命中“等待用户输入”关键词，自动草稿队列会暂停，避免在需要确认或选择时继续发送后续命令。

## 快捷命令

在 `Settings -> Custom Commands` 中可以维护快捷命令文本。

普通命令每行一条。输入框中键入 `/` 后会弹出全局搜索菜单，继续输入会过滤结果，用上下键选择，按 `Enter` 或 `Tab` 回填。

也可以使用分类块：

```text
[F1]
进度到多少了现在,剩下什么没做
确认,按你的建议来
[/F1]

[F2]
dotnet build src\TerminalShell\TerminalShell.csproj -c Release
[/F2]
```

在终端输入框中直接按 `F1` 到 `F12`，会打开对应分类块菜单。分类菜单打开后继续输入文本，只会过滤当前分类，不会污染真实输入框。

多行命令可以用 `[return]` 表示真实换行：

```text
cd src[return]dotnet build
```

## 剪贴板转换

当输入框中按 `Ctrl+V` 时，系统会检测剪贴板内容：

- 纯文本：走系统默认粘贴。
- 图片：保存到配置的图片目录，并插入格式化后的图片路径。
- HTML / RTF：清洗后转换为纯文本或 Markdown。
- 文件列表：转换为文件路径文本。

输出格式可以在输入框右键菜单中快速切换：

- `Paste Output: Markdown`
- `Paste Output: Raw Text`

也可以在 `Settings -> Global Settings -> Clipboard Conversion` 中配置：

- 是否启用转换。
- 输出格式。
- 图片保存目录。
- 图片路径输出模板。

默认图片模板：

```text
Image attached [{fullpath}{time}{-XXX}.{ext}]
```

## 任务提醒

TerminalShell 会定期读取终端尾部输出，用于判断任务状态。

支持的提醒能力：

- 单终端任务完成 TTS。
- 单终端任务失败 TTS。
- 所有主窗口终端都空闲后的语音提醒。
- 所有主窗口终端都空闲后的弹窗提醒。
- 失败关键词识别。
- 等待用户输入关键词识别。

相关配置位于 `Settings -> Global Settings -> Task Completion Alerts`。

可配置项包括：

- 统一提醒总开关。
- 检测周期。
- 稳定尾部阈值。
- 读取尾部行数。
- Windows 系统语音、语速、音量。
- 完成、失败和全终端空闲的 TTS 模板。
- 等待输入关键词。
- 失败关键词。

模板中可使用：

```text
{TerminalVoiceName_or_TerminalName}
{TerminalName}
{FailureKeyword}
```

## 远程 Web Console

开启后，TerminalShell 会在本机启动一个内置 HTTP Web 控制台。

入口路径：

```text
http://<本机地址>:<端口>/remote/
```

远程控制台支持：

- 密码登录。
- 查看当前主窗口中显示的终端列表。
- 查看单终端实时尾部输出。
- 发送命令。
- 查看、发送和删除草稿。
- 开关当前终端的自动草稿队列。
- 通过 WebSocket 自动刷新状态。

安全设计：

- 未配置访问密码时不会启动远程控制台。
- 配置文件只保存密码哈希，不保存明文密码。
- 登录失败次数和锁定时间可配置。
- Cookie 使用本机可校验的签名票据。

当前版本说明：

- `HTTP` 模式可用。
- `HTTPS` 选项是预留能力，当前版本保存配置但不会启动 HTTPS Host。
- 根路径不会提供页面，访问路径应使用 `/remote/`。
- 如需跨网络访问，可自行配合 Tailscale、frp/frpc、SSH 反向隧道等方案。

## 设置说明

设置窗口主要包含以下区域：

- `Global Settings`：窗口布局、输入字体、提交模式、默认启动命令、水印、任务提醒、远程控制台、历史、剪贴板、自动备份、开机启动和托盘行为。
- `Custom Commands`：维护 `/` 和 `F1-F12` 快捷命令。
- `themes`：配置主窗口主题色。
- 文件夹和终端配置：维护终端分组、终端名称、工作目录、启动命令、shell 类型、历史目录、备注和是否显示在主窗口。

终端配置字段说明：

- `Terminal Name`：终端显示名称。
- `Terminal Voice Name`：语音提醒使用的名称；为空时回退到终端名。
- `Message History Save to Folder`：历史记录保存子目录；为空时使用终端名。
- `Shell Type`：可选 `cmd.exe`、`powershell.exe`、[`pwsh.exe`](https://github.com/PowerShell/PowerShell)（PowerShell）。
- `Working Directory`：终端启动工作目录。
- `Startup Command`：终端启动后自动执行的命令，支持多行和 `[sleep=N]` 延迟。
- `Show in Main Window`：是否显示在主窗口。
- `Enable Auto Submit Draft Queue On Completion`：是否允许该终端自动发送下一条草稿。

启动命令示例：

```text
[sleep=2000]
dotnet build
[sleep=1000]
dotnet test
```

## 配置与数据文件

TerminalShell 的运行配置位于程序运行目录，主要文件和目录包括：

```text
config.json          主配置
config_draft.json    草稿配置
config_bak\          自动配置备份
history\             命令历史
debug_html\          剪贴板 HTML 调试输出
debug_output.log     调试日志
```

如果 `config.json` 不存在或为空，程序会使用默认配置，并自动创建一个默认终端。

配置系统采用逐字段解析和边界钳制，尽量避免单个字段损坏导致整个应用无法启动。启用自动备份后，配置变化会按间隔保存到 `config_bak`。

## 开发结构

主要目录：

```text
src\TerminalShell\          主程序
src\TerminalShell.Tests\    xUnit 测试项目
src\TerminalShell.sln       解决方案
specs.md                    当前技术规格
todo.md                     历史任务与待办记录
.agent\rules.md             项目协作规则
```

主项目技术栈：

- `.NET 8`
- WPF
- Windows Forms 托盘能力
- ASP.NET Core/Kestrel 内嵌远程 Web Console
- CommunityToolkit.Mvvm
- ModernWpfUI
- HtmlAgilityPack
- ReverseMarkdown
- QRCoder

运行测试：

```powershell
dotnet test src\TerminalShell.Tests\TerminalShell.Tests.csproj -c Release
```

构建主程序：

```powershell
dotnet build src\TerminalShell\TerminalShell.csproj -c Release
```

## 开源前注意事项

当前仓库中存在运行目录、备份目录、历史记录、调试输出和个人配置样例。开源前建议先补充 `.gitignore`，并避免提交以下内容：

```text
work_home\config.json
work_service4app\config.json
**\config_bak\
**\history\
**\debug_html\
**\debug_output.log
**\bin\
**\obj\
*.pdb
*.exe
```

还需要特别检查：

- 本机绝对路径。
- 远程访问密码哈希。
- 服务器 IP、端口、账号、口令。
- 私有项目名称。
- 个人快捷命令。
- 历史命令记录。
- 临时截图和调试文件。

这些内容不属于项目源码的一部分，不应进入公开仓库。

## 许可证

本项目采用 [MIT License](../LICENSE) 开源。你可以自由使用、复制、修改、分发和用于商业用途，但需要保留原始版权声明和许可证文本。

## 状态说明

TerminalShell 目前更像一个面向 Windows 命令行重度用户和 AI CLI 用户的生产力工具。它不是跨平台终端模拟器，也不是完整替代 Windows Terminal 的通用产品；它的重点是把多个原生控制台会话稳定地收纳到一个窗口中，并围绕长任务、快捷输入、历史回填、远程查看和自动接力做增强。
