using System.IO;
using JellybeansPicUploader.Models;

namespace JellybeansPicUploader.Services;

public sealed class ImageLinkGenerator
{
    public string GenerateGitHubPagesImageUrl(AppSettings settings, string remotePath)
    {
        return GenerateImageUrlByRuleName(settings, remotePath, "GitHubPages", string.Empty);
    }

    public string GenerateJsDelivrImageUrl(AppSettings settings, string remotePath, string branchName, string? commitSha = null)
    {
        var versionReference = ResolveJsDelivrVersionReference(settings.UserSettings, branchName, commitSha);
        return GenerateImageUrlByRuleName(settings, remotePath, "jsDelivr", versionReference);
    }

    public string GeneratePublishedImageUrl(
        AppSettings settings,
        string remotePath,
        string branchName,
        PostUploadPublishMode publishMode,
        string? commitSha = null)
    {
        return publishMode switch
        {
            PostUploadPublishMode.GitHubPages => GenerateGitHubPagesImageUrl(settings, remotePath),
            PostUploadPublishMode.JsDelivr => GenerateJsDelivrImageUrl(settings, remotePath, branchName, commitSha),
            _ => GenerateImageUrl(settings, remotePath, branchName)
        };
    }

    public static string ResolveJsDelivrVersionReference(
        UserSettings userSettings,
        string branchName,
        string? commitSha)
    {
        var branch = string.IsNullOrWhiteSpace(branchName) ? "main" : branchName;

        return userSettings.JsDelivrVersionReferenceMode switch
        {
            JsDelivrVersionReferenceMode.CommitHash when !string.IsNullOrWhiteSpace(commitSha) => commitSha,
            JsDelivrVersionReferenceMode.Tag when !string.IsNullOrWhiteSpace(userSettings.JsDelivrTagName)
                => userSettings.JsDelivrTagName.Trim(),
            _ => branch
        };
    }

    public string GenerateImageUrl(AppSettings settings, string remotePath, string branchName)
    {
        var directImageUrl = GenerateDirectImageUrl(
            settings,
            remotePath,
            settings.UserSettings.ImageLinkType.SelectedRuleName,
            branchName);

        return ApplyLinkFormat(
            settings.UserSettings.ImageLinkFormat,
            directImageUrl,
            Path.GetFileNameWithoutExtension(remotePath.Replace('\\', '/').TrimStart('/')));
    }

    /// <summary>
    /// 生成可直接用于图片预览的直链（不套用 Markdown/HTML 等格式包装）。
    /// </summary>
    public string GenerateDirectImageUrl(AppSettings settings, string remotePath, string branchName)
    {
        return GenerateDirectImageUrl(
            settings,
            remotePath,
            settings.UserSettings.ImageLinkType.SelectedRuleName,
            branchName);
    }

    public string GenerateDirectImageUrl(AppSettings settings, string remotePath, string ruleName, string branchName)
    {
        var normalizedPath = remotePath.Replace('\\', '/').TrimStart('/');
        var branch = string.IsNullOrWhiteSpace(branchName) ? "main" : branchName;
        var encodedPath = ImageUrlPathEncoder.EncodeRepositoryPath(normalizedPath);

        var selectedRule = settings.UserSettings.ImageLinkType.PresetRules
            .FirstOrDefault(rule => rule.Name == ruleName);

        if (string.Equals(ruleName, "GitHub", StringComparison.OrdinalIgnoreCase))
        {
            return ImageUrlPathEncoder.BuildRawGitHubContentUrl(settings.Owner, settings.Repository, branch, normalizedPath);
        }

        if (selectedRule is not null &&
            !string.Equals(ruleName, "GitHubPages", StringComparison.OrdinalIgnoreCase))
        {
            return selectedRule.RuleTemplate
                .Replace("{{owner}}", settings.Owner, StringComparison.Ordinal)
                .Replace("{{repo}}", settings.Repository, StringComparison.Ordinal)
                .Replace("{{branch}}", branch, StringComparison.Ordinal)
                .Replace("{{path}}", encodedPath, StringComparison.Ordinal);
        }

        if (selectedRule is not null &&
            string.Equals(ruleName, "GitHubPages", StringComparison.OrdinalIgnoreCase))
        {
            return selectedRule.RuleTemplate
                .Replace("{{owner}}", settings.Owner, StringComparison.Ordinal)
                .Replace("{{repo}}", settings.Repository, StringComparison.Ordinal)
                .Replace("{{branch}}", branch, StringComparison.Ordinal)
                .Replace("{{path}}", encodedPath, StringComparison.Ordinal);
        }

        return ImageUrlPathEncoder.BuildRawGitHubContentUrl(settings.Owner, settings.Repository, branch, normalizedPath);
    }

    public IReadOnlyList<string> BuildThumbnailCandidateUrls(AppSettings settings, string remotePath, string branchName)
    {
        var branch = string.IsNullOrWhiteSpace(branchName) ? "main" : branchName;
        var normalizedPath = remotePath.Replace('\\', '/').TrimStart('/');
        var candidateUrls = new List<string>();

        void AddCandidate(string? url)
        {
            if (!string.IsNullOrWhiteSpace(url) && !candidateUrls.Contains(url, StringComparer.OrdinalIgnoreCase))
            {
                candidateUrls.Add(url);
            }
        }

        AddCandidate(GenerateDirectImageUrl(settings, remotePath, "jsDelivr", branch));
        AddCandidate(GenerateDirectImageUrl(settings, remotePath, "GitHub", branch));
        AddCandidate(GenerateDirectImageUrl(settings, remotePath, "ChinaJsDelivr", branch));
        AddCandidate(ImageUrlPathEncoder.BuildRawGitHubContentUrl(settings.Owner, settings.Repository, branch, normalizedPath));
        AddCandidate(GenerateDirectImageUrl(settings, remotePath, "GitHubPages", branch));

        return candidateUrls;
    }

    private string GenerateImageUrlByRuleName(AppSettings settings, string remotePath, string ruleName, string branchName)
    {
        return GenerateDirectImageUrl(settings, remotePath, ruleName, branchName);
    }

    public string ApplyLinkFormat(ImageLinkFormatSettings formatSettings, string imageUrl, string imageName)
    {
        if (!formatSettings.Enable)
        {
            return imageUrl;
        }

        return formatSettings.SelectedFormat switch
        {
            ImageLinkFormatType.Markdown => $"![{imageName}]({imageUrl})",
            ImageLinkFormatType.Html => $"<img src=\"{imageUrl}\" alt=\"{imageName}\" />",
            ImageLinkFormatType.BbCode => $"[img]{imageUrl}[/img]",
            ImageLinkFormatType.Custom => ApplyCustomLinkFormat(formatSettings.CustomFormatTemplate, imageUrl, imageName),
            _ => imageUrl
        };
    }

    private static string ApplyCustomLinkFormat(string template, string imageUrl, string imageName)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return imageUrl;
        }

        return template
            .Replace("imageLink", imageUrl, StringComparison.Ordinal)
            .Replace("imageName", imageName, StringComparison.Ordinal);
    }

    public string GenerateBatchLinks(AppSettings settings, IEnumerable<(string RemotePath, string FileName)> images, string branchName)
    {
        return string.Join(Environment.NewLine, images.Select(image =>
            GenerateImageUrl(settings, image.RemotePath, branchName)));
    }
}
