using JellybeansPicUploader.Infrastructure;
using JellybeansPicUploader.ViewModels;

namespace JellybeansPicUploader.Views.Pages;

public partial class LoginConfigPage : PageHostBase
{
    public LoginConfigPage() : this(XamlLoaderShellViewModel.Instance)
    {
    }

    public LoginConfigPage(ShellViewModel shellViewModel) : base(shellViewModel)
    {
        InitializeComponent();
    }
}
