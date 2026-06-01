using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using JellybeansPicUploader.Services;

namespace JellybeansPicUploader.Views.QuickUpload;

public partial class QuickUploadDockWindow : Window
{
    private bool _suppressNextClickAfterDoubleClick;

    public event EventHandler? DockButtonClicked;
    public event EventHandler? RestoreMainWindowRequested;

    public QuickUploadDockWindow()
    {
        InitializeComponent();
    }

    public void UpdateScreenPosition()
    {
        const double marginFromScreenEdge = 12;
        var workArea = ScreenLayoutHelper.GetPrimaryWorkArea(this);
        Position = new PixelPoint(
            (int)(workArea.Right - Width - marginFromScreenEdge),
            (int)(workArea.Bottom - Height - marginFromScreenEdge));
    }

    private void DockNoteBorder_OnPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.ClickCount == 2)
        {
            _suppressNextClickAfterDoubleClick = true;
            RestoreMainWindowRequested?.Invoke(this, EventArgs.Empty);
            eventArgs.Handled = true;
        }
    }

    private void DockNoteBorder_OnPointerReleased(object? sender, PointerReleasedEventArgs eventArgs)
    {
        if (eventArgs.InitialPressMouseButton != MouseButton.Left)
        {
            return;
        }

        if (_suppressNextClickAfterDoubleClick)
        {
            _suppressNextClickAfterDoubleClick = false;
            return;
        }

        DockButtonClicked?.Invoke(this, EventArgs.Empty);
        eventArgs.Handled = true;
    }
}
