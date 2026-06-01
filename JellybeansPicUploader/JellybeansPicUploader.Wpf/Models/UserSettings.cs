namespace JellybeansPicUploader.Models;

public sealed class UserSettings
{
    public ImageNameSettings ImageName { get; set; } = new();
    public CompressSettings Compress { get; set; } = new();
    public ImageLinkTypeSettings ImageLinkType { get; set; } = ImageLinkTypeSettings.CreateDefault();
    public ImageLinkFormatSettings ImageLinkFormat { get; set; } = ImageLinkFormatSettings.CreateDefault();
    public WatermarkSettings Watermark { get; set; } = new();
    public bool AutoCopyLinkAfterUpload { get; set; } = true;

    /// <summary>
    /// 上传完成后使用 GitHub Pages 或 jsDelivr 生成可访问链接
    /// </summary>
    public PostUploadPublishMode PostUploadPublishMode { get; set; } = PostUploadPublishMode.GitHubPages;

    /// <summary>
    /// jsDelivr 上传后链接的版本引用方式（仅 PostUploadPublishMode.JsDelivr 时生效）
    /// </summary>
    public JsDelivrVersionReferenceMode JsDelivrVersionReferenceMode { get; set; } = JsDelivrVersionReferenceMode.CommitHash;

    /// <summary>
    /// Tag 模式下的标签名（不含 refs/tags/ 前缀），上传成功后会移动该标签到最新提交
    /// </summary>
    public string JsDelivrTagName { get; set; } = "picx-latest";
}

public sealed class ImageNameSettings
{
    public bool EnableHash { get; set; } = true;

    public bool EnablePrefix { get; set; }

    public string Prefix { get; set; } = string.Empty;
}

public sealed class CompressSettings
{
    public bool Enable { get; set; } = true;

    public CompressEncoder Encoder { get; set; } = CompressEncoder.WebP;
}

public sealed class WatermarkSettings
{
    public bool Enable { get; set; }

    public string Text { get; set; } = "JellybeansPicUploader";

    public int FontSize { get; set; } = 50;

    public WatermarkPosition Position { get; set; } = WatermarkPosition.RightBottom;

    public string TextColorHex { get; set; } = "#FFFFFF";

    public double Opacity { get; set; } = 0.5;
}

public sealed class ImageLinkRule
{
    public string Name { get; set; } = string.Empty;

    public string RuleTemplate { get; set; } = string.Empty;

    public bool IsCustom { get; set; }
}

public sealed class ImageLinkTypeSettings
{
    public string SelectedRuleName { get; set; } = "GitHub";

    public List<ImageLinkRule> PresetRules { get; set; } = [];

    public static ImageLinkTypeSettings CreateDefault()
    {
        return new ImageLinkTypeSettings
        {
            SelectedRuleName = "GitHub",
            PresetRules =
            [
                new ImageLinkRule { Name = "GitHubPages", RuleTemplate = "https://{{owner}}.github.io/{{repo}}/{{path}}" },
                new ImageLinkRule { Name = "GitHub", RuleTemplate = "https://github.com/{{owner}}/{{repo}}/raw/{{branch}}/{{path}}" },
                new ImageLinkRule { Name = "jsDelivr", RuleTemplate = "https://cdn.jsdelivr.net/gh/{{owner}}/{{repo}}@{{branch}}/{{path}}" },
                new ImageLinkRule { Name = "Statically", RuleTemplate = "https://cdn.statically.io/gh/{{owner}}/{{repo}}@{{branch}}/{{path}}" },
                new ImageLinkRule { Name = "ChinaJsDelivr", RuleTemplate = "https://jsd.cdn.zzko.cn/gh/{{owner}}/{{repo}}@{{branch}}/{{path}}" }
            ]
        };
    }
}

public sealed class ImageLinkFormatSettings
{
    public bool Enable { get; set; }

    public ImageLinkFormatType SelectedFormat { get; set; } = ImageLinkFormatType.Markdown;

    /// <summary>
    /// 自定义链接格式模板，占位符：imageLink、imageName
    /// </summary>
    public string CustomFormatTemplate { get; set; } = "![imageName](imageLink)";

    public static ImageLinkFormatSettings CreateDefault() => new();
}
