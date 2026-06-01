using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JellybeansPicUploader.Services;

public static class UploadInputHelper
{
    public static bool CanAcceptDataObject(IDataObject dataObject)
    {
        return dataObject.GetDataPresent(DataFormats.FileDrop)
            || dataObject.GetDataPresent(DataFormats.Bitmap)
            || (dataObject.GetDataPresent(DataFormats.Text) && HasImagePathInText(dataObject));
    }

    public static IReadOnlyList<string> CollectImagePathsFromDataObject(IDataObject dataObject)
    {
        var imagePaths = new List<string>();

        if (dataObject.GetDataPresent(DataFormats.FileDrop)
            && dataObject.GetData(DataFormats.FileDrop) is string[] droppedPaths)
        {
            imagePaths.AddRange(CollectImagePathsFromPaths(droppedPaths));
        }

        var clipboardImagePath = TrySaveBitmapSourceToTempFile(dataObject);
        if (!string.IsNullOrWhiteSpace(clipboardImagePath))
        {
            imagePaths.Add(clipboardImagePath);
        }

        if (dataObject.GetDataPresent(DataFormats.Text)
            && dataObject.GetData(DataFormats.Text) is string textPath
            && ImageFileHelper.IsImageFile(textPath.Trim().Trim('"'))
            && File.Exists(textPath.Trim().Trim('"')))
        {
            imagePaths.Add(textPath.Trim().Trim('"'));
        }

        return imagePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> CollectImagePathsFromClipboard()
    {
        try
        {
            if (Clipboard.ContainsFileDropList())
            {
                var fileDropPaths = Clipboard.GetFileDropList();
                var paths = new string[fileDropPaths.Count];
                fileDropPaths.CopyTo(paths, 0);
                return CollectImagePathsFromPaths(paths).ToList();
            }

            if (Clipboard.ContainsImage())
            {
                var imagePath = TrySaveBitmapSourceToTempFile(Clipboard.GetImage());
                return string.IsNullOrWhiteSpace(imagePath) ? [] : [imagePath];
            }

            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText()?.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(text) && File.Exists(text) && ImageFileHelper.IsImageFile(text))
                {
                    return [text];
                }
            }
        }
        catch
        {
            // 剪贴板被占用时忽略
        }

        return [];
    }

    public static IEnumerable<string> CollectImagePathsFromPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (File.Exists(path) && ImageFileHelper.IsImageFile(path))
            {
                yield return path;
                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var file in files.Where(ImageFileHelper.IsImageFile))
            {
                yield return file;
            }
        }
    }

    private static bool HasImagePathInText(IDataObject dataObject)
    {
        if (!dataObject.GetDataPresent(DataFormats.Text))
        {
            return false;
        }

        var text = dataObject.GetData(DataFormats.Text) as string;
        return !string.IsNullOrWhiteSpace(text)
            && File.Exists(text.Trim().Trim('"'))
            && ImageFileHelper.IsImageFile(text.Trim().Trim('"'));
    }

    private static string? TrySaveBitmapSourceToTempFile(IDataObject dataObject)
    {
        try
        {
            if (dataObject.GetDataPresent(DataFormats.Bitmap)
                && dataObject.GetData(DataFormats.Bitmap) is BitmapSource bitmapFromDrop)
            {
                return SaveBitmapSourceToTempFile(bitmapFromDrop);
            }

        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string? TrySaveBitmapSourceToTempFile(BitmapSource? bitmapSource)
    {
        if (bitmapSource is null)
        {
            return null;
        }

        try
        {
            return SaveBitmapSourceToTempFile(bitmapSource);
        }
        catch
        {
            return null;
        }
    }

    private static string SaveBitmapSourceToTempFile(BitmapSource bitmapSource)
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), "JellybeansPicUploader", "Clipboard");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
        using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        encoder.Save(fileStream);
        return outputPath;
    }
}
