using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using PicXWpf.ViewModels;
using PicXWpf.Views.QuickUpload;

namespace PicXWpf.Services;

/// <summary>
/// 主窗口最小化时，在屏幕右下角显示贴边「+」便签，并管理快捷上传面板。
/// </summary>
public sealed class QuickUploadDockCoordinator : IDisposable
{
    private readonly Window _mainWindow;
    private readonly ShellViewModel _shellViewModel;
    private QuickUploadDockWindow? _dockWindow;
    private QuickUploadPanelWindow? _panelWindow;
    private QuickUploadFeedbackWindow? _feedbackWindow;
    private DispatcherTimer? _feedbackHideTimer;
    private bool _isDisposed;
    private bool _wasMainWindowInTaskbarBeforeMinimize;
    private bool _isQuickUploadSessionActive;

    public QuickUploadDockCoordinator(Window mainWindow, ShellViewModel shellViewModel)
    {
        _mainWindow = mainWindow;
        _shellViewModel = shellViewModel;
        _mainWindow.StateChanged += MainWindow_OnStateChanged;
        _mainWindow.Closed += MainWindow_OnClosed;
        SystemParameters.StaticPropertyChanged += SystemParameters_OnStaticPropertyChanged;
        UpdateDockVisibility();
    }

    /// <summary>
    /// 由主窗口 OnStateChanged 显式调用，确保 WPF-UI 标题栏最小化也能触发。
    /// </summary>
    public void NotifyMainWindowStateChanged() => UpdateDockVisibility();

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _mainWindow.StateChanged -= MainWindow_OnStateChanged;
        _mainWindow.Closed -= MainWindow_OnClosed;
        SystemParameters.StaticPropertyChanged -= SystemParameters_OnStaticPropertyChanged;
        RestoreMainWindowTaskbarVisibility();
        ClosePanel();
        CloseDock();
        CloseFeedback();
    }

    private void MainWindow_OnStateChanged(object? sender, EventArgs eventArgs) => UpdateDockVisibility();

    private void MainWindow_OnClosed(object? sender, EventArgs eventArgs) => Dispose();

    private void SystemParameters_OnStaticPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(SystemParameters.WorkArea) or nameof(SystemParameters.PrimaryScreenWidth))
        {
            RepositionVisibleWindows();
        }
    }

    private bool IsMainWindowMinimized => _mainWindow.WindowState == WindowState.Minimized;

    private void UpdateDockVisibility()
    {
        if (IsMainWindowMinimized)
        {
            EnterMinimizedQuickUploadMode();
            return;
        }

        ExitMinimizedQuickUploadMode();
    }

    private void EnterMinimizedQuickUploadMode()
    {
        if (!_mainWindow.ShowInTaskbar)
        {
            ShowDock();
            return;
        }

        _wasMainWindowInTaskbarBeforeMinimize = _mainWindow.ShowInTaskbar;
        _mainWindow.ShowInTaskbar = false;
        ShowDock();
    }

    private void ExitMinimizedQuickUploadMode()
    {
        ClosePanel();
        CloseDock();
        RestoreMainWindowTaskbarVisibility();
    }

    private void RestoreMainWindowTaskbarVisibility()
    {
        if (_wasMainWindowInTaskbarBeforeMinimize || _mainWindow.WindowState != WindowState.Minimized)
        {
            _mainWindow.ShowInTaskbar = true;
        }
    }

    private void ShowDock()
    {
        _dockWindow ??= CreateDockWindow();

        _mainWindow.Dispatcher.BeginInvoke(() =>
        {
            if (_isDisposed || !IsMainWindowMinimized)
            {
                return;
            }

            _dockWindow!.UpdateScreenPosition();
            if (!_dockWindow.IsVisible)
            {
                _dockWindow.Show();
            }

            _dockWindow.Topmost = true;
            _dockWindow.Activate();
        }, DispatcherPriority.ApplicationIdle);
    }

    private QuickUploadDockWindow CreateDockWindow()
    {
        var dockWindow = new QuickUploadDockWindow();
        dockWindow.DockButtonClicked += DockWindow_OnDockButtonClicked;
        dockWindow.RestoreMainWindowRequested += DockWindow_OnRestoreMainWindowRequested;
        return dockWindow;
    }

    private void DockWindow_OnRestoreMainWindowRequested(object? sender, EventArgs eventArgs) => RestoreMainWindow();

    private void RestoreMainWindow()
    {
        ExitMinimizedQuickUploadMode();
        _mainWindow.ShowInTaskbar = true;
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Show();
        _mainWindow.Activate();
    }

    private void DockWindow_OnDockButtonClicked(object? sender, EventArgs eventArgs)
    {
        if (_dockWindow is null)
        {
            return;
        }

        _panelWindow ??= new QuickUploadPanelWindow(this);

        if (_panelWindow.IsVisible)
        {
            _panelWindow.Hide();
            return;
        }

        _panelWindow.UpdateScreenPosition(new Rect(_dockWindow.Left, _dockWindow.Top, _dockWindow.Width, _dockWindow.Height));
        _panelWindow.Show();
        _panelWindow.Activate();
        _panelWindow.Focus();
    }

    public async Task ExecuteQuickUploadAsync(IReadOnlyList<string> imagePaths)
    {
        if (imagePaths.Count == 0 || _dockWindow is null)
        {
            return;
        }

        ClosePanel();
        _isQuickUploadSessionActive = true;
        _shellViewModel.PropertyChanged += ShellViewModel_OnPropertyChangedDuringQuickUpload;

        try
        {
            ShowFeedbackUploading("正在准备上传…", 0);
            var uploadSucceeded = await _shellViewModel.QuickUploadImagePathsAsync(imagePaths).ConfigureAwait(true);

            var statusMessage = _shellViewModel.StatusText;

            if (uploadSucceeded && !string.IsNullOrWhiteSpace(_shellViewModel.LastGeneratedLink))
            {
                statusMessage = $"{statusMessage}\n链接已复制到剪贴板";
            }

            if (uploadSucceeded)
            {
                ShowFeedbackSuccess(statusMessage);
                ScheduleHideFeedback(TimeSpan.FromSeconds(5));
            }
            else
            {
                ShowFeedbackError(statusMessage);
                ScheduleHideFeedback(TimeSpan.FromSeconds(8));
            }
        }
        catch (Exception exception)
        {
            ShowFeedbackError($"上传失败：{exception.Message}");
            ScheduleHideFeedback(TimeSpan.FromSeconds(8));
        }
        finally
        {
            _isQuickUploadSessionActive = false;
            _shellViewModel.PropertyChanged -= ShellViewModel_OnPropertyChangedDuringQuickUpload;
        }
    }

    private void ShellViewModel_OnPropertyChangedDuringQuickUpload(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (!_isQuickUploadSessionActive)
        {
            return;
        }

        switch (eventArgs.PropertyName)
        {
            case nameof(ShellViewModel.UploadProgressText):
            case nameof(ShellViewModel.UploadOverallProgressPercent):
            case nameof(ShellViewModel.IsUploadProgressVisible):
                if (_shellViewModel.IsUploadProgressVisible)
                {
                    var progressMessage = string.IsNullOrWhiteSpace(_shellViewModel.UploadProgressText)
                        ? "正在上传…"
                        : _shellViewModel.UploadProgressText;
                    ShowFeedbackUploading(progressMessage, _shellViewModel.UploadOverallProgressPercent);
                }

                break;
            case nameof(ShellViewModel.StatusText):
                if (!_shellViewModel.IsBusy)
                {
                    break;
                }

                ShowFeedbackUploading(_shellViewModel.StatusText, _shellViewModel.UploadOverallProgressPercent);
                break;
        }
    }

    private Rect GetDockWindowBounds()
    {
        if (_dockWindow is null)
        {
            var workArea = SystemParameters.WorkArea;
            return new Rect(workArea.Right - 64, workArea.Bottom - 68, 52, 56);
        }

        return new Rect(_dockWindow.Left, _dockWindow.Top, _dockWindow.Width, _dockWindow.Height);
    }

    private void EnsureFeedbackWindow()
    {
        _feedbackWindow ??= new QuickUploadFeedbackWindow();
    }

    private void ShowFeedbackUploading(string message, double progressPercent)
    {
        EnsureFeedbackWindow();
        _feedbackHideTimer?.Stop();
        _feedbackWindow!.ShowUploading(message, progressPercent);
        _feedbackWindow.RepositionAboveDock(GetDockWindowBounds());
        if (!_feedbackWindow.IsVisible)
        {
            _feedbackWindow.Show();
        }
    }

    private void ShowFeedbackSuccess(string message)
    {
        EnsureFeedbackWindow();
        _feedbackWindow!.ShowSuccess(message);
        _feedbackWindow.RepositionAboveDock(GetDockWindowBounds());
        if (!_feedbackWindow.IsVisible)
        {
            _feedbackWindow.Show();
        }
    }

    private void ShowFeedbackError(string message)
    {
        EnsureFeedbackWindow();
        _feedbackWindow!.ShowError(message);
        _feedbackWindow.RepositionAboveDock(GetDockWindowBounds());
        if (!_feedbackWindow.IsVisible)
        {
            _feedbackWindow.Show();
        }
    }

    private void ScheduleHideFeedback(TimeSpan delay)
    {
        _feedbackHideTimer ??= new DispatcherTimer { Interval = delay };
        _feedbackHideTimer.Tick -= FeedbackHideTimer_OnTick;
        _feedbackHideTimer.Interval = delay;
        _feedbackHideTimer.Tick += FeedbackHideTimer_OnTick;
        _feedbackHideTimer.Stop();
        _feedbackHideTimer.Start();
    }

    private void FeedbackHideTimer_OnTick(object? sender, EventArgs eventArgs)
    {
        _feedbackHideTimer?.Stop();
        CloseFeedback();
    }

    private void CloseFeedback()
    {
        _feedbackHideTimer?.Stop();
        _feedbackWindow?.Hide();
    }

    public void ShowQuickUploadValidationError(string message)
    {
        ShowFeedbackError(message);
        ScheduleHideFeedback(TimeSpan.FromSeconds(6));
    }

    private void RepositionVisibleWindows()
    {
        if (_dockWindow?.IsVisible == true)
        {
            _dockWindow.UpdateScreenPosition();
        }

        if (_panelWindow?.IsVisible == true && _dockWindow is not null)
        {
            _panelWindow.UpdateScreenPosition(new Rect(_dockWindow.Left, _dockWindow.Top, _dockWindow.Width, _dockWindow.Height));
        }

        if (_feedbackWindow?.IsVisible == true)
        {
            _feedbackWindow.RepositionAboveDock(GetDockWindowBounds());
        }
    }

    private void ClosePanel()
    {
        if (_panelWindow is null)
        {
            return;
        }

        _panelWindow.Hide();
    }

    private void CloseDock()
    {
        if (_dockWindow is null)
        {
            return;
        }

        _dockWindow.Hide();
    }
}
