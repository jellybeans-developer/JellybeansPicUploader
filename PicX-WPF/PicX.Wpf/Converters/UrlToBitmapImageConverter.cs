using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace PicXWpf.Converters;

/// <summary>
/// 将图片 URL 转换为 WPF 可显示的 BitmapImage（用于管理页画廊缩略图）。
/// </summary>
public sealed class UrlToBitmapImageConverter : IValueConverter
{
    private const int ThumbnailDecodePixelWidth = 320;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string imageUrl || string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri))
        {
            return null;
        }

        try
        {
            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmapImage.DecodePixelWidth = ThumbnailDecodePixelWidth;
            bitmapImage.UriSource = imageUri;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
