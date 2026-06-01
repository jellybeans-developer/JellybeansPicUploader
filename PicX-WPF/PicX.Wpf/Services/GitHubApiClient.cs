using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PicXWpf.Models;

namespace PicXWpf.Services;

public sealed class GitHubApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private bool _disposed;

    public GitHubApiClient()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://api.github.com/"),
            Timeout = TimeSpan.FromMinutes(5)
        };

        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("PicX-WPF", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public void SetToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token.Trim());
    }

    public async Task<GitHubUserInfo?> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync("user", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return new GitHubUserInfo
        {
            Login = doc.RootElement.GetProperty("login").GetString() ?? string.Empty,
            Name = doc.RootElement.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
            AvatarUrl = doc.RootElement.TryGetProperty("avatar_url", out var avatar) ? avatar.GetString() ?? string.Empty : string.Empty
        };
    }

    public async Task<bool> RepositoryExistsAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}",
            cancellationToken).ConfigureAwait(false);

        return response.IsSuccessStatusCode;
    }

    public async Task<string?> GetDefaultBranchAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("default_branch", out var branch) ? branch.GetString() : null;
    }

    public async Task<CreateRepositoryResult> CreateRepositoryAsync(
        string owner,
        string repositoryName,
        string? authenticatedUserLogin,
        string description,
        bool isPrivate,
        CancellationToken cancellationToken = default)
    {
        var isOrganizationRepository = !string.IsNullOrWhiteSpace(authenticatedUserLogin) &&
            !owner.Equals(authenticatedUserLogin, StringComparison.OrdinalIgnoreCase);

        var requestUrl = isOrganizationRepository
            ? $"orgs/{EscapePathSegment(owner)}/repos"
            : "user/repos";

        var payload = new Dictionary<string, object?>
        {
            ["name"] = repositoryName,
            ["description"] = description,
            ["private"] = isPrivate,
            ["auto_init"] = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return CreateRepositoryResult.CreateSuccess(ExtractDefaultBranch(body), alreadyExisted: false);
        }

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity &&
            body.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            var defaultBranch = await GetDefaultBranchAsync(owner, repositoryName, cancellationToken).ConfigureAwait(false);
            return CreateRepositoryResult.CreateSuccess(defaultBranch, alreadyExisted: true);
        }

        return CreateRepositoryResult.CreateFailure(ExtractGitHubErrorMessage(body, response));
    }

    public async Task<UploadContentResult> InitializeRepositoryReadmeAsync(
        string owner,
        string repo,
        string branch,
        CancellationToken cancellationToken = default)
    {
        var readmeBytes = Encoding.UTF8.GetBytes(PicXGitHubConstants.InitializeRepositoryReadmeContent);
        return await UploadContentAsync(
            owner,
            repo,
            branch,
            "README.md",
            PicXGitHubConstants.InitializeRepositoryCommitMessage,
            readmeBytes,
            owner,
            null,
            cancellationToken).ConfigureAwait(false);
    }

    private static string? ExtractDefaultBranch(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("default_branch", out var branch))
            {
                return branch.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    public async Task<IReadOnlyList<string>> GetBranchNamesAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/branches", cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var names = new List<string>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty("name", out var name))
            {
                var branchName = name.GetString();
                if (!string.IsNullOrWhiteSpace(branchName))
                {
                    names.Add(branchName);
                }
            }
        }

        names.Reverse();
        return names;
    }

    public async Task<GitHubBranchInfo?> GetBranchInfoAsync(string owner, string repo, string branch, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/branches/{EscapePathSegment(branch)}",
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var commitSha = doc.RootElement.GetProperty("commit").GetProperty("sha").GetString() ?? string.Empty;
        var treeSha = doc.RootElement.GetProperty("commit").GetProperty("commit").GetProperty("tree").GetProperty("sha").GetString() ?? string.Empty;

        return new GitHubBranchInfo
        {
            Name = branch,
            HeadCommitSha = commitSha,
            BaseTreeSha = treeSha
        };
    }

    public async Task<IReadOnlyList<RepositoryContentItem>> GetDirectoryContentsAsync(
        string owner,
        string repo,
        string branch,
        string path,
        CancellationToken cancellationToken = default)
    {
        var query = $"?ref={Uri.EscapeDataString(branch)}";
        var url = string.IsNullOrWhiteSpace(path)
            ? $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/contents{query}"
            : $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/contents/{EscapeContentPath(path)}{query}";

        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<RepositoryContentItem>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            var type = element.GetProperty("type").GetString() ?? string.Empty;
            items.Add(new RepositoryContentItem
            {
                Name = element.GetProperty("name").GetString() ?? string.Empty,
                Path = element.GetProperty("path").GetString() ?? string.Empty,
                Sha = element.GetProperty("sha").GetString() ?? string.Empty,
                SizeBytes = element.TryGetProperty("size", out var size) ? size.GetInt64() : 0,
                IsDirectory = type.Equals("dir", StringComparison.OrdinalIgnoreCase)
            });
        }

        return items;
    }

    /// <summary>
    /// 递归获取仓库分支上的全部图片文件（优先 Git Tree API，失败时回退为目录遍历）。
    /// </summary>
    public async Task<IReadOnlyList<RepositoryContentItem>> GetAllRepositoryImageFilesAsync(
        string owner,
        string repo,
        string branch,
        CancellationToken cancellationToken = default)
    {
        var branchInfo = await GetBranchInfoAsync(owner, repo, branch, cancellationToken).ConfigureAwait(false);
        if (branchInfo is null || string.IsNullOrWhiteSpace(branchInfo.BaseTreeSha))
        {
            return await CollectRepositoryImageFilesByDirectoryWalkAsync(owner, repo, branch, string.Empty, cancellationToken)
                .ConfigureAwait(false);
        }

        var treeImages = await TryCollectImageFilesFromGitTreeAsync(owner, repo, branchInfo.BaseTreeSha, cancellationToken)
            .ConfigureAwait(false);
        if (treeImages.Count > 0)
        {
            return treeImages;
        }

        return await CollectRepositoryImageFilesByDirectoryWalkAsync(owner, repo, branch, string.Empty, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<RepositoryContentItem>> TryCollectImageFilesFromGitTreeAsync(
        string owner,
        string repo,
        string treeSha,
        CancellationToken cancellationToken)
    {
        var url = $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/git/trees/{Uri.EscapeDataString(treeSha)}?recursive=1";
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("truncated", out var truncated) && truncated.GetBoolean())
        {
            return [];
        }

        if (!doc.RootElement.TryGetProperty("tree", out var tree) || tree.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var images = new List<RepositoryContentItem>();
        foreach (var element in tree.EnumerateArray())
        {
            var type = element.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            if (!string.Equals(type, "blob", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var path = element.TryGetProperty("path", out var pathElement) ? pathElement.GetString() : null;
            if (string.IsNullOrWhiteSpace(path) || !ImageFileHelper.IsImageFile(path))
            {
                continue;
            }

            images.Add(new RepositoryContentItem
            {
                Name = Path.GetFileName(path),
                Path = path,
                Sha = element.TryGetProperty("sha", out var shaElement) ? shaElement.GetString() ?? string.Empty : string.Empty,
                SizeBytes = element.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0,
                IsDirectory = false
            });
        }

        return images;
    }

    private async Task<IReadOnlyList<RepositoryContentItem>> CollectRepositoryImageFilesByDirectoryWalkAsync(
        string owner,
        string repo,
        string branch,
        string directoryPath,
        CancellationToken cancellationToken)
    {
        var images = new List<RepositoryContentItem>();
        await CollectRepositoryImageFilesInDirectoryAsync(owner, repo, branch, directoryPath, images, cancellationToken)
            .ConfigureAwait(false);
        return images;
    }

    private async Task CollectRepositoryImageFilesInDirectoryAsync(
        string owner,
        string repo,
        string branch,
        string directoryPath,
        List<RepositoryContentItem> images,
        CancellationToken cancellationToken)
    {
        var contents = await GetDirectoryContentsAsync(owner, repo, branch, directoryPath, cancellationToken).ConfigureAwait(false);
        foreach (var item in contents)
        {
            if (item.IsDirectory)
            {
                await CollectRepositoryImageFilesInDirectoryAsync(owner, repo, branch, item.Path, images, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (ImageFileHelper.IsImageFile(item.Name))
            {
                images.Add(item);
            }
        }
    }

    public async Task<string?> GetContentShaAsync(
        string owner,
        string repo,
        string branch,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        var query = $"?ref={Uri.EscapeDataString(branch)}";
        var url = $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/contents/{EscapeContentPath(remotePath)}{query}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("sha", out var sha))
            {
                return sha.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    public async Task<UploadContentResult> UploadContentAsync(
        string owner,
        string repo,
        string branch,
        string remotePath,
        string commitMessage,
        byte[] fileBytes,
        string? committerName,
        string? committerEmail,
        CancellationToken cancellationToken = default,
        Action<GitHubContentUploadPhase>? reportUploadPhase = null)
    {
        reportUploadPhase?.Invoke(GitHubContentUploadPhase.CheckingRemoteFile);
        var existingSha = await GetContentShaAsync(owner, repo, branch, remotePath, cancellationToken).ConfigureAwait(false);

        reportUploadPhase?.Invoke(GitHubContentUploadPhase.EncodingFileContent);
        var base64Content = await Task.Run(() => Convert.ToBase64String(fileBytes), cancellationToken).ConfigureAwait(false);

        var payload = new Dictionary<string, object?>
        {
            ["message"] = string.IsNullOrWhiteSpace(commitMessage) ? "Upload by PicX-WPF" : commitMessage,
            ["content"] = base64Content,
            ["branch"] = branch
        };

        if (!string.IsNullOrWhiteSpace(existingSha))
        {
            payload["sha"] = existingSha;
        }

        var authorName = string.IsNullOrWhiteSpace(committerName) ? owner : committerName;
        if (!string.IsNullOrWhiteSpace(committerEmail))
        {
            payload["committer"] = new { name = authorName, email = committerEmail };
            payload["author"] = new { name = authorName, email = committerEmail };
        }

        reportUploadPhase?.Invoke(GitHubContentUploadPhase.SubmittingToRepository);
        var result = await SendContentsRequestAsync(
            HttpMethod.Put,
            owner,
            repo,
            remotePath,
            payload,
            cancellationToken).ConfigureAwait(false);

        if (!result.Success && existingSha is null &&
            result.ErrorMessage?.Contains("sha", StringComparison.OrdinalIgnoreCase) == true)
        {
            var retrySha = await GetContentShaAsync(owner, repo, branch, remotePath, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(retrySha))
            {
                payload["sha"] = retrySha;
                result = await SendContentsRequestAsync(
                    HttpMethod.Put,
                    owner,
                    repo,
                    remotePath,
                    payload,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return result;
    }

    public async Task<DeleteContentResult> DeleteContentAsync(
        string owner,
        string repo,
        string branch,
        string remotePath,
        string sha,
        string commitMessage,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            message = string.IsNullOrWhiteSpace(commitMessage) ? "Delete by PicX-WPF" : commitMessage,
            sha,
            branch
        };

        using var request = CreateContentsRequest(HttpMethod.Delete, owner, repo, remotePath, payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return new DeleteContentResult(true, null);
        }

        return new DeleteContentResult(false, ExtractGitHubErrorMessage(body, response));
    }

    public async Task<string?> CreateBlobAsync(string owner, string repo, byte[] fileBytes, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            content = Convert.ToBase64String(fileBytes),
            encoding = "base64"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/git/blobs")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("sha").GetString();
    }

    public async Task<BatchGitOperationResult> UploadMultipleAsync(
        string owner,
        string repo,
        string branch,
        string commitMessage,
        IReadOnlyList<(string RemotePath, byte[] FileBytes)> files,
        CancellationToken cancellationToken = default,
        Action<BatchGitUploadPhase, int, int>? reportBatchUploadPhase = null)
    {
        reportBatchUploadPhase?.Invoke(BatchGitUploadPhase.FetchingBranchInfo, 0, files.Count);
        var branchInfo = await GetBranchInfoAsync(owner, repo, branch, cancellationToken).ConfigureAwait(false);
        if (branchInfo is null)
        {
            return BatchGitOperationResult.CreateFailure("获取分支信息失败");
        }

        var blobEntries = new List<(string RemotePath, string BlobSha)>();
        for (var blobIndex = 0; blobIndex < files.Count; blobIndex++)
        {
            var file = files[blobIndex];
            cancellationToken.ThrowIfCancellationRequested();
            reportBatchUploadPhase?.Invoke(BatchGitUploadPhase.CreatingBlob, blobIndex, files.Count);
            var blobSha = await CreateBlobAsync(owner, repo, file.FileBytes, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(blobSha))
            {
                return BatchGitOperationResult.CreateFailure($"创建 Blob 失败：{file.RemotePath}");
            }

            blobEntries.Add((file.RemotePath, blobSha));
        }

        reportBatchUploadPhase?.Invoke(BatchGitUploadPhase.CreatingTree, files.Count, files.Count);
        var treeSha = await CreateTreeAsync(owner, repo, branchInfo.BaseTreeSha, blobEntries.Select(x => (x.RemotePath, x.BlobSha, false)).ToList(), cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(treeSha))
        {
            return BatchGitOperationResult.CreateFailure("创建 Tree 失败");
        }

        reportBatchUploadPhase?.Invoke(BatchGitUploadPhase.CreatingCommit, files.Count, files.Count);
        var commitSha = await CreateCommitAsync(owner, repo, treeSha, branchInfo.HeadCommitSha, commitMessage, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(commitSha))
        {
            return BatchGitOperationResult.CreateFailure("创建 Commit 失败");
        }

        reportBatchUploadPhase?.Invoke(BatchGitUploadPhase.UpdatingBranchReference, files.Count, files.Count);
        var updated = await UpdateBranchRefAsync(owner, repo, branch, commitSha, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return BatchGitOperationResult.CreateFailure("更新分支引用失败");
        }

        return BatchGitOperationResult.CreateSuccess(blobEntries.Select(x => x.RemotePath).ToList(), commitSha);
    }

    public async Task<bool> CreateOrUpdateTagReferenceAsync(
        string owner,
        string repo,
        string tagName,
        string commitSha,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tagName) || string.IsNullOrWhiteSpace(commitSha))
        {
            return false;
        }

        var normalizedTagName = tagName.Trim().TrimStart('/');
        var tagRef = normalizedTagName.StartsWith("refs/tags/", StringComparison.OrdinalIgnoreCase)
            ? normalizedTagName
            : $"refs/tags/{normalizedTagName}";

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/git/refs")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { @ref = tagRef, sha = commitSha }),
                Encoding.UTF8,
                "application/json")
        };

        using var createResponse = await _httpClient.SendAsync(createRequest, cancellationToken).ConfigureAwait(false);
        if (createResponse.IsSuccessStatusCode)
        {
            return true;
        }

        if (createResponse.StatusCode != HttpStatusCode.UnprocessableEntity
            && createResponse.StatusCode != HttpStatusCode.Conflict
            && createResponse.StatusCode != HttpStatusCode.UnprocessableContent)
        {
            return false;
        }

        var tagRefPath = tagRef["refs/tags/".Length..];
        using var updateRequest = new HttpRequestMessage(HttpMethod.Patch, $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/git/refs/tags/{EscapePathSegment(tagRefPath)}")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { sha = commitSha, force = true }),
                Encoding.UTF8,
                "application/json")
        };

        using var updateResponse = await _httpClient.SendAsync(updateRequest, cancellationToken).ConfigureAwait(false);
        return updateResponse.IsSuccessStatusCode;
    }

    public async Task<BatchGitOperationResult> DeleteMultipleAsync(
        string owner,
        string repo,
        string branch,
        string commitMessage,
        IReadOnlyList<string> remotePaths,
        CancellationToken cancellationToken = default)
    {
        var branchInfo = await GetBranchInfoAsync(owner, repo, branch, cancellationToken).ConfigureAwait(false);
        if (branchInfo is null)
        {
            return BatchGitOperationResult.CreateFailure("获取分支信息失败");
        }

        var deleteEntries = remotePaths.Select(path => (path, string.Empty, true)).ToList();
        var treeSha = await CreateTreeAsync(owner, repo, branchInfo.BaseTreeSha, deleteEntries, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(treeSha))
        {
            return BatchGitOperationResult.CreateFailure("创建 Tree 失败");
        }

        var commitSha = await CreateCommitAsync(owner, repo, treeSha, branchInfo.HeadCommitSha, commitMessage, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(commitSha))
        {
            return BatchGitOperationResult.CreateFailure("创建 Commit 失败");
        }

        var updated = await UpdateBranchRefAsync(owner, repo, branch, commitSha, cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return BatchGitOperationResult.CreateFailure("更新分支引用失败");
        }

        return BatchGitOperationResult.CreateSuccess(remotePaths.ToList());
    }

    public async Task<bool> DeployGitHubPagesBranchAsync(string owner, string repo, string sourceBranch, CancellationToken cancellationToken = default)
    {
        const string ghPagesBranch = "gh-pages";
        var branches = await GetBranchNamesAsync(owner, repo, cancellationToken).ConfigureAwait(false);
        if (branches.Contains(ghPagesBranch, StringComparer.OrdinalIgnoreCase))
        {
            using var deleteResponse = await _httpClient.DeleteAsync(
                $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/git/refs/heads/{ghPagesBranch}",
                cancellationToken).ConfigureAwait(false);

            if (!deleteResponse.IsSuccessStatusCode)
            {
                return false;
            }
        }

        using var refResponse = await _httpClient.GetAsync(
            $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/git/refs/heads/{EscapePathSegment(sourceBranch)}",
            cancellationToken).ConfigureAwait(false);

        if (!refResponse.IsSuccessStatusCode)
        {
            return false;
        }

        var refJson = await refResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var refDoc = JsonDocument.Parse(refJson);
        var sha = refDoc.RootElement.GetProperty("object").GetProperty("sha").GetString();
        if (string.IsNullOrWhiteSpace(sha))
        {
            return false;
        }

        var payload = new
        {
            @ref = $"refs/heads/{ghPagesBranch}",
            sha
        };

        using var createResponse = await _httpClient.PostAsync(
            $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/git/refs",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            cancellationToken).ConfigureAwait(false);

        return createResponse.IsSuccessStatusCode;
    }

    private async Task<string?> CreateTreeAsync(
        string owner,
        string repo,
        string baseTreeSha,
        IReadOnlyList<(string Path, string Sha, bool IsDelete)> entries,
        CancellationToken cancellationToken)
    {
        var treeItems = entries.Select(entry => new Dictionary<string, object?>
        {
            ["path"] = entry.Path,
            ["mode"] = "100644",
            ["type"] = "blob",
            ["sha"] = entry.IsDelete ? null : entry.Sha
        }).ToList();

        var payload = new Dictionary<string, object?>
        {
            ["base_tree"] = baseTreeSha,
            ["tree"] = treeItems
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/git/trees")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("sha").GetString();
    }

    private async Task<string?> CreateCommitAsync(
        string owner,
        string repo,
        string treeSha,
        string parentCommitSha,
        string commitMessage,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            message = string.IsNullOrWhiteSpace(commitMessage) ? "Batch operation by PicX-WPF" : commitMessage,
            tree = treeSha,
            parents = new[] { parentCommitSha }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/git/commits")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("sha").GetString();
    }

    private async Task<bool> UpdateBranchRefAsync(string owner, string repo, string branch, string commitSha, CancellationToken cancellationToken)
    {
        var payload = new { sha = commitSha, force = false };
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/git/refs/heads/{EscapePathSegment(branch)}")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    private async Task<UploadContentResult> SendContentsRequestAsync(
        HttpMethod method,
        string owner,
        string repo,
        string remotePath,
        object payload,
        CancellationToken cancellationToken)
    {
        using var request = CreateContentsRequest(method, owner, repo, remotePath, payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return new UploadContentResult(
                true,
                null,
                ExtractContentPath(body),
                ExtractContentSha(body),
                ExtractCommitSha(body));
        }

        return new UploadContentResult(false, ExtractGitHubErrorMessage(body, response), null, null, null);
    }

    private static HttpRequestMessage CreateContentsRequest(HttpMethod method, string owner, string repo, string remotePath, object payload)
    {
        return new HttpRequestMessage(method, $"repos/{EscapePathSegment(owner)}/{EscapePathSegment(repo)}/contents/{EscapeContentPath(remotePath)}")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    private static string EscapePathSegment(string value) => Uri.EscapeDataString(value);

    private static string EscapeContentPath(string remotePath)
    {
        var parts = remotePath.Replace("\\", "/").Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("/", parts.Select(Uri.EscapeDataString));
    }

    private static string? ExtractContentPath(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("content", out var content) &&
                content.TryGetProperty("path", out var path))
            {
                return path.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string? ExtractContentSha(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("content", out var content) &&
                content.TryGetProperty("sha", out var sha))
            {
                return sha.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string? ExtractCommitSha(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("commit", out var commit) &&
                commit.TryGetProperty("sha", out var sha))
            {
                return sha.GetString();
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string ExtractGitHubErrorMessage(string body, HttpResponseMessage response)
    {
        var message = TryExtractMessageFromGitHubError(body) ?? $"GitHub API 错误：{(int)response.StatusCode} {response.ReasonPhrase}";
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            message += "（请检查 Token 是否包含 repo 权限、是否过期、或是否触发速率限制）";
        }

        return message;
    }

    private static string? TryExtractMessageFromGitHubError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var parts = new List<string>();

            if (doc.RootElement.TryGetProperty("message", out var message))
            {
                var text = message.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(text);
                }
            }

            if (doc.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    if (error.TryGetProperty("message", out var errorMessage))
                    {
                        var text = errorMessage.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            parts.Add(text);
                        }
                    }
                }
            }

            if (parts.Count > 0)
            {
                return string.Join("；", parts.Distinct());
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _httpClient.Dispose();
    }
}

public sealed record UploadContentResult(
    bool Success,
    string? ErrorMessage,
    string? RemotePath,
    string? ContentSha,
    string? CommitSha);

public sealed record CreateRepositoryResult(bool Success, string? ErrorMessage, string? DefaultBranch, bool AlreadyExisted)
{
    public static CreateRepositoryResult CreateSuccess(string? defaultBranch, bool alreadyExisted) =>
        new(true, null, defaultBranch, alreadyExisted);

    public static CreateRepositoryResult CreateFailure(string errorMessage) =>
        new(false, errorMessage, null, false);
}

public sealed record DeleteContentResult(bool Success, string? ErrorMessage);

public sealed class BatchGitOperationResult
{
    public bool Success { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<string> AffectedPaths { get; init; } = [];

    public string? CommitSha { get; init; }

    public static BatchGitOperationResult CreateSuccess(IReadOnlyList<string> paths, string? commitSha = null) =>
        new() { Success = true, AffectedPaths = paths, CommitSha = commitSha };

    public static BatchGitOperationResult CreateFailure(string message) =>
        new() { Success = false, ErrorMessage = message };
}
