using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace TerminalShell.Services;

public static class RemoteAccessQrCodeRenderer
{
    private static readonly object SyncRoot = new();
    private static string _cachedText = string.Empty;
    private static BitmapImage? _cachedImage;

    public static BitmapImage? Render(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        lock (SyncRoot)
        {
            if (string.Equals(_cachedText, text, StringComparison.Ordinal) && _cachedImage != null)
            {
                return _cachedImage;
            }

            using QRCodeGenerator generator = new();
            using QRCodeData qrCodeData = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            PngByteQRCode qrCode = new(qrCodeData);
            byte[] pngBytes = qrCode.GetGraphic(20);

            using MemoryStream stream = new(pngBytes);
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();

            _cachedText = text;
            _cachedImage = image;
            return image;
        }
    }
}
