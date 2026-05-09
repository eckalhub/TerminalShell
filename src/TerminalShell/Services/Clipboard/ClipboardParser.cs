using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TerminalShell.Models;
using TerminalShell.Core;

using RichTextBox = System.Windows.Controls.RichTextBox;
using WinFormsRichTextBox = System.Windows.Forms.RichTextBox;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using HtmlNode = HtmlAgilityPack.HtmlNode;
using HtmlNodeType = HtmlAgilityPack.HtmlNodeType;

namespace TerminalShell.Services.Clipboard;

/// <summary>
/// 剪贴板内容解析器
/// </summary>
public class ClipboardParser
{
    private readonly ClipboardConfigService _configService;

    public ClipboardParser(ClipboardConfigService configService)
    {
        _configService = configService;
    }

    #region Win32 API

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint CF_DIB = 3;
    private const uint CF_BITMAP = 2;

    #endregion

    private readonly ClipboardDetector _detector = new();

    /// <summary>
    /// 解析剪贴板内容（在 UI 线程执行）
    /// </summary>
    /// <param name="primaryType">已检测的内容类型（可选，避免重复检测）</param>
    public async Task<ClipboardContent> ParseAsync(ClipboardContentType? primaryType = null)
    {
        return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            return Parse(primaryType);
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// 解析剪贴板内容（同步版本）
    /// </summary>
    /// <param name="primaryType">已检测的内容类型（可选，避免重复检测）</param>
    public ClipboardContent Parse(ClipboardContentType? primaryType = null)
    {
        var content = new ClipboardContent();

        try
        {
            SimpleLogger.Log("开始解析剪贴板内容");

            // 使用传入的类型，或检测类型
            var detectedType = primaryType ?? _detector.GetPrimaryContentType();

            if (detectedType == ClipboardContentType.Unknown)
            {
                // 尝试强制解析图片
                var image = TryGetImageWin32();
                if (image != null)
                {
                    content.Items.Add(new ClipboardContentItem
                    {
                        Type = ClipboardContentType.Image,
                        Image = image
                    });
                    SimpleLogger.Log("强制解析图片成功");
                    return content;
                }

                SimpleLogger.Log("剪贴板为空或不支持的格式");
                return content;
            }


            switch (detectedType)
            {
                case ClipboardContentType.Html:
                    ParseHtml(content);
                    break;

                case ClipboardContentType.PlainText:
                    ParsePlainText(content);
                    break;

                case ClipboardContentType.Image:
                    ParseImage(content);
                    break;

                case ClipboardContentType.RichText:
                    ParseRichText(content);
                    break;

                case ClipboardContentType.Files:
                    ParseFiles(content);
                    break;
            }

            SimpleLogger.Log($"剪贴板解析完成，共 {content.Items.Count} 项");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardParser.Parse");
        }

        return content;
    }

    /// <summary>
    /// 尝试使用 Win32 API 获取图片
    /// </summary>
    private BitmapSource? TryGetImageWin32()
    {
        try
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    // 尝试获取 CF_DIB 格式
                    IntPtr hMem = GetClipboardData(CF_DIB);

                    if (hMem != IntPtr.Zero)
                    {
                        var image = LoadBitmapFromDIB(hMem);
                        if (image != null)
                        {
                            SimpleLogger.Log("通过 CF_DIB 成功获取图片");
                            return image;
                        }
                    }


                    // 尝试获取 CF_BITMAP 格式
                    hMem = GetClipboardData(CF_BITMAP);
                    if (hMem != IntPtr.Zero)
                    {
                        var image = LoadBitmapFromHandle(hMem);
                        if (image != null)
                        {
                            SimpleLogger.Log("通过 CF_BITMAP 成功获取图片");
                            return image;
                        }
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardParser.TryGetImageWin32");
        }

        return null;
    }

    /// <summary>
    /// 从 DIB 加载图片
    /// </summary>
    private BitmapSource? LoadBitmapFromDIB(IntPtr hMem)
    {
        try
        {
            IntPtr pMem = GlobalLock(hMem);
            if (pMem == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                // 读取 BITMAPINFOHEADER
                int headerSize = Marshal.SizeOf<BITMAPINFOHEADER>();
                var header = Marshal.PtrToStructure<BITMAPINFOHEADER>(pMem);


                // 计算像素数据偏移量
                int colors = header.biBitCount <= 8 ? (1 << header.biBitCount) : 0;
                int paletteSize = colors * 4;
                IntPtr pixels = new IntPtr(pMem.ToInt64() + headerSize + paletteSize);


                // 计算 stride
                int stride = ((header.biWidth * header.biBitCount + 31) / 32) * 4;
                int imageHeight = Math.Abs(header.biHeight);
                int imageSize = imageHeight * stride;


                // 获取像素数据
                byte[] pixelData = GetPixelData(pixels, stride, imageHeight);

                // 检查像素数据
                if (pixelData == null || pixelData.Length == 0)
                {
                    return null;
                }


                // 创建 WPF BitmapSource
                var bitmapSource = BitmapSource.Create(
                    header.biWidth,
                    imageHeight,
                    96, 96,
                    GetPixelFormat(header.biBitCount),
                    null,
                    pixelData,
                    stride);


                bitmapSource.Freeze();
                return bitmapSource;
            }
            finally
            {
                GlobalUnlock(hMem);
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardParser.LoadBitmapFromDIB");
            return null;
        }
    }

    /// <summary>
    /// 从位图句柄加载图片
    /// </summary>
    private BitmapSource? LoadBitmapFromHandle(IntPtr hBitmap)
    {
        try
        {
            using var bitmap = System.Drawing.Bitmap.FromHbitmap(hBitmap);
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardParser.LoadBitmapFromHandle");
            return null;
        }
    }

    private static byte[] GetPixelData(IntPtr pixels, int stride, int height)
    {
        byte[] data = new byte[stride * height];
        Marshal.Copy(pixels, data, 0, data.Length);
        return data;
    }

    private static System.Windows.Media.PixelFormat GetPixelFormat(int bits)
    {
        return bits switch
        {
            32 => System.Windows.Media.PixelFormats.Bgra32,
            24 => System.Windows.Media.PixelFormats.Bgr24,
            8 => System.Windows.Media.PixelFormats.Indexed8,
            _ => System.Windows.Media.PixelFormats.Bgra32
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public RGBQUAD[] bmiColors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RGBQUAD
    {
        public byte rgbBlue;
        public byte rgbGreen;
        public byte rgbRed;
        public byte rgbReserved;
    }

    /// <summary>
    /// 解析 HTML 内容
    /// </summary>
    private void ParseHtml(ClipboardContent content)
    {
        try
        {
            var htmlData = System.Windows.Clipboard.GetData(System.Windows.DataFormats.Html) as string;
            if (string.IsNullOrEmpty(htmlData))
            {
                return;
            }

            content.RawHtml = htmlData;

            // 保存原始 HTML (调试用)
            try
            {
                var debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_html");
                if (!Directory.Exists(debugDir)) Directory.CreateDirectory(debugDir);
                File.WriteAllText(Path.Combine(debugDir, "0_原始完整HTML.html"), htmlData);
            }
            catch { }

            // 提取 HTML 片段
            var html = ExtractHtmlFragment(htmlData);
            if (string.IsNullOrEmpty(html)) return;

            // 保存片段 (调试用)
            try
            {
                var debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_html");
                File.WriteAllText(Path.Combine(debugDir, "0_原始_HTML片段.html"), html);
            }
            catch { }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // 使用栈进行遍历，支持哨兵模式处理结束标签
            var stack = new Stack<(HtmlNode Node, bool IsEndTag)>();
            var htmlBuilder = new System.Text.StringBuilder();

            if (doc.DocumentNode != null)
            {
                // 检查根节点是否有子节点
                if (doc.DocumentNode.ChildNodes.Count == 0 && !string.IsNullOrWhiteSpace(doc.DocumentNode.InnerText))
                {
                    SimpleLogger.Log("HTML 片段没有子节点，直接使用 InnerText");
                    htmlBuilder.Append(doc.DocumentNode.InnerText);
                }
                else
                {
                    // 逆序压入根节点的子节点
                    for (int i = doc.DocumentNode.ChildNodes.Count - 1; i >= 0; i--)
                    {
                        stack.Push((doc.DocumentNode.ChildNodes[i], false));
                    }
                }
            }

            int nodeCount = 0;
            while (stack.Count > 0)
            {
                var (node, isEndTag) = stack.Pop();
                nodeCount++;

                if (isEndTag)
                {
                    // 处理块级元素结束 -> 插入换行占位符
                    if (IsBlockElement(node.Name))
                    {
                        htmlBuilder.Append("{RETURN}");
                    }
                    else if (node.Name == "li") // 列表项结束也换行
                    {
                        htmlBuilder.Append("{RETURN}");
                    }
                    continue;
                }

                // 处理开始节点
                if (node.NodeType == HtmlNodeType.Text)
                {
                    htmlBuilder.Append(node.InnerText);
                }
                else if (node.NodeType == HtmlNodeType.Element)
                {
                    var name = node.Name.ToLowerInvariant();

                    if (name == "br")
                    {
                        htmlBuilder.Append("{RETURN}");
                    }
                    else if (name == "img")
                    {
                        // 遇到图片：先处理累积文本
                        if (htmlBuilder.Length > 0)
                        {
                            AddTextItem(content, htmlBuilder);
                        }

                        // 添加图片项
                        var src = node.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(src))
                        {
                            content.Items.Add(new ClipboardContentItem
                            {
                                Type = ClipboardContentType.Image,
                                ImagePath = src
                            });
                            SimpleLogger.Log($"发现图片: {src}");
                        }
                    }
                    else
                    {
                        // 其他元素：压入结束哨兵（用于闭合块级元素），再压入子节点
                        
                        // 1. 压入结束哨兵 (除了自闭合元素)
                        stack.Push((node, true));

                        // 2. 如果是 li，可以在这里加个 bullet 或者序号
                        if (name == "li")
                        {
                             htmlBuilder.Append("{RETURN}- ");
                        }

                        // 3. 逆序压入子节点
                        if (node.ChildNodes != null)
                        {
                            for (int i = node.ChildNodes.Count - 1; i >= 0; i--)
                            {
                                stack.Push((node.ChildNodes[i], false));
                            }
                        }
                    }
                }
            }

            SimpleLogger.Log($"[HTML解析] 遍历完成，处理了 {nodeCount} 个节点，Builder长度: {htmlBuilder.Length}");

            // 兜底机制：如果遍历后没有收集到任何文本，但原始 InnerText 不为空，则尝试直接使用 InnerText
            if (htmlBuilder.Length == 0 && content.Items.Count == 0 && doc.DocumentNode != null)
            {
                var rawText = doc.DocumentNode.InnerText;
                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    SimpleLogger.Log("[HTML解析] 遍历未获取到内容，启用兜底机制使用 InnerText");
                    htmlBuilder.Append(rawText);
                }
            }

            // 处理剩余文本
            if (htmlBuilder.Length > 0)
            {
                AddTextItem(content, htmlBuilder);
            }

            if (content.Items.Count > 0)
            {
                content.RawText = string.Join(" ", content.Items
                    .Where(i => !string.IsNullOrWhiteSpace(i.Text))
                    .Select(i => i.Text));
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardParser.ParseHtml");
        }
    }

    /// <summary>
    /// 解析纯文本
    /// </summary>
    private void ParsePlainText(ClipboardContent content)
    {
        try
        {
            var text = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.UnicodeText);
            if (!string.IsNullOrEmpty(text))
            {
                text = CleanText(text);
                content.RawText = text;
                content.Items.Add(new ClipboardContentItem
                {
                    Type = ClipboardContentType.PlainText,
                    Text = text
                });
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardParser.ParsePlainText");
        }
    }

    /// <summary>
    /// 解析图片
    /// </summary>
    private void ParseImage(ClipboardContent content)
    {

        // 治本方案：优先使用 Win32 API 直接读取原始剪贴板数据
        var win32Image = TryGetImageWin32();

        if (win32Image != null)
        {

            // 冻结图片以支持跨线程访问
            if (win32Image.CanFreeze)
            {
                win32Image.Freeze();
            }

            content.Items.Add(new ClipboardContentItem
            {
                Type = ClipboardContentType.Image,
                Image = win32Image
            });
            SimpleLogger.Log($"发现图片(Win32): {win32Image.PixelWidth}x{win32Image.PixelHeight}");
            return;
        }

        // Win32 失败，尝试 WPF 方式
        try
        {
            var image = System.Windows.Clipboard.GetImage();

            if (image != null)
            {

                // 冻结图片以支持跨线程访问
                if (image.CanFreeze)
                {
                    image.Freeze();
                }

                content.Items.Add(new ClipboardContentItem
                {
                    Type = ClipboardContentType.Image,
                    Image = image
                });
                SimpleLogger.Log($"发现图片(WPF): {image.PixelWidth}x{image.PixelHeight}");
                return;
            }
        }
        catch
        {
        }

        // WPF 失败，最后尝试 WinForms
        try
        {
            var winImage = System.Windows.Forms.Clipboard.GetImage();

            if (winImage != null)
            {
                var bitmapSource = ConvertToBitmapSource(winImage);

                if (bitmapSource != null)
                {

                    // 冻结图片以支持跨线程访问
                    if (bitmapSource.CanFreeze)
                    {
                        bitmapSource.Freeze();
                    }

                    content.Items.Add(new ClipboardContentItem
                    {
                        Type = ClipboardContentType.Image,
                        Image = bitmapSource
                    });
                    SimpleLogger.Log($"发现图片(WinForms): {bitmapSource.PixelWidth}x{bitmapSource.PixelHeight}");
                }
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardParser.ParseImage.WinForms");
        }
    }

    /// <summary>
    /// 将 System.Drawing.Image 转换为 WPF BitmapSource
    /// </summary>
    private BitmapSource? ConvertToBitmapSource(System.Drawing.Image image)
    {
        try
        {
            using var ms = new MemoryStream();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardParser.ConvertToBitmapSource");
            return null;
        }
    }

    /// <summary>
    /// 解析富文本
    /// </summary>
    private void ParseRichText(ClipboardContent content)
    {
        try
        {
            // 先尝试获取 HTML 格式（Word 通常会同时提供）
            if (_detector.ContainsHtml())
            {
                ParseHtml(content);
                // 尝试同时解析图片
                ParseImage(content);
                return;
            }

            // 使用 WinForms RichTextBox 解析 RTF
            var rtf = System.Windows.Clipboard.GetData(System.Windows.DataFormats.Rtf) as string;
            if (!string.IsNullOrEmpty(rtf))
            {
                try
                {
                    // 使用 WinForms RichTextBox 解析 RTF
                    var rtb = new WinFormsRichTextBox();
                    rtb.Rtf = rtf;
                    var text = rtb.Text;

                    // 使用 CleanText 处理
                    text = CleanText(text);

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        content.RawText = text;
                        content.Items.Add(new ClipboardContentItem
                        {
                            Type = ClipboardContentType.RichText,
                            Text = text
                        });
                        SimpleLogger.Log("RTF 解析成功");
                    }
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError(ex, "ClipboardParser.ParseRichText.RTF");
                }
            }

            // 如果没有解析到内容，尝试获取纯文本
            if (content.Items.Count == 0 && _detector.ContainsText())
            {
                ParsePlainText(content);
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardParser.ParseRichText");
        }
    }

    /// <summary>
    /// 解析文件列表
    /// </summary>
    private void ParseFiles(ClipboardContent content)
    {
        try
        {
            var files = System.Windows.Clipboard.GetFileDropList();
            foreach (string? file in files)
            {
                if (!string.IsNullOrEmpty(file))
                {
                    content.Items.Add(new ClipboardContentItem
                    {
                        Type = ClipboardContentType.Files,
                        FilePath = file
                    });
                }
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardParser.ParseFiles");
        }
    }

    /// <summary>
    /// 从剪贴板 HTML 数据中提取 HTML 片段
    /// </summary>
    private string ExtractHtmlFragment(string htmlData)
    {
        var match = Regex.Match(htmlData, @"<!--StartFragment-->(.*?)<!--EndFragment-->",
            RegexOptions.Singleline);

        if (match.Success)
        {
            var fragment = match.Groups[1].Value;
            SimpleLogger.Log($"[HTML解析] HTML片段(前100字符): {fragment.Substring(0, System.Math.Min(100, fragment.Length))}");
            return fragment;
        }

        SimpleLogger.Log("[HTML解析] 未找到StartFragment，使用完整HTML");
        return htmlData;
    }

    private void AddTextItem(ClipboardContent content, System.Text.StringBuilder sb)
    {
        var rawText = sb.ToString();
        var text = CleanText(rawText);
        sb.Clear();

        if (!string.IsNullOrWhiteSpace(text))
        {
            content.Items.Add(new ClipboardContentItem
            {
                Type = ClipboardContentType.Html,
                Text = text
            });
        }
    }

    private bool IsBlockElement(string tagName)
    {
        // 常见的块级元素
        return tagName == "p" || tagName == "div" || tagName == "h1" || tagName == "h2" || 
               tagName == "h3" || tagName == "h4" || tagName == "h5" || tagName == "h6" || 
               tagName == "tr" || tagName == "blockquote" || tagName == "pre" || tagName == "hr";
    }

    /// <summary>
    /// 清理文本 - HTML 转纯文本格式转换
    /// 此时输入的 text 已经是提取出的纯文本 + {RETURN} 占位符
    /// </summary>
    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        // 保存 CleanText 输入 (包含 {RETURN} 占位符的原始文本)
        SaveDebugHtml("1_CleanText_Input.txt", text);

        const string RETURN_PLACEHOLDER = "{RETURN}";

        // 1. 解码 HTML 实体 (&nbsp; &lt; 等)
        text = System.Net.WebUtility.HtmlDecode(text);
        SaveDebugHtml("2_CleanText_Decoded.txt", text);

        // 2. 替换 {RETURN} 为真实换行符
        text = text.Replace(RETURN_PLACEHOLDER, "\n");

        // 3. 统一换行符
        text = text.Replace("\r\n", "\n").Replace("\r", "\n");

        // 4. 清理连续换行 (超过2个的换行合并为2个)
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        // 5. 按行处理清理空白
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            // 去除首尾空白
            lines[i] = lines[i].Trim();
        }

        // 6. 重新组合，过滤掉空行
        text = string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));

        var result = text.Trim();
        SaveDebugHtml("9_CleanText_Output.txt", result);
        return result;
    }

    /// <summary>
    /// 保存调试 HTML 到文件
    /// </summary>
    private void SaveDebugHtml(string filename, string content)
    {
        // 检查配置是否启用调试输出
        if (!_configService.Settings.EnableDebugOutput)
        {
            return;
        }

        try
        {
            var debugDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_html");
            if (!Directory.Exists(debugDir))
            {
                Directory.CreateDirectory(debugDir);
            }
            var filePath = Path.Combine(debugDir, filename);
            File.WriteAllText(filePath, content);
            SimpleLogger.Log($"[调试文件] 已保存: {filename}");
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardParser.SaveDebugHtml");
        }
    }

    /// <summary>
    /// 输出调试日志（当监控开启时）
    /// </summary>
    private void LogDebug(string message)
    {
        SimpleLogger.Log("[HTML转换] " + message);
    }
}
