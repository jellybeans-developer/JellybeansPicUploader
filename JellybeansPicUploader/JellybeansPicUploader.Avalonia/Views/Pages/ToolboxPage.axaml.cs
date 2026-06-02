using JellybeansPicUploader.Infrastructure;
using JellybeansPicUploader.ViewModels;

namespace JellybeansPicUploader.Views.Pages;

public partial class ToolboxPage : PageHostBase
{
    public ToolboxPage() : this(XamlLoaderShellViewModel.Instance)
    {
    }

    public ToolboxPage(ShellViewModel shellViewModel) : base(shellViewModel)
    {
        InitializeComponent();
    }
}
