using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JellybeansPicUploader.Services;

namespace JellybeansPicUploader;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;

        base.OnFrameworkInitializationCompleted();
    }

    private static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        DesktopDialogService.ShowErrorMessage($"程序发生错误：{eventArgs.Exception.Message}");
        eventArgs.Handled = true;
    }

    private static void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is Exception exception)
        {
            Dispatcher.UIThread.Post(() =>
                DesktopDialogService.ShowErrorMessage($"程序发生严重错误：{exception.Message}"));
        }
    }
}
