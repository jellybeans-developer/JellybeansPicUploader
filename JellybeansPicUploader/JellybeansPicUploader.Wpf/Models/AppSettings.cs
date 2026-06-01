namespace JellybeansPicUploader.Models;

public sealed class AppSettings
{
    public string GitHubToken { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;

    public string Repository { get; set; } = string.Empty;

    public string? Branch { get; set; }

    public string Email { get; set; } = string.Empty;

    public string TargetDirectoryPath { get; set; } = "images";

    public DirectoryMode DirectoryMode { get; set; } = DirectoryMode.NewDirectory;

    public string CommitMessage { get; set; } = "Upload images by JellybeansPicUploader";

    public LoginMode LoginMode { get; set; } = LoginMode.ManualToken;

    public GitHubAuthorizationInfo Authorization { get; set; } = new();

    public UserSettings UserSettings { get; set; } = new();

    public string? LastViewedDirectoryPath { get; set; }
}

public sealed class GitHubAuthorizationInfo
{
    public bool IsAuthorized { get; set; }

    public bool? IsAppInstalled { get; set; }

    public string OAuthCode { get; set; } = string.Empty;

    public long OAuthCodeCreateTime { get; set; }

    public long TokenCreateTime { get; set; }

    public string InstallationId { get; set; } = string.Empty;

    public string ManualToken { get; set; } = string.Empty;

    public bool IsAutoAuthorize { get; set; }
}
