namespace JellybeansPicUploader.Services;

/// <summary>
/// 为图床直链编码仓库路径（保留斜杠，编码中文等特殊字符）。
/// </summary>
public static class ImageUrlPathEncoder
{
    public static string EncodeRepositoryPath(string repositoryPath)
    {
        if (string.IsNullOrWhiteSpace(repositoryPath))
        {
            return string.Empty;
        }

        var normalizedPath = repositoryPath.Replace('\\', '/').TrimStart('/');
        var pathSegments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('/', pathSegments.Select(Uri.EscapeDataString));
    }

    public static string BuildRawGitHubContentUrl(string owner, string repository, string branch, string remotePath)
    {
        var encodedPath = EncodeRepositoryPath(remotePath);
        var encodedOwner = Uri.EscapeDataString(owner);
        var encodedRepository = Uri.EscapeDataString(repository);
        var encodedBranch = Uri.EscapeDataString(branch);
        return $"https://raw.githubusercontent.com/{encodedOwner}/{encodedRepository}/{encodedBranch}/{encodedPath}";
    }
}
