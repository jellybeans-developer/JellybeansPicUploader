using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace PicXWpf.Views.QuickUpload;

public partial class QuickUploadFeedbackWindow : Window
{
    public QuickUploadFeedbackWindow()
    {
        InitializeComponent();
    }

    public void UpdateScreenPosition(Rect dockWindowBounds)
    {
        const double gapAboveDock = 8;
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left + 8, dockWindowBounds.Right - Width);
        Top = Math.Max(workArea.Top + 8, dockWindowBounds.Top - ActualHeight - gapAboveDock);
    }

    public void ShowUploading(string message, double progressPercent)
    {
        StatusIcon.Symbol = SymbolRegular.CloudArrowUp24;
        StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
        StatusMessageTextBlock.Text = message;
        UploadProgressBar.Visibility = Visibility.Visible;
        UploadProgressBar.Value = Math.Clamp(progressPercent, 0, 100);
    }

    public void ShowSuccess(string message)
    {
        StatusIcon.Symbol = SymbolRegular.CheckmarkCircle24;
        StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0xBB, 0x6A));
        StatusMessageTextBlock.Text = message;
        UploadProgressBar.Visibility = Visibility.Collapsed;
        UploadProgressBar.Value = 100;
    }

    public void ShowError(string message)
    {
        StatusIcon.Symbol = SymbolRegular.ErrorCircle24;
        StatusIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x53, 0x50));
        StatusMessageTextBlock.Text = message;
        UploadProgressBar.Visibility = Visibility.Collapsed;
        UploadProgressBar.Value = 0;
    }

    public void RepositionAboveDock(Rect dockWindowBounds)
    {
        UpdateLayout();
        UpdateScreenPosition(dockWindowBounds);
    }
}
