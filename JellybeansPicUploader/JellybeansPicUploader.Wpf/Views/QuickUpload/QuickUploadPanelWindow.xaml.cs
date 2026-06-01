using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Win32;
using JellybeansPicUploader.Services;
namespace JellybeansPicUploader.Views.QuickUpload;

public partial class QuickUploadPanelWindow : Window
{
    private readonly QuickUploadDockCoordinator _quickUploadDockCoordinator;
    private bool _isChoosingFiles;

    public QuickUploadPanelWindow(QuickUploadDockCoordinator quickUploadDockCoordinator)
    {
        _quickUploadDockCoordinator = quickUploadDockCoordinator;
        InitializeComponent();
        Deactivated += QuickUploadPanelWindow_OnDeactivated;
    }

    public void UpdateScreenPosition(Rect dockWindowBounds)
    {
        const double gapAboveDock = 8;
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left + 8, dockWindowBounds.Right - Width);
        Top = Math.Max(workArea.Top + 8, dockWindowBounds.Top - Height - gapAboveDock);
    }

    protected override void OnSourceInitialized(EventArgs eventArgs)
    {
        base.OnSourceInitialized(eventArgs);
        Focus();
        Activate();
    }

    private void QuickUploadPanelWindow_OnDeactivated(object? sender, EventArgs eventArgs)
    {
        if (_isChoosingFiles || IsMouseOver)
        {
            return;
        }

        Hide();
    }

    private async void QuickUploadDropZone_OnDrop(object sender, DragEventArgs eventArgs)
    {
        QuickUploadDropOverlay.Visibility = Visibility.Collapsed;

        if (!UploadInputHelper.CanAcceptDataObject(eventArgs.Data))
        {
            _quickUploadDockCoordinator.ShowQuickUploadValidationError("未识别到可上传的图片");
            return;
        }

        await StartQuickUploadAsync(UploadInputHelper.CollectImagePathsFromDataObject(eventArgs.Data));
        eventArgs.Handled = true;
    }

    private void QuickUploadDropZone_OnDragOver(object sender, DragEventArgs eventArgs)
    {
        if (UploadInputHelper.CanAcceptDataObject(eventArgs.Data))
        {
            eventArgs.Effects = DragDropEffects.Copy;
            QuickUploadDropOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            eventArgs.Effects = DragDropEffects.None;
        }

        eventArgs.Handled = true;
    }

    private void QuickUploadDropZone_OnDragLeave(object sender, DragEventArgs eventArgs)
    {
        QuickUploadDropOverlay.Visibility = Visibility.Collapsed;
        eventArgs.Handled = true;
    }

    private void QuickUploadDropZone_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is ButtonBase)
        {
            return;
        }

        PickImagesFromDialog();
        eventArgs.Handled = true;
    }

    private void ClickUploadText_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        PickImagesFromDialog();
        eventArgs.Handled = true;
    }

    private async void QuickUploadPanelWindow_OnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        var imagePaths = UploadInputHelper.CollectImagePathsFromClipboard();
        if (imagePaths.Count == 0)
        {
            _quickUploadDockCoordinator.ShowQuickUploadValidationError("剪贴板中没有可用的图片");
            return;
        }

        await StartQuickUploadAsync(imagePaths);
        eventArgs.Handled = true;
    }

    private async void PickImagesFromDialog()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "选择图片（可多选）",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp;*.avif|所有文件|*.*",
            Multiselect = true
        };

        _isChoosingFiles = true;
        try
        {
            var dialogAccepted = openFileDialog.ShowDialog(this) == true;
            if (!dialogAccepted)
            {
                return;
            }

            await StartQuickUploadAsync(openFileDialog.FileNames);
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
