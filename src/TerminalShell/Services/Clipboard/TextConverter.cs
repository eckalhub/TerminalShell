using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TerminalShell.Models;
using TerminalShell.Core;

namespace TerminalShell.Services.Clipboard;

/// <summary>
/// 文本格式转换器
/// </summary>
public class TextConverter
{
    private readonly ClipboardConfigService _configService;

    public TextConverter(ClipboardConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// 将剪贴板内容转换为目标格式
    /// </summary>
    public string Convert(ClipboardContent content, List<string>? imagePaths)
    {
        try
        {
            var outputFormat = _configService.Settings.OutputFormat.ToLowerInvariant();
            return outputFormat switch
            {
                "markdown" => ConvertToFormat(content, imagePaths, isMarkdown: true),
                _ => ConvertToFormat(content, imagePaths, isMarkdown: false)
            };
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "TextConverter.Convert");
            return string.Empty;
        }
    }

    /// <summary>
    /// 转换为指定格式（统一实现）
    /// </summary>
    private string ConvertToFormat(ClipboardContent content, List<string>? imagePaths, bool isMarkdown)
    {
        imagePaths ??= new List<string>();

        // Markdown 模式且存在原始 HTML
        if (isMarkdown && !string.IsNullOrEmpty(content.RawHtml))
        {
            return ConvertHtmlToMarkdown(content.RawHtml, imagePaths);
        }

        // Text 模式（现有逻辑）
        var sb = new StringBuilder();
        int imageIndex = 0;

        // 按 Items 顺序输出
        foreach (var item in content.Items)
        {
            if (item.Type == ClipboardContentType.Image)
            {
                // 图片项：输出图片路径
                if (imageIndex < imagePaths.Count)
                {
                    var currentPath = imagePaths[imageIndex];
                    if (string.IsNullOrEmpty(currentPath))
                    {
                        // Ignore empty fallback
                    }
                    else if (currentPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                             currentPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || 
                             currentPath.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                    {
                        // Output original fallback URL for text mode
                        sb.AppendLine(currentPath);
                    }
                    else
                    {
                        sb.AppendLine(FormatImagePath(currentPath));
                    }
                    imageIndex++;
                }
            }
            else if (item.Type == ClipboardContentType.PlainText ||
                     item.Type == ClipboardContentType.Html ||
                     item.Type == ClipboardContentType.RichText)
            {
                // 文本项：输出文本
                if (!string.IsNullOrWhiteSpace(item.Text))
                {
                    sb.AppendLine(item.Text);
                }
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// 将 HTML 转为 Markdown，并替换图片为本地路径
    /// </summary>
    private string ConvertHtmlToMarkdown(string rawHtml, List<string> imagePaths)
    {
        // 1. 提取独立 HTML 片段 (忽略 StartFragment 注释外的无用头尾)
        var match = Regex.Match(rawHtml, @"<!--StartFragment-->(.*?)<!--EndFragment-->", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var htmlFragment = match.Success ? match.Groups[1].Value : rawHtml;

        // 2. 用 HtmlAgilityPack 预处理 HTML，将图片 src 替换为本地格式化后的路径
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(htmlFragment);

        var imgNodes = doc.DocumentNode.SelectNodes("//img");
        if (imgNodes != null && imagePaths.Count > 0)
        {
            int imgIndex = 0;
            foreach (var img in imgNodes)
            {
                var originalSrc = img.GetAttributeValue("src", "");
                if (string.IsNullOrEmpty(originalSrc)) continue;

                if (imgIndex < imagePaths.Count)
                {
                    // 获取映射路径或其兜底网络路径
                    var currentPath = imagePaths[imgIndex];
                    var formattedPath = currentPath;

                    // 如果不是兜底网络 URL 且不为空，进行本地模板映射
                    if (!string.IsNullOrEmpty(currentPath) && 
                        !currentPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                        !currentPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && 
                        !currentPath.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
                    {
                        formattedPath = FormatImagePath(currentPath);
                    }

                    // 对于 markdown，如果配置直接就是路径，ReverseMarkdown 格式化后就是 ![alt](路径)
                    // 强制清空 alt，使用 image 作为默认，或者保留原有的 alt。
                    var alt = img.GetAttributeValue("alt", "image");
                    if (string.IsNullOrWhiteSpace(alt)) alt = "image";
                    
                    if (!string.IsNullOrEmpty(formattedPath))
                    {
                        img.SetAttributeValue("src", formattedPath);
                        img.SetAttributeValue("alt", alt);
                    }
                    imgIndex++;
                }
            }
        }

        // 移除 style 和 script 节点以防止其文本内容残留
        var nodesToRemove = doc.DocumentNode.SelectNodes("//style | //script");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }
        }

        var processedHtml = doc.DocumentNode.OuterHtml;

        // 3. 使用 ReverseMarkdown 转为 MD
        var config = new ReverseMarkdown.Config
        {
            GithubFlavored = true,
            UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.Bypass, // Bypass 将丢弃未知标签(不保留HTML)但保留其包裹的内部文字
            RemoveComments = true,
            SmartHrefHandling = true
        };

        var converter = new ReverseMarkdown.Converter(config);
        string result = converter.Convert(processedHtml);

        return result.Trim();
    }

    /// <summary>
    /// 生成格式化好的图片路径模板内容
    /// </summary>
    private string FormatImagePath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        var template = _configService.Settings.ImageFormat;

        // 获取图片保存目录
        var imageDir = _configService.GetImageDirectoryFullPath();
        // 确保目录以 \ 结尾
        if (!imageDir.EndsWith('\\'))
            imageDir += "\\";

        // 从实际保存的路径中提取信息
        var extension = Path.GetExtension(path).TrimStart('.');

        // 解析文件名中的时间戳和序号
        // 格式：yyyyMMdd_HHmmss{-XXX}.ext 或 yyyyMMdd_HHmmss.ext
        var baseName = Path.GetFileNameWithoutExtension(path);
        var timePart = baseName;
        var suffix = "";

        // 检查是否有序号后缀（如 -001）
        var dashIndex = baseName.LastIndexOf('-');
        if (dashIndex > 0)
        {
            var potentialSuffix = baseName.Substring(dashIndex);
            if (potentialSuffix.Length == 4 && potentialSuffix.StartsWith('-') &&
                char.IsDigit(potentialSuffix[1]) && char.IsDigit(potentialSuffix[2]) && char.IsDigit(potentialSuffix[3]))
            {
                suffix = potentialSuffix;
                timePart = baseName.Substring(0, dashIndex);
            }
        }

        // 替换模板中的变量
        template = template.Replace("{fullpath}", imageDir);
        template = template.Replace("{time}", timePart);
        template = template.Replace("{-XXX}", suffix);
        template = template.Replace("{ext}", extension);

        return template;
    }

    /// <summary>
    /// 获取相对路径
    /// </summary>
    private string GetRelativePath(string fullPath)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // 尝试获取相对路径
        try
        {
            if (fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = fullPath.Substring(baseDir.Length);
                // 移除开头的路径分隔符
                if (relativePath.StartsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    relativePath = relativePath.Substring(1);
                }
                return relativePath;
            }
        }
        catch
        {
            // 失败时返回原始路径
        }

        return fullPath;
    }

    /// <summary>
    /// 去除 HTML 标签（强制执行）
    /// </summary>
    public string StripHtmlTags(string html)
    {
        // 强制去除 HTML 标签
        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        // 移除脚本和样式
        html = Regex.Replace(html, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

        // 移除 HTML 标签
        html = Regex.Replace(html, @"<[^>]+>", "");

        // 解码 HTML 实体
        html = System.Net.WebUtility.HtmlDecode(html);

        // 清理空白
        html = Regex.Replace(html, @"\s+", " ").Trim();

        return html;
    }
}
