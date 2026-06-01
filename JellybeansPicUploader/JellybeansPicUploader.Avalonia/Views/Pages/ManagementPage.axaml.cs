using JellybeansPicUploader.Infrastructure;
using JellybeansPicUploader.ViewModels;

namespace JellybeansPicUploader.Views.Pages;

public partial class ManagementPage : PageHostBase
{
    public ManagementPage() : this(XamlLoaderShellViewModel.Instance)
    {
    }

    public ManagementPage(ShellViewModel shellViewModel) : base(shellViewModel)
    {
        InitializeComponent();
    }
}
