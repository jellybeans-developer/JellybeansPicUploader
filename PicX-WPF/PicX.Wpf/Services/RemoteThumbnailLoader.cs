using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows.Media.Imaging;

namespace PicXWpf.Services;

/// <summary>
/// 通过 HTTP 下载远程图片并转为 WPF 缩略图（兼容 GitHub 直链与中文路径）。
/// </summary>
public sealed class RemoteThumbnailLoader : IDisposable
{
    private const int ThumbnailDecodePixelWidth = 320;

    private readonly HttpClient _httpClient;
    private bool _disposed;

    public RemoteThumbnailLoader()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("PicX-WPF/1.0");
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("image/*");
    }

    public async Task<BitmapImage?> TryLoadFirstAvailableAsync(
        IEnumerable<string> candidateUrls,
        string? githubToken,
        CancellationToken cancellationToken = default)
    {
        foreach (var candidateUrl in candidateUrls)
        {
            var thumbnail = await TryLoadAsync(candidateUrl, githubToken, cancellationToken).ConfigureAwait(false);
            if (thumbnail is not null)
            {
                return thumbnail;
            }
        }

        return null;
    }

    public async Task<BitmapImage?> TryLoadAsync(
        string imageUrl,
        string? githubToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, imageUri);
            if (!string.IsNullOrWhiteSpace(githubToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", githubToken.Trim());
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(contentType) &&
                !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var memoryStream = new MemoryStream();
            await responseStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            memoryStream.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmapImage.DecodePixelWidth = ThumbnailDecodePixelWidth;
            bitmapImage.StreamSource = memoryStream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _httpClient.Dispose();
        _disposed = true;
    }
}
