using System.IO;
using TerminalShell.Models;
using TerminalShell.Core;

namespace TerminalShell.Services.Clipboard;

/// <summary>
/// 剪贴板配置服务 - 适配 TerminalShell 的 AppConfig
/// 桥接原 AllinTextPaste.ConfigService，为 ClipboardParser/ImageService/TextConverter 提供配置
/// </summary>
public class ClipboardConfigService
{
    private readonly Func<AppConfig> _getConfig;

    /// <summary>
    /// 当前设置（适配对象）
    /// </summary>
    public ClipboardSettings Settings { get; private set; }

    public ClipboardConfigService(Func<AppConfig> getConfig)
    {
        _getConfig = getConfig;
        Settings = new ClipboardSettings(getConfig);
    }

    /// <summary>
    /// 获取图片保存目录的完整路径
    /// </summary>
    public string GetImageDirectoryFullPath()
    {
        var path = Settings.ImageDirectory;

        // 去掉可能的 ./ 前缀
        if (path.StartsWith("./") || path.StartsWith(".\\"))
        {
            path = path.Substring(2);
        }

        // 确保以 \ 结尾
        if (!path.EndsWith('\\') && !path.EndsWith('/'))
        {
            path += Path.DirectorySeparatorChar;
        }

        // 如果是相对路径，基于程序目录
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        }

        // 确保目录存在
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            SimpleLogger.Log($"创建图片目录: {path}");
        }

        return path;
    }
}

/// <summary>
/// 剪贴板设置 - 适配 AppConfig 中的剪贴板相关字段
/// </summary>
public class ClipboardSettings
{
    private readonly Func<AppConfig> _getConfig;

    public ClipboardSettings(Func<AppConfig> getConfig)
    {
        _getConfig = getConfig;
    }

    /// <summary>
    /// 图片保存目录
    /// </summary>
    public string ImageDirectory => _getConfig().ClipboardImageDirectory;

    /// <summary>
    /// 图片输出格式
    /// </summary>
    public string ImageFormat => _getConfig().ClipboardImageFormat;

    /// <summary>
    /// 是否去除 HTML 标签（固定为 true）
    /// </summary>
    public bool StripHtml => true;

    /// <summary>
    /// 输出格式：text 还是 markdown
    /// </summary>
    public string OutputFormat => _getConfig().ClipboardOutputFormat;

    /// <summary>
    /// 是否启用调试输出（固定为 false）
    /// </summary>
    public bool EnableDebugOutput => false;
}
