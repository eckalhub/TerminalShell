using System.Windows.Media.Imaging;

namespace TerminalShell.Models;

/// <summary>
/// 剪贴板内容类型
/// </summary>
public enum ClipboardContentType
{
    Unknown,
    PlainText,
    Html,
    RichText,
    Image,
    Files
}

/// <summary>
/// 剪贴板内容项目
/// </summary>
public class ClipboardContentItem
{
    /// <summary>
    /// 内容类型
    /// </summary>
    public ClipboardContentType Type { get; set; }

    /// <summary>
    /// 文本内容
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// 图片数据
    /// </summary>
    public BitmapSource? Image { get; set; }

    /// <summary>
    /// 图片本地路径（保存后）
    /// </summary>
    public string? ImagePath { get; set; }

    /// <summary>
    /// 图片格式（扩展名，如 png, jpg, gif）
    /// </summary>
    public string? ImageFormat { get; set; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// 原始 HTML 片段（保留格式用）
    /// </summary>
    public string? HtmlFragment { get; set; }
}

/// <summary>
/// 剪贴板内容
/// </summary>
public class ClipboardContent
{
    /// <summary>
    /// 是否为空
    /// </summary>
    public bool IsEmpty => Items.Count == 0;

    /// <summary>
    /// 内容项目列表
    /// </summary>
    public List<ClipboardContentItem> Items { get; set; } = new();

    /// <summary>
    /// 原始文本（用于调试）
    /// </summary>
    public string? RawText { get; set; }

    /// <summary>
    /// 原始 HTML（用于调试）
    /// </summary>
    public string? RawHtml { get; set; }
}
