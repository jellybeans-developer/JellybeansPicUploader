using PicXWpf.Infrastructure;

namespace PicXWpf.Models;

public enum UploadItemStatus
{
    Pending,
    Uploading,
    Uploaded,
    Failed
}

public sealed class UploadItem : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    public string LocalFilePath { get; init; } = string.Empty;

    public string OriginalFileName { get; init; } = string.Empty;

    public long FileSizeBytes { get; init; }

    private string _displayFileName = string.Empty;
    public string DisplayFileName
    {
        get => _displayFileName;
        set
        {
            if (SetProperty(ref _displayFileName, value))
            {
                OnPropertyChanged(nameof(UploadProgressOverlayText));
            }
        }
    }

    private string _remoteFileName = string.Empty;
    public string RemoteFileName
    {
        get => _remoteFileName;
        set => SetProperty(ref _remoteFileName, value);
    }

    private UploadItemStatus _status = UploadItemStatus.Pending;
    public UploadItemStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(IsUploadInProgress));
                OnPropertyChanged(nameof(UploadProgressOverlayText));
            }
        }
    }

    public bool IsUploadInProgress => Status == UploadItemStatus.Uploading;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    private int _progressPercent;
    public int ProgressPercent
    {
        get => _progressPercent;
        set
        {
            if (SetProperty(ref _progressPercent, value))
            {
                OnPropertyChanged(nameof(UploadProgressOverlayText));
            }
        }
    }

    public string UploadProgressOverlayText =>
        Status == UploadItemStatus.Uploading
            ? $"上传中 {ProgressPercent}% · {DisplayFileName}"
            : string.Empty;

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    private string? _remotePath;
    public string? RemotePath
    {
        get => _remotePath;
        set => SetProperty(ref _remotePath, value);
    }

    private string? _imageUrl;
    public string? ImageUrl
    {
        get => _imageUrl;
        set => SetProperty(ref _imageUrl, value);
    }

    private string? _gitHubPagesImageUrl;
    public string? GitHubPagesImageUrl
    {
        get => _gitHubPagesImageUrl;
        set => SetProperty(ref _gitHubPagesImageUrl, value);
    }

    private string? _contentSha;
    public string? ContentSha
    {
        get => _contentSha;
        set => SetProperty(ref _contentSha, value);
    }

    private string? _commitSha;
    public string? CommitSha
    {
        get => _commitSha;
        set => SetProperty(ref _commitSha, value);
    }

    public byte[]? ProcessedBytes { get; set; }
}
