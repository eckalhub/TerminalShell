using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using TerminalShell.Models;
using TerminalShell.Core;

using Application = System.Windows.Application;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace TerminalShell.Services.Clipboard;

/// <summary>
/// 剪贴板服务 - 整合内容检测、解析、转换
/// 从 AllinTextPaste 整合，提供剪贴板图文转纯文本功能
/// </summary>
public class ClipboardService
{
    #region Win32 API

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private const uint GMEM_MOVEABLE = 0x0002;
    private const uint CF_UNICODETEXT = 13;

    #endregion

    private readonly ClipboardConfigService _configService;
    private readonly ImageService _imageService;
    private readonly TextConverter _textConverter;
    private readonly ClipboardDetector _detector;
    private readonly ClipboardParser _parser;

    public ClipboardService(ClipboardConfigService configService)
    {
        _configService = configService;
        _imageService = new ImageService(configService);
        _textConverter = new TextConverter(configService);
        _detector = new ClipboardDetector();
        _parser = new ClipboardParser(configService);
    }

    /// <summary>
    /// 获取剪贴板内容并转换为文本
    /// </summary>
    public async Task<string> GetConvertedContentAsync()
    {
        SimpleLogger.Log("开始获取剪贴板内容");

        // 检查剪贴板是否有内容（异步在 UI 线程执行）
        var contentType = await _detector.GetPrimaryContentTypeAsync();

        // 记录解析方法
        var parseMethod = contentType switch
        {
            ClipboardContentType.RichText => "RTF",
            ClipboardContentType.Html => "HTML",
            ClipboardContentType.Image => "图片",
            ClipboardContentType.PlainText => "纯文本",
            ClipboardContentType.Files => "文件",
            _ => "未知"
        };

        SimpleLogger.Log($"检测到内容类型: {parseMethod}");

        if (contentType == ClipboardContentType.Unknown)
        {
            SimpleLogger.Log("剪贴板为空");
            return string.Empty;
        }

        // 解析剪贴板内容（异步在 UI 线程执行，传入已检测的类型避免重复检测）
        var content = await _parser.ParseAsync(contentType);

        if (content.IsEmpty)
        {
            SimpleLogger.Log("剪贴板内容为空");
            return string.Empty;
        }

        // 处理图片
        var imagePaths = await ProcessImagesAsync(content);

        // 转换格式
        var result = _textConverter.Convert(content, imagePaths);

        SimpleLogger.Log($"剪贴板内容转换完成，使用方法: {parseMethod}，结果长度: {result.Length}");

        return result;
    }

    /// <summary>
    /// 检查剪贴板是否包含需要转换的内容（图片/HTML/RTF）
    /// 纯文本不需要转换，直接放行默认粘贴
    /// </summary>
    public async Task<bool> HasConvertibleContentAsync()
    {
        var contentType = await _detector.GetPrimaryContentTypeAsync();

        // 只有图片、HTML、RTF、文件需要转换
        // 纯文本直接放行
        return contentType == ClipboardContentType.Image ||
               contentType == ClipboardContentType.Html ||
               contentType == ClipboardContentType.RichText ||
               contentType == ClipboardContentType.Files;
    }

    /// <summary>
    /// 处理剪贴板中的图片
    /// </summary>
    private async Task<List<string>> ProcessImagesAsync(ClipboardContent content)
    {
        var imagePaths = new List<string>();

        // 按 Items 顺序处理图片（保持与文本的相对位置）
        foreach (var item in content.Items.Where(i => i.Type == ClipboardContentType.Image))
        {
            try
            {
                // 优先处理有 BitmapSource 的图片（直接从剪贴板读取）
                if (item.Image != null)
                {
                    var path = await _imageService.SaveImageAsync(item.Image);
                    if (!string.IsNullOrEmpty(path))
                    {
                        imagePaths.Add(path);
                    }
                    else
                    {
                        // 失败时混入空值占位，防止索引错位 (Index Shift Bug)
                        imagePaths.Add("");
                    }
                }
                // 处理有 ImagePath 的图片（从 HTML 中提取）
                else if (!string.IsNullOrEmpty(item.ImagePath))
                {
                    var paths = await ProcessHtmlImageAsync(item.ImagePath);
                    imagePaths.AddRange(paths);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "ClipboardService.ProcessImagesAsync");
            }
        }

        return imagePaths;
    }

    /// <summary>
    /// 处理单个 HTML 图片
    /// </summary>
    private async Task<List<string>> ProcessHtmlImageAsync(string src)
    {
        var imagePaths = new List<string>();

        try
        {
            if (string.IsNullOrEmpty(src))
            {
                return imagePaths;
            }

            SimpleLogger.Log($"处理 HTML 图片: {src}");

            var path = await _imageService.ProcessImageAsync(src);
            if (!string.IsNullOrEmpty(path))
            {
                imagePaths.Add(path);
            }
            else
            {
                // 网络/下载失败时保留原 URL 兜底，防止错标
                imagePaths.Add(src);
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardService.ProcessHtmlImageAsync");
        }

        return imagePaths;
    }

    /// <summary>
    /// 处理 HTML 中的图片（旧方法，保留兼容）
    /// </summary>
    private async Task<List<string>> ProcessHtmlImagesAsync(string html)
    {
        var imagePaths = new List<string>();

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var imgNodes = doc.DocumentNode.SelectNodes("//img");
            if (imgNodes == null)
            {
                return imagePaths;
            }

            foreach (var img in imgNodes)
            {
                var src = img.GetAttributeValue("src", "");
                if (string.IsNullOrEmpty(src))
                {
                    continue;
                }

                SimpleLogger.Log($"处理 HTML 图片: {src}");

                try
                {
                    var path = await _imageService.ProcessImageAsync(src);
                    if (!string.IsNullOrEmpty(path))
                    {
                        imagePaths.Add(path);
                    }
                    else
                    {
                        imagePaths.Add(src); // Fallback
                    }
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError(ex, "ClipboardService.ProcessHtmlImagesAsync");
                }
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardService.ProcessHtmlImagesAsync");
        }

        return imagePaths;
    }

    /// <summary>
    /// 将文本复制到剪贴板（带重试机制，异步执行避免阻塞UI）
    /// </summary>
    public async Task SetTextAsync(string text)
    {
        const int maxRetries = 10;
        const int retryDelayMs = 200;

        // 先尝试 Win32 API（更可靠）
        for (int i = 0; i < maxRetries; i++)
        {
            if (SetTextWin32(text))
            {
                SimpleLogger.Log($"已设置剪贴板内容(Win32)，长度: {text.Length}");
                return;
            }

            await Task.Delay(retryDelayMs);
        }

        // Win32 失败，回退到 WPF Clipboard
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                System.Windows.Clipboard.SetText(text);
                SimpleLogger.Log($"已设置剪贴板内容(WPF)，长度: {text.Length}");
                return;
            }
            catch (Exception) when (i < maxRetries - 1)
            {
                await Task.Delay(retryDelayMs);
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError(ex, "ClipboardService.SetTextAsync");
                throw;
            }
        }
    }

    /// <summary>
    /// 使用 Win32 API 设置文本到剪贴板
    /// </summary>
    private bool SetTextWin32(string text)
    {
        try
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                return false;
            }

            try
            {
                if (!EmptyClipboard())
                {
                    return false;
                }

                // 将字符串转换为 UTF-16 字节数组（带 null 终止符）
                var bytes = System.Text.Encoding.Unicode.GetBytes(text + "\0");
                var size = new UIntPtr((uint)bytes.Length);

                var hMem = GlobalAlloc(GMEM_MOVEABLE, size);
                if (hMem == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    var pMem = GlobalLock(hMem);
                    if (pMem == IntPtr.Zero)
                    {
                        return false;
                    }

                    try
                    {
                        Marshal.Copy(bytes, 0, pMem, bytes.Length);
                    }
                    finally
                    {
                        GlobalUnlock(hMem);
                    }

                    if (SetClipboardData(CF_UNICODETEXT, hMem) == IntPtr.Zero)
                    {
                        return false;
                    }

                    return true;
                }
                catch
                {
                    GlobalFree(hMem);
                    throw;
                }
            }
            finally
            {
                CloseClipboard();
            }
        }
        catch
        {
            return false;
        }
    }
}
