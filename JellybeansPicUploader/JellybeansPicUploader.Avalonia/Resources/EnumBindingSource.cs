using JellybeansPicUploader.Models;

namespace JellybeansPicUploader.Resources;

/// <summary>
/// 枚举列表绑定源（替代 WPF ObjectDataProvider）。
/// </summary>
public static class EnumBindingSource
{
    public static Array DirectoryModeValues => Enum.GetValues<DirectoryMode>();

    public static Array CompressEncoderValues => Enum.GetValues<CompressEncoder>();

    public static Array WatermarkPositionValues => Enum.GetValues<WatermarkPosition>();

    public static Array ImageLinkFormatValues => Enum.GetValues<ImageLinkFormatType>();

    public static Array PostUploadPublishModeValues => Enum.GetValues<PostUploadPublishMode>();

    public static Array JsDelivrVersionReferenceModeValues => Enum.GetValues<JsDelivrVersionReferenceMode>();
}
