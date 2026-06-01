using System.Windows;
using System.Windows.Controls;

namespace JellybeansPicUploader.Views.Pages;

/// <summary>
/// 页面基类：自动继承主窗口的 DataContext（ShellViewModel）
/// </summary>
public class PageHostBase : UserControl
{
    protected PageHostBase()
    {
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is null && Application.Current.MainWindow?.DataContext is not null)
        {
            DataContext = Application.Current.MainWindow.DataContext;
        }
    }
}
