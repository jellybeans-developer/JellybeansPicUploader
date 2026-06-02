using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using JellybeansPicUploader.Services;

namespace JellybeansPicUploader.Views.QuickUpload;

public partial class QuickUploadPanelWindow : Window
{
    private readonly QuickUploadDockCoordinator _quickUploadDockCoordinator;
    private bool _isChoosingFiles;

    public QuickUploadPanelWindow()
    {
        _quickUploadDockCoordinator = null!;
        InitializeComponent();
    }

    public QuickUploadPanelWindow(QuickUploadDockCoordinator quickUploadDockCoordinator)
    {
        _quickUploadDockCoordinator = quickUploadDockCoordinator;
        InitializeComponent();
        Deactivated += QuickUploadPanelWindow_OnDeactivated;
    }

    public void UpdateScreenPosition(Rect dockWindowBounds)
    {
        const double gapAboveDock = 8;
        var workArea = ScreenLayoutHelper.GetPrimaryWorkArea(this);
        Position = new PixelPoint(
            (int)Math.Max(workArea.X + 8, dockWindowBounds.Right - Width),
            (int)Math.Max(workArea.Y + 8, dockWindowBounds.Top - Height - gapAboveDock));
    }

    private void QuickUploadPanelWindow_OnDeactivated(object? sender, EventArgs eventArgs)
    {
        if (_isChoosingFiles)
        {
            return;
        }

        Hide();
    }

    private async void QuickUploadDropZone_OnDrop(object? sender, DragEventArgs eventArgs)
    {
        QuickUploadDropOverlay.IsVisible = false;

        if (!UploadInputHelper.CanAcceptDragEvent(eventArgs))
        {
            _quickUploadDockCoordinator.ShowQuickUploadValidationError("未识别到可上传的图片");
            return;
        }

        await StartQuickUploadAsync(UploadInputHelper.CollectImagePathsFromDragEvent(eventArgs));
        eventArgs.Handled = true;
    }

    private void QuickUploadDropZone_OnDragOver(object? sender, DragEventArgs eventArgs)
    {
        if (UploadInputHelper.CanAcceptDragEvent(eventArgs))
        {
            eventArgs.DragEffects = DragDropEffects.Copy;
            QuickUploadDropOverlay.IsVisible = true;
        }
        else
        {
            eventArgs.DragEffects = DragDropEffects.None;
        }

        eventArgs.Handled = true;
    }

    private void QuickUploadDropZone_OnDragLeave(object? sender, DragEventArgs eventArgs)
    {
        QuickUploadDropOverlay.IsVisible = false;
        eventArgs.Handled = true;
    }

    private void QuickUploadDropZone_OnPointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        if (eventArgs.InitialPressMouseButton == MouseButton.Left)
        {
            _ = PickImagesFromDialogAsync();
            eventArgs.Handled = true;
        }
    }

    private void ClickUploadText_OnPointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        _ = PickImagesFromDialogAsync();
        eventArgs.Handled = true;
    }

    private async void QuickUploadPanelWindow_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.V || eventArgs.KeyModifiers != KeyModifiers.Control)
        {
            return;
        }

        var imagePaths = await UploadInputHelper.CollectImagePathsFromClipboardAsync();
        if (imagePaths.Count == 0)
        {
            _quickUploadDockCoordinator.ShowQuickUploadValidationError("剪贴板中没有可用的图片");
            return;
        }

        await StartQuickUploadAsync(imagePaths);
        eventArgs.Handled = true;
    }

    private async Task PickImagesFromDialogAsync()
    {
        _isChoosingFiles = true;
        try
        {
            var selectedFiles = await DesktopDialogService.PickImageFilesAsync(allowMultiple: true);
            if (selectedFiles is null || selectedFiles.Count == 0)
            {
                return;
            }

            await StartQuickUploadAsync(selectedFiles);
        }
        finally
        {
            _isChoosingFiles = false;
        }
    }

    private Task StartQuickUploadAsync(IReadOnlyList<string> imagePaths)
    {
        if (imagePaths.Count == 0)
        {
            return Task.CompletedTask;
        }

        Hide();
        return _quickUploadDockCoordinator.ExecuteQuickUploadAsync(imagePaths);
    }
}
