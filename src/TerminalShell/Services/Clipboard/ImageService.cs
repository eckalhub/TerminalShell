using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using TerminalShell.Core;

namespace TerminalShell.Services.Clipboard;

/// <summary>
/// 图片服务
/// </summary>
public class ImageService
{
    private readonly ClipboardConfigService _configService;
    private readonly HttpClient _httpClient;

    public ImageService(ClipboardConfigService configService)
    {
        _configService = configService;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 保存图片（异步）
    /// </summary>
    /// <param name="bitmap">图片数据</param>
    /// <param name="imageFormat">图片格式扩展名（如 "png", "jpg", "gif"），默认为 "png"</param>
    /// <returns>实际保存的文件路径</returns>
    public async Task<string> SaveImageAsync(BitmapSource bitmap, string? imageFormat = null)
    {
        var directory = _configService.GetImageDirectoryFullPath();

        // 解析图片格式（从用户配置中获取）
        var format = ParseImageFormat(imageFormat);

        // 生成文件名（带序号冲突检测）
        var fileName = GenerateFileName(format);
        var filePath = Path.Combine(directory, fileName);

        // 检查目录是否存在
        if (!Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch
            {
            }
        }

        try
        {
            SimpleLogger.Log($"保存图片: {filePath}");

            // 方法1：尝试使用 System.Drawing.Bitmap 保存
            try
            {
                return await SaveUsingSystemDrawingAsync(bitmap, filePath, format);
            }
            catch
            {
            }

            // 方法2：回退到 PngBitmapEncoder
            return await SaveUsingPngEncoderAsync(bitmap, filePath);
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ImageService.SaveImageAsync");
            throw;
        }
    }

    /// <summary>
    /// 使用 System.Drawing 保存图片（更可靠，异步）
    /// </summary>
    private async Task<string> SaveUsingSystemDrawingAsync(BitmapSource bitmap, string filePath, string format)
    {
        return await Task.Run(() =>
        {
            try
            {
                // 检查像素数据
                int stride = bitmap.PixelWidth * 4;
                byte[] pixels = new byte[stride * bitmap.PixelHeight];
                bitmap.CopyPixels(pixels, stride, 0);

                // 检查是否有实际像素（非透明）
                int nonTransparent = 0;
                for (int i = 0; i < pixels.Length; i += 4)
                {
                    if (pixels[i] != 0 || pixels[i+1] != 0 || pixels[i+2] != 0)
                    {
                        nonTransparent++;
                    }
                }

                // 转换为 System.Drawing.Bitmap
                using var ms = new MemoryStream();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(ms);
                ms.Position = 0;

                // 使用 System.Drawing.Image.FromStream
                using var sysImage = System.Drawing.Image.FromStream(ms);
                using var bitmap2 = new System.Drawing.Bitmap(sysImage);

                // 根据格式保存
                var imageFormat = GetImageFormatByExtension(format);
                bitmap2.Save(filePath, imageFormat);

                var fileInfo = new FileInfo(filePath);

                SimpleLogger.Log($"图片保存成功: {filePath}, 格式: {format}");
                return filePath;
            }
            catch
            {
                throw;
            }
        });
    }

    /// <summary>
    /// 根据扩展名获取 ImageFormat
    /// </summary>
    private System.Drawing.Imaging.ImageFormat GetImageFormatByExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
            "gif" => System.Drawing.Imaging.ImageFormat.Gif,
            "bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
            "tiff" or "tif" => System.Drawing.Imaging.ImageFormat.Tiff,
            _ => System.Drawing.Imaging.ImageFormat.Png
        };
    }

    /// <summary>
    /// 使用 PngBitmapEncoder 保存图片（异步）
    /// </summary>
    private async Task<string> SaveUsingPngEncoderAsync(BitmapSource bitmap, string filePath)
    {
        return await Task.Run(() =>
        {

            try
            {
                using var stream = new FileStream(filePath, FileMode.Create);

                // 转换为 Pbgra32 格式
                var converted = new FormatConvertedBitmap(bitmap, System.Windows.Media.PixelFormats.Pbgra32, null, 0);
                converted.Freeze();

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(converted));
                encoder.Save(stream);

                var fileInfo = new FileInfo(filePath);

                SimpleLogger.Log($"图片保存成功: {filePath}");
                return filePath;
            }
            catch
            {
                throw;
            }
        });
    }

    /// <summary>
    /// 下载并保存网络图片
    /// </summary>
    public async Task<string?> DownloadAndSaveImageAsync(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        var directory = _configService.GetImageDirectoryFullPath();
        var fileName = GenerateFileName();
        var filePath = Path.Combine(directory, fileName);

        try
        {
            SimpleLogger.Log($"下载图片: {url}");

            var bytes = await _httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(filePath, bytes);

            SimpleLogger.Log($"图片下载成功: {filePath}");
            return filePath;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ImageService.DownloadAndSaveImageAsync");
            return null;
        }
    }

    /// <summary>
    /// 从 Base64 保存图片
    /// </summary>
    public string? SaveBase64Image(string base64Data)
    {
        if (string.IsNullOrEmpty(base64Data))
        {
            return null;
        }

        try
        {
            // 移除 data:image/xxx;base64, 前缀
            var base64 = base64Data;
            if (base64.Contains(","))
            {
                base64 = base64.Split(',')[1];
            }

            var bytes = Convert.FromBase64String(base64);

            var directory = _configService.GetImageDirectoryFullPath();
            var fileName = GenerateFileName();
            var filePath = Path.Combine(directory, fileName);

            File.WriteAllBytes(filePath, bytes);

            SimpleLogger.Log($"Base64 图片保存成功: {filePath}");
            return filePath;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ImageService.SaveBase64Image");
            return null;
        }
    }

    /// <summary>
    /// 保存本地图片文件
    /// </summary>
    public string? SaveLocalImage(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        try
        {
            var directory = _configService.GetImageDirectoryFullPath();
            var extension = Path.GetExtension(sourcePath);
            var fileName = GenerateFileName(extension);
            var destPath = Path.Combine(directory, fileName);

            File.Copy(sourcePath, destPath, true);

            SimpleLogger.Log($"本地图片保存成功: {destPath}");
            return destPath;
        }
        catch (Exception ex)
        {
            SimpleLogger.LogError(ex, "ImageService.SaveLocalImage");
            return null;
        }
    }

    /// <summary>
    /// 处理图片路径（判断是网络图片、本地图片还是 Base64）
    /// </summary>
    public async Task<string?> ProcessImageAsync(string imageSource)
    {
        if (string.IsNullOrEmpty(imageSource))
        {
            return null;
        }

        if (imageSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return await DownloadAndSaveImageAsync(imageSource);
        }
        else if (imageSource.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
        {
            var localPath = new Uri(imageSource).LocalPath;
            return SaveLocalImage(localPath);
        }
        else if (imageSource.StartsWith("data:image"))
        {
            return SaveBase64Image(imageSource);
        }
        else if (File.Exists(imageSource))
        {
            return SaveLocalImage(imageSource);
        }

        SimpleLogger.Log($"无法处理图片源: {imageSource}");
        return null;
    }

    /// <summary>
    /// 解析图片格式扩展名
    /// </summary>
    private string ParseImageFormat(string? customFormat)
    {
        // 默认 PNG
        var defaultFormat = "png";

        // 如果没有提供自定义格式，从用户配置中解析
        if (string.IsNullOrEmpty(customFormat))
        {
            customFormat = _configService.Settings.ImageFormat;
        }

        // 从模板中提取扩展名（如 .png, .jpg, .gif）
        // 支持格式示例：Image attached [{fullpath}{time}.png] 或 Image attached [{fullpath}{time}.jpg]
        if (!string.IsNullOrEmpty(customFormat))
        {
            var match = System.Text.RegularExpressions.Regex.Match(customFormat, @"\.(\w+)\]");
            if (match.Success)
            {
                return match.Groups[1].Value.ToLowerInvariant();
            }
        }

        return defaultFormat;
    }

    /// <summary>
    /// 生成文件名（带序号冲突检测）
    /// </summary>
    /// <param name="extension">扩展名（不带点），如 "png", "jpg", "gif"</param>
    /// <returns>生成的文件名</returns>
    private string GenerateFileName(string? extension = null)
    {
        var ext = string.IsNullOrEmpty(extension) ? "png" : extension;
        if (!ext.StartsWith('.')) ext = "." + ext;

        var directory = _configService.GetImageDirectoryFullPath();
        var baseName = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // 检查文件是否已存在，逐增序号
        for (int i = 0; i < 1000; i++)
        {
            var fileName = i == 0
                ? $"{baseName}{ext}"
                : $"{baseName}-{i:D3}{ext}";

            var filePath = Path.Combine(directory, fileName);
            if (!File.Exists(filePath))
            {
                return fileName;
            }
        }

        // 如果1000个都存在，返回最后一个（极端情况）
        return $"{baseName}-999{ext}";
    }
}
