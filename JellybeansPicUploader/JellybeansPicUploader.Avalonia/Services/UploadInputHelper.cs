using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace JellybeansPicUploader.Services;

public static class UploadInputHelper
{
    public static bool CanAcceptDragEvent(DragEventArgs eventArgs)
    {
        var dataTransfer = eventArgs.DataTransfer;
        return dataTransfer.Formats.Contains(DataFormat.File)
            || dataTransfer.Formats.Contains(DataFormat.Bitmap)
            || dataTransfer.Formats.Contains(DataFormat.Text);
    }

    public static IReadOnlyList<string> CollectImagePathsFromDragEvent(DragEventArgs eventArgs)
    {
        var imagePaths = new List<string>();
        var dataTransfer = eventArgs.DataTransfer;

        if (dataTransfer.Formats.Contains(DataFormat.File))
        {
            var files = dataTransfer.TryGetFiles();
            if (files is not null)
            {
                foreach (var file in files)
                {
                    var path = file.TryGetLocalPath();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        imagePaths.Add(path);
                    }
                }
            }
        }

        if (dataTransfer.Formats.Contains(DataFormat.Bitmap)
            && dataTransfer.TryGetBitmap() is Bitmap bitmap)
        {
            var clipboardImagePath = TrySaveBitmapToTempFile(bitmap);
            if (!string.IsNullOrWhiteSpace(clipboardImagePath))
            {
                imagePaths.Add(clipboardImagePath);
            }
        }

        if (dataTransfer.Formats.Contains(DataFormat.Text)
            && dataTransfer.TryGetText() is string textPath
            && File.Exists(textPath.Trim().Trim('"'))
            && ImageFileHelper.IsImageFile(textPath.Trim().Trim('"')))
        {
            imagePaths.Add(textPath.Trim().Trim('"'));
        }

        return CollectImagePathsFromPaths(imagePaths).ToList();
    }

    public static async Task<IReadOnlyList<string>> CollectImagePathsFromClipboardAsync()
    {
        try
        {
            var topLevel = GetTopLevel();
            var clipboard = topLevel?.Clipboard;
            if (clipboard is null)
            {
                return [];
            }

            if (await clipboard.TryGetFilesAsync() is { } storageItems)
            {
                var paths = storageItems
                    .Select(item => item.TryGetLocalPath())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Cast<string>()
                    .ToArray();
                var imagePaths = CollectImagePathsFromPaths(paths).ToList();
                if (imagePaths.Count > 0)
                {
                    return imagePaths;
                }
            }

            if (await clipboard.TryGetBitmapAsync() is Bitmap bitmap)
            {
                var imagePath = TrySaveBitmapToTempFile(bitmap);
                if (!string.IsNullOrWhiteSpace(imagePath))
                {
                    return [imagePath];
                }
            }

            if (await clipboard.TryGetTextAsync() is string text)
            {
                text = text.Trim().Trim('"');
                if (File.Exists(text) && ImageFileHelper.IsImageFile(text))
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

    private static string? TrySaveBitmapToTempFile(Bitmap bitmap)
    {
        try
        {
            var outputDirectory = Path.Combine(Path.GetTempPath(), "JellybeansPicUploader", "Clipboard");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, $"clipboard_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
            using var fileStream = File.Create(outputPath);
            bitmap.Save(fileStream);
            return outputPath;
        }
        catch
        {
            return null;
        }
    }

    private static TopLevel? GetTopLevel()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return TopLevel.GetTopLevel(desktop.MainWindow);
        }

        return null;
    }
}
