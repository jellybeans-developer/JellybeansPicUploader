using System.Windows;
using System.Windows.Threading;
using Wpf.Ui.Appearance;

namespace PicXWpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;

        base.OnStartup(e);
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"程序发生错误：{e.Exception.Message}",
            "PicX-WPF",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            MessageBox.Show(
                $"程序发生严重错误：{exception.Message}",
                "PicX-WPF",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
