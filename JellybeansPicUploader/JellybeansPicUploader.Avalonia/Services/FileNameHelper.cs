namespace JellybeansPicUploader.Services;

using System.IO;
using JellybeansPicUploader.Models;

public static class ImageFileHelper
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif", ".svg", ".ico", ".bmp"
    };

    private static readonly HashSet<string> ProcessableImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".avif", ".bmp"
    };

    public static bool IsImageFile(string filePath)
    {
        return SupportedImageExtensions.Contains(Path.GetExtension(filePath));
    }

    public static bool CanProcessImage(string filePath)
    {
        return ProcessableImageExtensions.Contains(Path.GetExtension(filePath));
    }

    public static string GetFileSuffixWithoutDot(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.TrimStart('.');
    }
}

public static class FileNameHelper
{
    private const int RenameMaxLength = 18;

    public static string BuildRemoteFileName(string originalFileName, UserSettings settings, string? customName = null)
    {
        var suffix = ImageFileHelper.GetFileSuffixWithoutDot(originalFileName);
        var baseName = Path.GetFileNameWithoutExtension(originalFileName);

        if (!string.IsNullOrWhiteSpace(customName))
        {
            baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(customName));
            var customSuffix = ImageFileHelper.GetFileSuffixWithoutDot(customName);
            if (!string.IsNullOrWhiteSpace(customSuffix))
            {
                suffix = customSuffix;
            }
        }
        else
        {
            baseName = SanitizeFileName(baseName);
            if (baseName.Length > RenameMaxLength)
            {
                baseName = baseName[..RenameMaxLength];
            }
        }

        if (settings.ImageName.EnablePrefix && !string.IsNullOrWhiteSpace(settings.ImageName.Prefix))
        {
            baseName = $"{settings.ImageName.Prefix}{baseName}";
        }

        if (settings.ImageName.EnableHash)
        {
            baseName = $"{baseName}.{Guid.NewGuid():N}";
        }

        return string.IsNullOrWhiteSpace(suffix) ? baseName : $"{baseName}.{suffix}";
    }

    public static string BuildTargetDirectoryPath(DirectoryMode directoryMode, string configuredDirectoryPath)
    {
        return directoryMode switch
        {
            DirectoryMode.Root => string.Empty,
            DirectoryMode.Date => DateTime.Now.ToString("yyyyMMdd"),
            DirectoryMode.Repository => configuredDirectoryPath.Replace("\\", "/").Trim('/'),
            DirectoryMode.NewDirectory => configuredDirectoryPath.Replace("\\", "/").Trim('/'),
            _ => configuredDirectoryPath.Replace("\\", "/").Trim('/')
        };
    }

    public static string BuildRemotePath(string targetDirectoryPath, string remoteFileName)
    {
        var directory = targetDirectoryPath.Replace("\\", "/").Trim('/');
        var safeFileName = remoteFileName.Replace("\\", "_").Replace("/", "_").Trim();

        return string.IsNullOrWhiteSpace(directory) ? safeFileName : $"{directory}/{safeFileName}";
    }

    public static string SanitizeDirectorySegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return string.Empty;
        }

        var sanitized = segment.Trim().Replace(' ', '-');
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidChar, '-');
        }

        return sanitized.Trim('-');
    }

    private static string SanitizeFileName(string fileName)
    {
        var sanitized = fileName.Trim().Replace(' ', '-');
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidChar, '-');
        }

        return sanitized;
    }
}
