using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using JellybeansPicUploader.Models;

namespace JellybeansPicUploader.Services;

/// <summary>
/// GitHub App OAuth 授权（与网页端图床 使用相同的 client_id 与回调地址）
/// </summary>
public sealed class GitHubOAuthService
{
    private const string ClientId = "Iv1.274fe6f96551b91f";
    private const string AuthorizeUri = "https://github.com/login/oauth/authorize";
    private const string TokenExchangeApi = "https://apis.xpoet.cn/api/github-authorize";
    private const string InstallUrl = "https://github.com/apps/picx-app/installations/select_target";

    /// <summary>
    /// 必须与 GitHub App 注册的回调地址一致（网页端为 http://localhost:4000）
    /// </summary>
    private const int OAuthCallbackPort = 4000;

    public string RedirectUri => $"http://localhost:{OAuthCallbackPort}/";

    public string BuildAuthorizeUrl() =>
        $"{AuthorizeUri}?client_id={ClientId}&redirect_uri={Uri.EscapeDataString(RedirectUri.TrimEnd('/'))}";

    public string BuildInstallUrl() => InstallUrl;

    public void OpenAuthorizePageInBrowser()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = BuildAuthorizeUrl(),
            UseShellExecute = true
        });
    }

    public void OpenInstallPageInBrowser()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = BuildInstallUrl(),
            UseShellExecute = true
        });
    }

    /// <summary>
    /// 启动本地回调监听 → 打开浏览器授权 → 用 code 换取 Token
    /// </summary>
    public async Task<GitHubOAuthResult> AuthorizeWithBrowserAsync(CancellationToken cancellationToken = default)
    {
        HttpListener? listener = null;

        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add(RedirectUri);
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            return GitHubOAuthResult.Fail(
                $"无法在 {RedirectUri} 启动本地回调（端口 {OAuthCallbackPort} 可能被占用）。请关闭占用该端口的程序（如本地 OAuth 回调 dev 服务）后重试。详情：{ex.Message}");
        }

        try
        {
            OpenAuthorizePageInBrowser();

            var contextTask = listener.GetContextAsync();
            var completedTask = await Task.WhenAny(contextTask, Task.Delay(TimeSpan.FromMinutes(5), cancellationToken)).ConfigureAwait(false);

            if (completedTask != contextTask)
            {
                return GitHubOAuthResult.Fail("授权超时：请在浏览器中完成 GitHub 登录后重试。");
            }

            var context = await contextTask.ConfigureAwait(false);
            var query = context.Request.Url?.Query ?? string.Empty;
            var queryParams = ParseQueryString(query);

            await WriteCallbackResponseAsync(context, cancellationToken).ConfigureAwait(false);

            if (queryParams.TryGetValue("setup_action", out var setupAction) &&
                setupAction == "install" &&
                queryParams.ContainsKey("installation_id") &&
                !queryParams.ContainsKey("code"))
            {
                return GitHubOAuthResult.NeedsAuthorizeAfterInstall(
                    queryParams.GetValueOrDefault("installation_id") ?? string.Empty);
            }

            if (!queryParams.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            {
                var error = queryParams.GetValueOrDefault("error_description")
                            ?? queryParams.GetValueOrDefault("error")
                            ?? "未收到授权 code";
                return GitHubOAuthResult.Fail($"GitHub 授权失败：{error}");
            }

            return await ExchangeCodeForTokenAsync(code, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return GitHubOAuthResult.Fail("授权已取消");
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    public async Task<GitHubOAuthResult> ExchangeCodeForTokenAsync(string code, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var redirectWithoutTrailingSlash = RedirectUri.TrimEnd('/');
            var url = $"{TokenExchangeApi}?code={Uri.EscapeDataString(code)}&redirect_uri={Uri.EscapeDataString(redirectWithoutTrailingSlash)}";

            using var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var tokenElement))
            {
                var token = tokenElement.GetString();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    return GitHubOAuthResult.Success(token);
                }
            }

            var message = doc.RootElement.TryGetProperty("msg", out var msgElement)
                ? msgElement.GetString()
                : null;

            return GitHubOAuthResult.Fail(message ?? $"换取 Token 失败（HTTP {(int)response.StatusCode}）");
        }
        catch (Exception ex)
        {
            return GitHubOAuthResult.Fail($"换取 Token 时网络错误：{ex.Message}");
        }
    }

    private static async Task WriteCallbackResponseAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        const string html = """
            <!DOCTYPE html>
            <html lang="zh-CN">
            <head><meta charset="utf-8"/><title>JellybeansPicUploader</title></head>
            <body style="font-family:Segoe UI,sans-serif;text-align:center;padding:48px;">
              <h2>授权完成</h2>
              <p>可以关闭此页面并返回 JellybeansPicUploader 桌面客户端。</p>
            </body>
            </html>
            """;

        var buffer = Encoding.UTF8.GetBytes(html);
        context.Response.StatusCode = 200;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = buffer.Length;
        await context.Response.OutputStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        context.Response.OutputStream.Close();
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        var trimmed = query.StartsWith('?') ? query[1..] : query;
        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }
}

public sealed class GitHubOAuthResult
{
    public bool IsSuccess { get; init; }

    public string? Token { get; init; }

    public string? ErrorMessage { get; init; }

    public bool NeedsContinueAuthorizeAfterInstall { get; init; }

    public string? InstallationId { get; init; }

    public static GitHubOAuthResult Success(string token) =>
        new() { IsSuccess = true, Token = token };

    public static GitHubOAuthResult Fail(string message) =>
        new() { IsSuccess = false, ErrorMessage = message };

    public static GitHubOAuthResult NeedsAuthorizeAfterInstall(string installationId) =>
        new()
        {
            IsSuccess = false,
            NeedsContinueAuthorizeAfterInstall = true,
            InstallationId = installationId,
            ErrorMessage = "GitHub App 已安装，请再次点击「GitHub 授权登录」完成授权。"
        };
}
