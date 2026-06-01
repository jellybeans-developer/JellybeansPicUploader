using System.Collections.ObjectModel;
using System.Windows.Media;
using PicXWpf.Infrastructure;

namespace PicXWpf.Models;

public sealed class ManagedImageItem : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string DirectoryPath { get; init; } = string.Empty;

    public string Sha { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private bool _isDeleting;
    public bool IsDeleting
    {
        get => _isDeleting;
        set => SetProperty(ref _isDeleting, value);
    }

    private string? _imageUrl;
    public string? ImageUrl
    {
        get => _imageUrl;
        set => SetProperty(ref _imageUrl, value);
    }

    private ImageSource? _thumbnailSource;
    public ImageSource? ThumbnailSource
    {
        get => _thumbnailSource;
        set => SetProperty(ref _thumbnailSource, value);
    }

    private bool _isThumbnailLoading;
    public bool IsThumbnailLoading
    {
        get => _isThumbnailLoading;
        set => SetProperty(ref _isThumbnailLoading, value);
    }
}

public sealed class DirectoryTreeNode : ObservableObject
{
    public string Name { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public ObservableCollection<DirectoryTreeNode> Children { get; } = [];

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsLoaded { get; set; }
}

public sealed class RepositoryContentItem
{
    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string Sha { get; init; } = string.Empty;

    public long SizeBytes { get; init; }

    public bool IsDirectory { get; init; }
}

public sealed class GitHubUserInfo
{
    public string Login { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string AvatarUrl { get; init; } = string.Empty;
}

public sealed class GitHubBranchInfo
{
    public string Name { get; init; } = string.Empty;

    public string HeadCommitSha { get; init; } = string.Empty;

    public string BaseTreeSha { get; init; } = string.Empty;
}
