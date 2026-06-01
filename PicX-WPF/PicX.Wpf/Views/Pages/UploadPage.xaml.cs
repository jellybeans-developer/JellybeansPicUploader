using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using PicXWpf.Models;
using PicXWpf.Services;
using PicXWpf.ViewModels;

namespace PicXWpf.Views.Pages;

public partial class UploadPage : PageHostBase
{
    public UploadPage()
    {
        InitializeComponent();
        Loaded += UploadPage_OnLoaded;
    }

    private void UploadPage_OnLoaded(object sender, RoutedEventArgs e)
    {
        Focus();
    }

    private ShellViewModel? GetShellViewModel() => DataContext as ShellViewModel;

    private void UploadArea_OnDragOver(object sender, DragEventArgs eventArgs)
    {
        if (UploadInputHelper.CanAcceptDataObject(eventArgs.Data))
        {
            eventArgs.Effects = DragDropEffects.Copy;
            UploadDropOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            eventArgs.Effects = DragDropEffects.None;
        }

        eventArgs.Handled = true;
    }

    private void UploadArea_OnDragLeave(object sender, DragEventArgs eventArgs)
    {
        UploadDropOverlay.Visibility = Visibility.Collapsed;
        eventArgs.Handled = true;
    }

    private void UploadArea_OnDrop(object sender, DragEventArgs eventArgs)
    {
        UploadDropOverlay.Visibility = Visibility.Collapsed;

        if (GetShellViewModel() is not ShellViewModel shellViewModel)
        {
            return;
        }

        if (!UploadInputHelper.CanAcceptDataObject(eventArgs.Data))
        {
            shellViewModel.StatusText = "未识别到可上传的图片";
            return;
        }

        shellViewModel.ImportDroppedImageFiles(UploadInputHelper.CollectImagePathsFromDataObject(eventArgs.Data));
        eventArgs.Handled = true;
    }

    private void UploadPage_OnPreviewKeyDown(object sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        if (GetShellViewModel()?.TryImportClipboardImages() == true)
        {
            eventArgs.Handled = true;
        }
    }

    private void UploadDropZone_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is ButtonBase)
        {
            return;
        }

        if (GetShellViewModel() is ShellViewModel shellViewModel && shellViewModel.AddImagesCommand.CanExecute(null))
        {
            shellViewModel.AddImagesCommand.Execute(null);
            eventArgs.Handled = true;
        }
    }

    private void UploadItemCheckBox_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (sender is not CheckBox checkBox || checkBox.DataContext is not UploadItem uploadItem)
        {
            return;
        }

        uploadItem.IsSelected = !uploadItem.IsSelected;
        eventArgs.Handled = true;
    }
}
