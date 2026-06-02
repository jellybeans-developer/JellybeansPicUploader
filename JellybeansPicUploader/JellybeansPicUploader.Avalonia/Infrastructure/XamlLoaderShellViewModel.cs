using JellybeansPicUploader.ViewModels;

namespace JellybeansPicUploader.Infrastructure;

/// <summary>
/// 供 Avalonia XAML 加载器使用的 ShellViewModel 占位；运行时应由 MainWindow 注入真实实例。
/// </summary>
internal static class XamlLoaderShellViewModel
{
    private static ShellViewModel? _sharedInstance;

    public static ShellViewModel Instance => _sharedInstance ??= new ShellViewModel();
}
