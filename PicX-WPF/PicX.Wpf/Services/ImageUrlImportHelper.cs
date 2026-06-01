using System.IO;
using System.Net.Http;
using PicXWpf.Models;

namespace PicXWpf.Services;

public static class ImageUrlImportHelper
{
    private static readonly HttpClient SharedHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    public static async Task<string?> DownloadImageToTempFileAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(imageUrl.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        using var response = await SharedHttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var extension = ResolveFileExtension(uri, response.Content.Headers.ContentType?.MediaType);
        var outputDirectory = Path.Combine(Path.GetTempPath(), "PicX-WPF", "UrlImport");
        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, $"url_{DateTime.Now:yyyyMMdd_HHmmss_fff}{extension}");

        await using var fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await responseStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        return outputPath;
    }

    private static string ResolveFileExtension(Uri uri, string? contentType)
    {
        var pathExtension = Path.GetExtension(uri.LocalPath);
        if (!string.IsNullOrWhiteSpace(pathExtension) && ImageFileHelper.IsImageFile($"x{pathExtension}"))
        {
            return pathExtension;
        }

        return contentType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            "image/bmp" => ".bmp",
            "image/avif" => ".avif",
            _ => ".png"
        };
    }
}
