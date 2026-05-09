using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using TerminalShell.Core;
using TerminalShell.Models;

namespace TerminalShell.Services.Clipboard;

/// <summary>
/// 剪贴板内容类型检测器
/// </summary>
public class ClipboardDetector
{
    #region Win32 API

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll")]
    private static extern bool IsClipboardFormatAvailable(uint format);

    // 标准剪贴板格式
    private const uint CF_BITMAP = 2;
    private const uint CF_DIB = 3;
    private const uint CF_DIBV5 = 17;
    private const uint CF_PNG = 0x0000001E;  // PNG 图片
    private const uint CF_JPEG = 0x0000001F; // JPEG 图片

    #endregion

    private uint _lastSequenceNumber = 0;

    /// <summary>
    /// 异步检测主要类型（在 UI 线程执行）
    /// </summary>
    public async Task<ClipboardContentType> GetPrimaryContentTypeAsync()
    {
        return await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            return GetPrimaryContentType();
        }, DispatcherPriority.Background);
    }

    /// <summary>
    /// 检测剪贴板是否有任何数据
    /// </summary>
    public bool HasAnyData()
    {
        try
        {

            bool hasText = System.Windows.Clipboard.ContainsText();
            bool hasImage = ContainsImage();
            bool hasFiles = System.Windows.Clipboard.ContainsFileDropList();
            bool hasHtml = System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Html);
            bool hasRtf = System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Rtf);


            return hasText || hasImage || hasFiles || hasHtml || hasRtf;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardDetector.HasAnyData");
            return false;
        }
    }

    /// <summary>
    /// 检测剪贴板可用的格式
    /// </summary>
    public string[] GetAvailableFormats()
    {
        try
        {

            if (!HasAnyData())
            {
                return Array.Empty<string>();
            }

            var data = System.Windows.Clipboard.GetDataObject();
            if (data == null)
            {
                return Array.Empty<string>();
            }

            var formats = data.GetFormats();

            return formats;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardDetector.GetAvailableFormats");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// 检测是否包含文本
    /// </summary>
    public bool ContainsText()
    {
        try
        {
            return System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.UnicodeText);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardDetector.ContainsText");
            return false;
        }
    }

    /// <summary>
    /// 检测是否包含 HTML
    /// </summary>
    public bool ContainsHtml()
    {
        try
        {
            return System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Html);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardDetector.ContainsHtml");
            return false;
        }
    }

    /// <summary>
    /// 检测是否包含富文本
    /// </summary>
    public bool ContainsRichText()
    {
        try
        {
            return System.Windows.Clipboard.ContainsData(System.Windows.DataFormats.Rtf);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardDetector.ContainsRichText");
            return false;
        }
    }

    /// <summary>
    /// 使用 Win32 API 检测是否包含图片
    /// </summary>
    public bool ContainsImage()
    {
        try
        {

            // 首先获取剪贴板所有可用格式
            try
            {
                var dataObject = System.Windows.Clipboard.GetDataObject();
                if (dataObject != null)
                {
                    var formats = dataObject.GetFormats();
                }
            }
            catch
            {
            }

            // 先尝试 WPF 方式
            bool wpfResult = System.Windows.Clipboard.ContainsImage();

            if (wpfResult)
            {
                SimpleLogger.Log("检测到图片(WPF)");
                return true;
            }

            // 使用 Win32 API 检测更多图片格式

            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    // 枚举所有可用格式

                    // 检测多种图片格式
                    bool hasBitmap = IsClipboardFormatAvailable(CF_BITMAP);
                    bool hasDib = IsClipboardFormatAvailable(CF_DIB);
                    bool hasDibV5 = IsClipboardFormatAvailable(CF_DIBV5);
                    bool hasPng = IsClipboardFormatAvailable(CF_PNG);
                    bool hasJpeg = IsClipboardFormatAvailable(CF_JPEG);


                    SimpleLogger.Log($"Win32图片检测: CF_BITMAP={hasBitmap}, CF_DIB={hasDib}, CF_DIBV5={hasDibV5}, CF_PNG={hasPng}, CF_JPEG={hasJpeg}");

                    if (hasBitmap || hasDib || hasDibV5 || hasPng || hasJpeg)
                    {
                        return true;
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }
            else
            {
            }

            // 最后尝试 WinForms
            try
            {
                bool winformsResult = System.Windows.Forms.Clipboard.ContainsImage();
                return winformsResult;
            }
            catch
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardDetector.ContainsImage");
            return false;
        }
    }

    /// <summary>
    /// 检测是否包含文件
    /// </summary>
    public bool ContainsFiles()
    {
        try
        {
            return System.Windows.Clipboard.ContainsFileDropList();
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ClipboardDetector.ContainsFiles");
            return false;
        }
    }

    /// <summary>
    /// 剪贴板内容是否发生变化
    /// </summary>
    public bool HasClipboardChanged()
    {
        try
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                return false;
            }

            try
            {
                uint currentSeq = GetClipboardSequenceNumber();
                if (currentSeq != _lastSequenceNumber)
                {
                    _lastSequenceNumber = currentSeq;
                    return true;
                }
                return false;
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

    /// <summary>
    /// 获取优先使用的内容类型
    /// </summary>
    public ClipboardContentType GetPrimaryContentType()
    {

        // 优先级: HTML > RTF > 图片 > 纯文本 > 文件

        // 优先检查 HTML
        bool hasHtml = ContainsHtml();
        if (hasHtml)
        {
            return ClipboardContentType.Html;
        }

        // 检查 RTF
        bool hasRichText = ContainsRichText();
        if (hasRichText)
        {
            return ClipboardContentType.RichText;
        }

        bool hasImage = ContainsImage();
        if (hasImage)
        {
            return ClipboardContentType.Image;
        }

        bool hasText = ContainsText();
        if (hasText)
        {
            return ClipboardContentType.PlainText;
        }

        bool hasFiles = ContainsFiles();
        if (hasFiles)
        {
            return ClipboardContentType.Files;
        }

        return ClipboardContentType.Unknown;
    }
}
