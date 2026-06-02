using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using JellybeansPicUploader.Views.Dialogs;

namespace JellybeansPicUploader.Services;

/// <summary>
/// 桌面文件选择与文本输入对话框。
/// </summary>
public static class DesktopDialogService
{
    public static async Task<IReadOnlyList<string>?> PickImageFilesAsync(bool allowMultiple = true, Visual? owner = null)
    {
        var topLevel = ResolveTopLevel(owner);
        if (topLevel is null)
        {
            return null;
        }

        if (!topLevel.StorageProvider.CanOpen)
        {
            throw new InvalidOperationException("当前环境不支持打开文件选择器");
        }

        await EnsureUiThreadAsync().ConfigureAwait(true);

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = allowMultiple ? "选择图片（可多选）" : "选择图片",
                AllowMultiple = allowMultiple,
                FileTypeFilter =
                [
                    new FilePickerFileType("图片文件")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.gif", "*.webp", "*.bmp", "*.avif"]
                    }
                ]
            }).ConfigureAwait(true);

            await RestoreMainWindowAfterFileDialogAsync(topLevel).ConfigureAwait(true);

            if (files.Count == 0)
            {
                return null;
            }

            return files
                .Select(file => file.TryGetLocalPath())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Cast<string>()
                .ToList();
        }
        catch (Exception exception)
        {
            await RestoreMainWindowAfterFileDialogAsync(topLevel).ConfigureAwait(true);
            throw new InvalidOperationException($"打开文件选择器失败：{exception.Message}", exception);
        }
    }

    public static async Task<string?> PromptTextAsync(string prompt, string defaultText = "", Visual? owner = null)
    {
        var mainWindow = ResolveMainWindow(owner);
        if (mainWindow is null)
        {
            return null;
        }

        var dialog = new TextPromptDialog(prompt, defaultText);
        return await dialog.ShowDialogAsync(mainWindow);
    }

    public static void ShowErrorMessage(string message, Visual? owner = null)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            _ = ShowErrorMessageAsync(message, owner);
            return;
        }

        Dispatcher.UIThread.Post(() => _ = ShowErrorMessageAsync(message, owner));
    }

    private static async Task ShowErrorMessageAsync(string message, Visual? owner)
    {
        var mainWindow = ResolveMainWindow(owner);
        if (mainWindow is null)
        {
            return;
        }

        var dialog = new Window
        {
            Title = "JellybeansPicUploader",
            Width = 480,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new Avalonia.Media.SolidColorBrush(0xFF2D2D30),
            Foreground = new Avalonia.Media.SolidColorBrush(0xFFE8E8E8),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Foreground = new Avalonia.Media.SolidColorBrush(0xFFE8E8E8)
                    },
                    new Button
                    {
                        Content = "确定",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Command = new Infrastructure.RelayCommand(() => { })
                    }
                }
            }
        };

        if (dialog.Content is StackPanel panel && panel.Children[1] is Button okButton)
        {
            okButton.Command = new Infrastructure.RelayCommand(() => dialog.Close());
        }

        await dialog.ShowDialog(mainWindow);
        await RestoreMainWindowAfterFileDialogAsync(mainWindow).ConfigureAwait(true);
    }

    /// <summary>
    /// 缓解 Windows 上 StorageProvider 关闭后未向主窗口发送 WM_ENABLE 导致假死的问题。
    /// </summary>
    private static async Task RestoreMainWindowAfterFileDialogAsync(TopLevel topLevel)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await Task.Delay(150).ConfigureAwait(true);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (topLevel is Window window)
            {
                window.IsEnabled = true;
                window.Activate();
                window.Focus();
            }
        });
    }

    private static async Task EnsureUiThreadAsync()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { });
        }
    }

    private static TopLevel? ResolveTopLevel(Visual? owner)
    {
        if (owner is not null)
        {
            var fromOwner = TopLevel.GetTopLevel(owner);
            if (fromOwner is not null)
            {
                return fromOwner;
            }
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow is TopLevel mainWindow)
        {
            return mainWindow;
        }

        return null;
    }

    private static Window? ResolveMainWindow(Visual? owner)
    {
        if (owner is not null)
        {
            var topLevel = TopLevel.GetTopLevel(owner);
            if (topLevel is Window window)
            {
                return window;
            }
        }

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }
}
