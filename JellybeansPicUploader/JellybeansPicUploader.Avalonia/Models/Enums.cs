namespace JellybeansPicUploader.Models;

public enum CompressEncoder
{
    WebP,
    MozJpeg,
    Avif
}

public enum WatermarkPosition
{
    LeftTop,
    LeftBottom,
    RightTop,
    RightBottom
}

public enum DirectoryMode
{
    Root,
    Date,
    Repository,
    NewDirectory
}

public enum ImageLinkFormatType
{
    Plain,
    Markdown,
    Html,
    BbCode,
    Custom
}

public enum LoginMode
{
    ManualToken,
    OAuth
}

/// <summary>
/// 上传完成后生成图片链接的方式
/// </summary>
public enum PostUploadPublishMode
{
    GitHubPages,
    JsDelivr
}

/// <summary>
/// jsDelivr 链接中的版本引用：分支名易缓存旧图；Commit Hash 每次上传为新链接；Tag 可固定 URL 并在上传后移动标签
/// </summary>
public enum JsDelivrVersionReferenceMode
{
    Branch,
    CommitHash,
    Tag
}

/// <summary>
/// 单文件 Contents API 上传过程中的阶段，用于驱动进度条与状态文案。
/// </summary>
public enum GitHubContentUploadPhase
{
    CheckingRemoteFile,
    EncodingFileContent,
    SubmittingToRepository
}

/// <summary>
/// 批量 Git 树提交上传过程中的阶段。
/// </summary>
public enum BatchGitUploadPhase
{
    FetchingBranchInfo,
    CreatingBlob,
    CreatingTree,
    CreatingCommit,
    UpdatingBranchReference
}
