using Avalonia.Threading;

namespace JellybeansPicUploader.Infrastructure;

/// <summary>
/// UI 线程调度辅助（替代 WPF Dispatcher）。
/// </summary>
public static class UiThreadHelper
{
    public static void RunOnUiThread(Action action)
    {
        var dispatcher = Dispatcher.UIThread;
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Post(action);
    }
}
