using PicXWpf.Models;

namespace PicXWpf.Services;

public sealed class AppSession
{
    public AppSettings Settings { get; private set; } = new();

    public GitHubUserInfo? CurrentUser { get; set; }

    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(Settings.GitHubToken) && CurrentUser is not null;

    public event EventHandler? SettingsChanged;

    public void UpdateSettings(AppSettings settings)
    {
        Settings = settings;
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NotifySettingsChanged() => SettingsChanged?.Invoke(this, EventArgs.Empty);
}
