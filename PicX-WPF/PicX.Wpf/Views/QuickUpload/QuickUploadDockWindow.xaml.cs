using System.Windows;
using System.Windows.Input;

namespace PicXWpf.Views.QuickUpload;

public partial class QuickUploadDockWindow : Window
{
    public event EventHandler? DockButtonClicked;
    public event EventHandler? RestoreMainWindowRequested;

    public QuickUploadDockWindow()
    {
        InitializeComponent();
    }

    public void UpdateScreenPosition()
    {
        const double marginFromScreenEdge = 12;
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - marginFromScreenEdge;
        Top = workArea.Bottom - Height - marginFromScreenEdge;
    }

    private void DockNoteBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ClickCount != 2)
        {
            return;
        }

        RestoreMainWindowRequested?.Invoke(this, EventArgs.Empty);
        eventArgs.Handled = true;
    }

    private void DockNoteBorder_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ClickCount > 1)
        {
            return;
        }

        DockButtonClicked?.Invoke(this, EventArgs.Empty);
        eventArgs.Handled = true;
    }
}
