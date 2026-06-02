using Avalonia.Controls;
using JellybeansPicUploader.ViewModels;

namespace JellybeansPicUploader.Views.Pages;

/// <summary>
/// 页面基类：在 InitializeComponent 之前注入 ShellViewModel，确保 Command 绑定生效。
/// </summary>
public class PageHostBase : UserControl
{
    protected PageHostBase(ShellViewModel shellViewModel)
    {
        ShellViewModel = shellViewModel ?? throw new ArgumentNullException(nameof(shellViewModel));
        DataContext = shellViewModel;
    }

    protected ShellViewModel ShellViewModel { get; }
}
