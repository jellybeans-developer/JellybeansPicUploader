using JellybeansPicUploader.Infrastructure;
using JellybeansPicUploader.ViewModels;

namespace JellybeansPicUploader.Views.Pages;

public partial class SettingsPage : PageHostBase
{
    public SettingsPage() : this(XamlLoaderShellViewModel.Instance)
    {
    }

    public SettingsPage(ShellViewModel shellViewModel) : base(shellViewModel)
    {
        InitializeComponent();
    }
}
