using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using JellybeansPicUploader.Infrastructure;
using JellybeansPicUploader.Models;
using JellybeansPicUploader.Services;
using JellybeansPicUploader.ViewModels;

namespace JellybeansPicUploader.Views.Pages;

public partial class UploadPage : PageHostBase
{
    public UploadPage() : this(XamlLoaderShellViewModel.Instance)
    {
    }

    public UploadPage(ShellViewModel shellViewModel) : base(shellViewModel)
    {
        InitializeComponent();
        Loaded += (_, _) => Focus();
    }

    private void UploadArea_OnDragOver(object? sender, DragEventArgs eventArgs)
    {
        if (UploadInputHelper.CanAcceptDragEvent(eventArgs))
        {
            eventArgs.DragEffects = DragDropEffects.Copy;
            UploadDropOverlay.IsVisible = true;
        }
        else
        {
            eventArgs.DragEffects = DragDropEffects.None;
        }

        eventArgs.Handled = true;
    }

    private void UploadArea_OnDragLeave(object? sender, DragEventArgs eventArgs)
    {
        UploadDropOverlay.IsVisible = false;
        eventArgs.Handled = true;
    }

    private void UploadArea_OnDrop(object? sender, DragEventArgs eventArgs)
    {
        UploadDropOverlay.IsVisible = false;

        if (!UploadInputHelper.CanAcceptDragEvent(eventArgs))
        {
            ShellViewModel.StatusText = "未识别到可上传的图片";
            return;
        }

        ShellViewModel.ImportDroppedImageFiles(UploadInputHelper.CollectImagePathsFromDragEvent(eventArgs));
        eventArgs.Handled = true;
    }

    private void UploadPage_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.V || eventArgs.KeyModifiers != KeyModifiers.Control)
        {
            return;
        }

        if (ShellViewModel.TryImportClipboardImages())
        {
            eventArgs.Handled = true;
        }
    }

    private void UploadDropZone_OnPointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        if (eventArgs.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        // 按钮及其子元素（如 TextBlock）由 Command 处理
        if (eventArgs.Source is Visual visual && visual.FindAncestorOfType<Button>() is not null)
        {
            return;
        }

        if (ShellViewModel.AddImagesCommand.CanExecute(null))
        {
            ShellViewModel.AddImagesCommand.Execute(null);
        }
    }

    private void UploadItemCheckBox_OnPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is not CheckBox checkBox || checkBox.DataContext is not UploadItem uploadItem)
        {
            return;
        }

        uploadItem.IsSelected = !uploadItem.IsSelected;
        eventArgs.Handled = true;
    }
}
