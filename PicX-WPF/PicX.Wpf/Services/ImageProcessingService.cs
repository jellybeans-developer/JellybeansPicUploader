using System.IO;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using PicXWpf.Models;

namespace PicXWpf.Services;

public sealed class ImageProcessingService
{
    public async Task<byte[]> ProcessForUploadAsync(string filePath, UserSettings settings, CancellationToken cancellationToken = default)
    {
        var sourceBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (!ImageFileHelper.CanProcessImage(filePath))
        {
            return sourceBytes;
        }

        try
        {
            byte[] processedBytes = sourceBytes;

            if (settings.Watermark.Enable && !string.IsNullOrWhiteSpace(settings.Watermark.Text))
            {
                processedBytes = await ApplyWatermarkAsync(processedBytes, settings.Watermark, cancellationToken).ConfigureAwait(false);
            }

            if (settings.Compress.Enable)
            {
                processedBytes = await CompressAsync(processedBytes, settings.Compress.Encoder, cancellationToken).ConfigureAwait(false);
            }

            return processedBytes.Length > 0 ? processedBytes : sourceBytes;
        }
        catch
        {
            return sourceBytes;
        }
    }

    public async Task<byte[]> CompressAsync(byte[] sourceBytes, CompressEncoder encoder, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var image = Image.Load(sourceBytes);
            using var outputStream = new MemoryStream();

            switch (encoder)
            {
                case CompressEncoder.MozJpeg:
                    image.Save(outputStream, new JpegEncoder { Quality = 80 });
                    break;
                case CompressEncoder.Avif:
                    try
                    {
                        image.SaveAsWebp(outputStream, new WebpEncoder { Quality = 80 });
                    }
                    catch
                    {
                        image.Save(outputStream, new JpegEncoder { Quality = 80 });
                    }
                    break;
                default:
                    image.SaveAsWebp(outputStream, new WebpEncoder { Quality = 80 });
                    break;
            }

            return outputStream.ToArray();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> ApplyWatermarkAsync(byte[] sourceBytes, WatermarkSettings watermarkSettings, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            using var image = Image.Load(sourceBytes);
            var fontCollection = new FontCollection();
            var fontFamily = SystemFonts.CreateFont("Arial", watermarkSettings.FontSize, FontStyle.Bold).Family;
            var font = fontFamily.CreateFont(watermarkSettings.FontSize, FontStyle.Bold);
            var textOptions = new RichTextOptions(font)
            {
                Origin = CalculateWatermarkOrigin(image.Width, image.Height, watermarkSettings, font, watermarkSettings.Text),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var textColor = ParseHexColor(watermarkSettings.TextColorHex, watermarkSettings.Opacity);
            var strokeColor = Color.FromRgba(0, 0, 0, (byte)(watermarkSettings.Opacity * 255));

            image.Mutate(context =>
            {
                context.DrawText(textOptions, watermarkSettings.Text, strokeColor);
                context.DrawText(textOptions, watermarkSettings.Text, textColor);
            });

            using var outputStream = new MemoryStream();
            image.SaveAsPng(outputStream);
            return outputStream.ToArray();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ConvertToBase64Async(string filePath, CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        var mimeType = GetMimeType(filePath);
        return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
    }

    public async Task SaveBytesToFileAsync(byte[] bytes, string targetFilePath, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(targetFilePath, bytes, cancellationToken).ConfigureAwait(false);
    }

    private static PointF CalculateWatermarkOrigin(int width, int height, WatermarkSettings settings, Font font, string text)
    {
        var textSize = TextMeasurer.MeasureSize(text, new TextOptions(font));
        return settings.Position switch
        {
            WatermarkPosition.LeftTop => new PointF(10, settings.FontSize + 5),
            WatermarkPosition.RightTop => new PointF(width - textSize.Width - 10, settings.FontSize + 5),
            WatermarkPosition.LeftBottom => new PointF(10, height - 15),
            _ => new PointF(width - textSize.Width - 10, height - 15)
        };
    }

    private static Color ParseHexColor(string hexColor, double opacity)
    {
        var normalized = hexColor.TrimStart('#');
        if (normalized.Length != 6)
        {
            return Color.FromRgba(255, 255, 255, (byte)(opacity * 255));
        }

        var red = Convert.ToByte(normalized[..2], 16);
        var green = Convert.ToByte(normalized.Substring(2, 2), 16);
        var blue = Convert.ToByte(normalized.Substring(4, 2), 16);
        return Color.FromRgba(red, green, blue, (byte)(opacity * 255));
    }

    private static string GetMimeType(string filePath)
    {
        return System.IO.Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }
}
