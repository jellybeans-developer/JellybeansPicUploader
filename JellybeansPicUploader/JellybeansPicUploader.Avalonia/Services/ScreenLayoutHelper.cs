using Avalonia;
using Avalonia.Controls;

namespace JellybeansPicUploader.Services;

/// <summary>
/// 屏幕工作区布局辅助（替代 WPF SystemParameters.WorkArea）。
/// </summary>
public static class ScreenLayoutHelper
{
    public static PixelRect GetPrimaryWorkArea(Window? referenceWindow)
    {
        var screen = referenceWindow?.Screens?.ScreenFromWindow(referenceWindow)
            ?? referenceWindow?.Screens?.Primary;
        return screen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
    }
}
