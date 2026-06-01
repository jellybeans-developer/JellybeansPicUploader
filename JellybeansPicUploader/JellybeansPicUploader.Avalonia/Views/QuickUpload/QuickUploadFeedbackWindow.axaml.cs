using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using JellybeansPicUploader.Services;

namespace JellybeansPicUploader.Views.QuickUpload;

public partial class QuickUploadFeedbackWindow : Window
{
    public QuickUploadFeedbackWindow()
    {
        InitializeComponent();
    }

    public void UpdateScreenPosition(Rect dockWindowBounds)
    {
        const double gapAboveDock = 8;
        var workArea = ScreenLayoutHelper.GetPrimaryWorkArea(this);
        Position = new PixelPoint(
            (int)Math.Max(workArea.X + 8, dockWindowBounds.Right - Width),
            (int)Math.Max(workArea.Y + 8, dockWindowBounds.Top - Bounds.Height - gapAboveDock));
    }

    public void ShowUploading(string message, double progressPercent)
    {
        StatusIconTextBlock.Text = "☁";
        StatusIconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
        StatusMessageTextBlock.Text = message;
        UploadProgressBar.IsVisible = true;
        UploadProgressBar.Value = Math.Clamp(progressPercent, 0, 100);
    }

    public void ShowSuccess(string message)
    {
        StatusIconTextBlock.Text = "✓";
        StatusIconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));
        StatusMessageTextBlock.Text = message;
        UploadProgressBar.IsVisible = false;
        UploadProgressBar.Value = 100;
    }

    public void ShowError(string message)
    {
        StatusIconTextBlock.Text = "✕";
        StatusIconTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
        StatusMessageTextBlock.Text = message;
        UploadProgressBar.IsVisible = false;
        UploadProgressBar.Value = 0;
    }

    public void RepositionAboveDock(Rect dockWindowBounds)
    {
        UpdateScreenPosition(dockWindowBounds);
    }
}
