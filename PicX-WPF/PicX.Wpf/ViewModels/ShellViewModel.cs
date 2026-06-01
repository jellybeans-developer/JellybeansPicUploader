using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using PicXWpf.Infrastructure;
using PicXWpf.Models;
using PicXWpf.Services;
using PicXWpf.Views.Dialogs;

namespace PicXWpf.ViewModels;

public sealed class ShellViewModel : ObservableObject, IDisposable
{
    private const int ManagementTabIndex = 2;

    private readonly AppSettingsService _appSettingsService = new();
    private readonly GitHubApiClient _gitHubApiClient = new();
    private readonly GitHubOAuthService _gitHubOAuthService = new();
    private readonly ImageLinkGenerator _imageLinkGenerator = new();
    private readonly ImageProcessingService _imageProcessingService = new();
    private readonly RemoteThumbnailLoader _remoteThumbnailLoader = new();
    private readonly AppSession _appSession = new();
    private readonly SemaphoreSlim _managedImageThumbnailLoadSemaphore = new(6, 6);

    private CancellationTokenSource? _operationCancellationTokenSource;
    private CancellationTokenSource? _managedImageThumbnailLoadCancellationTokenSource;
    private UploadProgressSmoother? _uploadProgressSmoother;
    private string _statusText = "就绪";
    private bool _isBusy;
    private int _selectedTabIndex;
    private GitHubUserInfo? _currentUser;
    private string _manualToken = string.Empty;
    private string _lastGeneratedLink = string.Empty;
    private string _toolboxSourceFilePath = string.Empty;
    private string _toolboxBase64Result = string.Empty;
    private string _customLinkRuleTemplate = string.Empty;
    private bool _isGitHubAuthorizing;
    private CancellationTokenSource? _settingsPersistDebounceCancellationTokenSource;
    private bool _isUploadProgressVisible;
    private double _uploadOverallProgressPercent;
    private string _uploadProgressText = string.Empty;

    public ShellViewModel()
    {
        UploadItems = new ObservableCollection<UploadItem>();
        ManagedImages = new ObservableCollection<ManagedImageItem>();
        AvailableBranches = new ObservableCollection<string>();
        LinkRuleNames = new ObservableCollection<string>();
        ToolboxProcessedFiles = new ObservableCollection<string>();

        LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        LoginWithManualTokenCommand = new AsyncRelayCommand(LoginWithManualTokenAsync);
        LoginWithGitHubAuthorizeCommand = new AsyncRelayCommand(LoginWithGitHubAuthorizeAsync, () => !IsGitHubAuthorizing);
        LoginWithOAuthCommand = LoginWithGitHubAuthorizeCommand;
        OpenInstallPageCommand = new RelayCommand(OpenInstallPage);
        LogoutCommand = new RelayCommand(Logout, () => IsLoggedIn);
        AddImagesCommand = new RelayCommand(() => AddImagesFromDialog(), () => !IsBusy);
        PasteImagesFromClipboardCommand = new RelayCommand(PasteImagesFromClipboard, () => !IsBusy);
        ImportImageFromUrlCommand = new AsyncRelayCommand(ImportImageFromUrlAsync, () => !IsBusy);
        SelectUploadLinkFormatCommand = new RelayCommand<string?>(SelectUploadLinkFormat, _ => !IsBusy);
        SelectAllUploadItemsCommand = new RelayCommand(SelectAllUploadItems, () => UploadItems.Count > 0 && !IsBusy);
        ClearUploadItemSelectionCommand = new RelayCommand(ClearUploadItemSelection, () => UploadItems.Any(item => item.IsSelected) && !IsBusy);
        RemoveSelectedUploadCommand = new RelayCommand(RemoveSelectedUpload, CanRemoveSelectedUploadItems);
        ClearUploadListCommand = new RelayCommand(ClearUploadList, () => UploadItems.Count > 0 && !IsBusy);
        UploadImagesCommand = new AsyncRelayCommand(UploadImagesAsync, CanUploadImages);
        CancelOperationCommand = new RelayCommand(CancelOperation, () => IsBusy);
        CopyLastLinkCommand = new RelayCommand(CopyLastLink, () => !string.IsNullOrWhiteSpace(LastGeneratedLink));
        CopyUploadItemGitHubPagesLinkCommand = new RelayCommand<UploadItem>(
            CopyUploadItemGitHubPagesLink,
            item => item is not null && !string.IsNullOrWhiteSpace(item.GitHubPagesImageUrl ?? item.ImageUrl));
        RefreshManagedImagesCommand = new AsyncRelayCommand(LoadAllRepositoryImagesAsync, () => IsLoggedIn && !IsBusy);
        SelectAllManagedImagesCommand = new RelayCommand(SelectAllManagedImages, () => ManagedImages.Count > 0 && !IsBusy);
        ClearManagedImageSelectionCommand = new RelayCommand(ClearManagedImageSelection, () => ManagedImages.Any(item => item.IsSelected) && !IsBusy);
        DeleteSelectedImagesCommand = new AsyncRelayCommand(DeleteSelectedImagesAsync, () => IsLoggedIn && ManagedImages.Any(x => x.IsSelected) && !IsBusy);
        CopySelectedImageLinksCommand = new RelayCommand(CopySelectedImageLinks, () => ManagedImages.Any(x => x.IsSelected));
        DeployGitHubPagesCommand = new AsyncRelayCommand(DeployGitHubPagesAsync, () => IsLoggedIn && !IsBusy);
        AddCustomLinkRuleCommand = new RelayCommand(AddCustomLinkRule);
        SelectToolboxFileCommand = new RelayCommand(SelectToolboxFile);
        RunToolboxCompressCommand = new AsyncRelayCommand(RunToolboxCompressAsync, () => !string.IsNullOrWhiteSpace(ToolboxSourceFilePath) && !IsBusy);
        RunToolboxBase64Command = new AsyncRelayCommand(RunToolboxBase64Async, () => !string.IsNullOrWhiteSpace(ToolboxSourceFilePath) && !IsBusy);
        RunToolboxWatermarkCommand = new AsyncRelayCommand(RunToolboxWatermarkAsync, () => !string.IsNullOrWhiteSpace(ToolboxSourceFilePath) && !IsBusy);
        CopyToolboxBase64Command = new RelayCommand(CopyToolboxBase64, () => !string.IsNullOrWhiteSpace(ToolboxBase64Result));

        _appSession.SettingsChanged += (_, _) => RaiseLoginStateChanged();
    }

    public ObservableCollection<UploadItem> UploadItems { get; }
    public ObservableCollection<ManagedImageItem> ManagedImages { get; }
    public ObservableCollection<string> AvailableBranches { get; }

    public bool IsManagedImageGalleryEmpty => !IsBusy && ManagedImages.Count == 0;

    public string ManagedImageGallerySummary
    {
        get
        {
            var selectedCount = ManagedImages.Count(item => item.IsSelected);
            return selectedCount > 0
                ? $"共 {ManagedImages.Count} 张图片，已选中 {selectedCount} 张"
                : $"共 {ManagedImages.Count} 张图片";
        }
    }
    public ObservableCollection<string> LinkRuleNames { get; }
    public ObservableCollection<string> ToolboxProcessedFiles { get; }

    public AppSettings Settings => _appSession.Settings;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            if (!SetProperty(ref _selectedTabIndex, value))
            {
                return;
            }

            if (value == ManagementTabIndex)
            {
                _ = InitializeManagementViewAsync();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseAllCommandStates();
            }
        }
    }

    public bool IsLoggedIn => _appSession.IsLoggedIn;

    public GitHubUserInfo? CurrentUser
    {
        get => _currentUser;
        private set
        {
            if (SetProperty(ref _currentUser, value))
            {
                OnPropertyChanged(nameof(LoginDisplayName));
                RaiseLoginStateChanged();
            }
        }
    }

    public string LoginDisplayName => CurrentUser?.Name ?? CurrentUser?.Login ?? "未登录";

    public bool IsGitHubAuthorizing
    {
        get => _isGitHubAuthorizing;
        private set
        {
            if (SetProperty(ref _isGitHubAuthorizing, value))
            {
                OnPropertyChanged(nameof(GitHubAuthorizeButtonText));
                LoginWithGitHubAuthorizeCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string GitHubAuthorizeButtonText =>
        IsGitHubAuthorizing ? "正在授权，请在浏览器中完成..." : "GitHub 授权登录";

    public string ManualToken
    {
        get => _manualToken;
        set => SetProperty(ref _manualToken, value);
    }

    public string GitHubToken
    {
        get => Settings.GitHubToken;
        set
        {
            Settings.GitHubToken = value;
            OnPropertyChanged();
            SaveSettingsCommand.RaiseCanExecuteChanged();
        }
    }

    public string Owner
    {
        get => Settings.Owner;
        set => UpdateRepositoryConfiguration(value, Settings.Owner, v => Settings.Owner = v, nameof(Owner));
    }

    public string Repository
    {
        get => Settings.Repository;
        set => UpdateRepositoryConfiguration(value, Settings.Repository, v => Settings.Repository = v, nameof(Repository));
    }

    public string? Branch
    {
        get => Settings.Branch;
        set => UpdateRepositoryConfiguration(value, Settings.Branch, v => Settings.Branch = v, nameof(Branch));
    }

    public string Email
    {
        get => Settings.Email;
        set => UpdateRepositoryConfiguration(value, Settings.Email, v => Settings.Email = v, nameof(Email));
    }

    public string TargetDirectoryPath
    {
        get => Settings.TargetDirectoryPath;
        set => UpdateRepositoryConfiguration(value, Settings.TargetDirectoryPath, v => Settings.TargetDirectoryPath = v, nameof(TargetDirectoryPath));
    }

    public DirectoryMode DirectoryMode
    {
        get => Settings.DirectoryMode;
        set => UpdateRepositoryConfiguration(value, Settings.DirectoryMode, v => Settings.DirectoryMode = v, nameof(DirectoryMode));
    }

    public string CommitMessage
    {
        get => Settings.CommitMessage;
        set => UpdateRepositoryConfiguration(value, Settings.CommitMessage, v => Settings.CommitMessage = v, nameof(CommitMessage));
    }

    public bool EnableHash
    {
        get => Settings.UserSettings.ImageName.EnableHash;
        set { Settings.UserSettings.ImageName.EnableHash = value; OnPropertyChanged(); }
    }

    public bool EnablePrefix
    {
        get => Settings.UserSettings.ImageName.EnablePrefix;
        set { Settings.UserSettings.ImageName.EnablePrefix = value; OnPropertyChanged(); }
    }

    public string ImageNamePrefix
    {
        get => Settings.UserSettings.ImageName.Prefix;
        set { Settings.UserSettings.ImageName.Prefix = value; OnPropertyChanged(); }
    }

    public bool EnableCompress
    {
        get => Settings.UserSettings.Compress.Enable;
        set { Settings.UserSettings.Compress.Enable = value; OnPropertyChanged(); }
    }

    public CompressEncoder CompressEncoder
    {
        get => Settings.UserSettings.Compress.Encoder;
        set { Settings.UserSettings.Compress.Encoder = value; OnPropertyChanged(); }
    }

    public bool EnableWatermark
    {
        get => Settings.UserSettings.Watermark.Enable;
        set { Settings.UserSettings.Watermark.Enable = value; OnPropertyChanged(); }
    }

    public string WatermarkText
    {
        get => Settings.UserSettings.Watermark.Text;
        set { Settings.UserSettings.Watermark.Text = value; OnPropertyChanged(); }
    }

    public int WatermarkFontSize
    {
        get => Settings.UserSettings.Watermark.FontSize;
        set { Settings.UserSettings.Watermark.FontSize = value; OnPropertyChanged(); }
    }

    public WatermarkPosition WatermarkPosition
    {
        get => Settings.UserSettings.Watermark.Position;
        set { Settings.UserSettings.Watermark.Position = value; OnPropertyChanged(); }
    }

    public string WatermarkTextColorHex
    {
        get => Settings.UserSettings.Watermark.TextColorHex;
        set { Settings.UserSettings.Watermark.TextColorHex = value; OnPropertyChanged(); }
    }

    public double WatermarkOpacity
    {
        get => Settings.UserSettings.Watermark.Opacity;
        set { Settings.UserSettings.Watermark.Opacity = value; OnPropertyChanged(); }
    }

    public bool EnableLinkFormat
    {
        get => Settings.UserSettings.ImageLinkFormat.Enable;
        set
        {
            Settings.UserSettings.ImageLinkFormat.Enable = value;
            OnPropertyChanged();
            NotifyUploadLinkFormatBindingsChanged();
            SchedulePersistSettings();
        }
    }

    public ImageLinkFormatType SelectedLinkFormat
    {
        get => Settings.UserSettings.ImageLinkFormat.SelectedFormat;
        set
        {
            Settings.UserSettings.ImageLinkFormat.SelectedFormat = value;
            OnPropertyChanged();
            NotifyUploadLinkFormatBindingsChanged();
            SchedulePersistSettings();
        }
    }

    /// <summary>
    /// 上传页链接格式选项：Markdown / HTML / URL / UBB / Custom
    /// </summary>
    public string SelectedUploadLinkFormatOption => ResolveUploadLinkFormatOption();

    public bool IsCustomUploadLinkFormatPanelVisible =>
        string.Equals(SelectedUploadLinkFormatOption, "Custom", StringComparison.Ordinal);

    public string CustomLinkFormatTemplate
    {
        get => Settings.UserSettings.ImageLinkFormat.CustomFormatTemplate;
        set
        {
            Settings.UserSettings.ImageLinkFormat.CustomFormatTemplate = value;
            OnPropertyChanged();
            SchedulePersistSettings();
        }
    }

    public string SelectedLinkRuleName
    {
        get => Settings.UserSettings.ImageLinkType.SelectedRuleName;
        set { Settings.UserSettings.ImageLinkType.SelectedRuleName = value; OnPropertyChanged(); }
    }

    public string CustomLinkRuleTemplate
    {
        get => _customLinkRuleTemplate;
        set => SetProperty(ref _customLinkRuleTemplate, value);
    }

    public bool AutoCopyLinkAfterUpload
    {
        get => Settings.UserSettings.AutoCopyLinkAfterUpload;
        set { Settings.UserSettings.AutoCopyLinkAfterUpload = value; OnPropertyChanged(); }
    }

    public PostUploadPublishMode PostUploadPublishMode
    {
        get => Settings.UserSettings.PostUploadPublishMode;
        set
        {
            UpdateUserSetting(
                value,
                Settings.UserSettings.PostUploadPublishMode,
                v => Settings.UserSettings.PostUploadPublishMode = v,
                nameof(PostUploadPublishMode));
            NotifyJsDelivrConfigurationBindingsChanged();
        }
    }

    public JsDelivrVersionReferenceMode JsDelivrVersionReferenceMode
    {
        get => Settings.UserSettings.JsDelivrVersionReferenceMode;
        set
        {
            UpdateUserSetting(
                value,
                Settings.UserSettings.JsDelivrVersionReferenceMode,
                v => Settings.UserSettings.JsDelivrVersionReferenceMode = v,
                nameof(JsDelivrVersionReferenceMode));
            NotifyJsDelivrConfigurationBindingsChanged();
        }
    }

    public string JsDelivrTagName
    {
        get => Settings.UserSettings.JsDelivrTagName;
        set => UpdateUserSetting(value, Settings.UserSettings.JsDelivrTagName, v => Settings.UserSettings.JsDelivrTagName = v, nameof(JsDelivrTagName));
    }

    public bool IsJsDelivrVersionReferenceConfigurationVisible =>
        Settings.UserSettings.PostUploadPublishMode == PostUploadPublishMode.JsDelivr;

    public bool IsJsDelivrTagNameConfigurationVisible =>
        IsJsDelivrVersionReferenceConfigurationVisible
        && Settings.UserSettings.JsDelivrVersionReferenceMode == JsDelivrVersionReferenceMode.Tag;

    public string LastGeneratedLink
    {
        get => _lastGeneratedLink;
        set
        {
            if (SetProperty(ref _lastGeneratedLink, value))
            {
                CopyLastLinkCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsUploadProgressVisible
    {
        get => _isUploadProgressVisible;
        private set => SetProperty(ref _isUploadProgressVisible, value);
    }

    public double UploadOverallProgressPercent
    {
        get => _uploadOverallProgressPercent;
        private set => SetProperty(ref _uploadOverallProgressPercent, value);
    }

    public string UploadProgressText
    {
        get => _uploadProgressText;
        private set => SetProperty(ref _uploadProgressText, value);
    }

    public UploadItem? SelectedUploadItem
    {
        get => _selectedUploadItem;
        set
        {
            if (SetProperty(ref _selectedUploadItem, value))
            {
                RemoveSelectedUploadCommand.RaiseCanExecuteChanged();
            }
        }
    }
    private UploadItem? _selectedUploadItem;

    public string ToolboxSourceFilePath
    {
        get => _toolboxSourceFilePath;
        set
        {
            if (SetProperty(ref _toolboxSourceFilePath, value))
            {
                RunToolboxCompressCommand.RaiseCanExecuteChanged();
                RunToolboxBase64Command.RaiseCanExecuteChanged();
                RunToolboxWatermarkCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ToolboxBase64Result
    {
        get => _toolboxBase64Result;
        set
        {
            if (SetProperty(ref _toolboxBase64Result, value))
            {
                CopyToolboxBase64Command.RaiseCanExecuteChanged();
            }
        }
    }

    public AsyncRelayCommand LoadSettingsCommand { get; }
    public AsyncRelayCommand SaveSettingsCommand { get; }
    public AsyncRelayCommand LoginWithManualTokenCommand { get; }
    public AsyncRelayCommand LoginWithGitHubAuthorizeCommand { get; }
    public AsyncRelayCommand LoginWithOAuthCommand { get; }
    public RelayCommand OpenInstallPageCommand { get; }
    public RelayCommand LogoutCommand { get; }
    public RelayCommand AddImagesCommand { get; }
    public RelayCommand PasteImagesFromClipboardCommand { get; }
    public AsyncRelayCommand ImportImageFromUrlCommand { get; }
    public RelayCommand<string?> SelectUploadLinkFormatCommand { get; }
    public RelayCommand SelectAllUploadItemsCommand { get; }
    public RelayCommand ClearUploadItemSelectionCommand { get; }
    public RelayCommand RemoveSelectedUploadCommand { get; }
    public RelayCommand ClearUploadListCommand { get; }
    public AsyncRelayCommand UploadImagesCommand { get; }
    public RelayCommand CancelOperationCommand { get; }
    public RelayCommand CopyLastLinkCommand { get; }
    public RelayCommand<UploadItem> CopyUploadItemGitHubPagesLinkCommand { get; }
    public AsyncRelayCommand RefreshManagedImagesCommand { get; }
    public RelayCommand SelectAllManagedImagesCommand { get; }
    public RelayCommand ClearManagedImageSelectionCommand { get; }
    public AsyncRelayCommand DeleteSelectedImagesCommand { get; }
    public RelayCommand CopySelectedImageLinksCommand { get; }
    public AsyncRelayCommand DeployGitHubPagesCommand { get; }
    public RelayCommand AddCustomLinkRuleCommand { get; }
    public RelayCommand SelectToolboxFileCommand { get; }
    public AsyncRelayCommand RunToolboxCompressCommand { get; }
    public AsyncRelayCommand RunToolboxBase64Command { get; }
    public AsyncRelayCommand RunToolboxWatermarkCommand { get; }
    public RelayCommand CopyToolboxBase64Command { get; }

    public async Task InitializeAsync()
    {
        await LoadSettingsAsync();
        RefreshLinkRuleNames();
        if (!string.IsNullOrWhiteSpace(Settings.GitHubToken))
        {
            StatusText = "正在恢复登录状态…";
            _ = ValidateCurrentTokenOnStartupAsync();
        }
    }

    private async Task ValidateCurrentTokenOnStartupAsync()
    {
        try
        {
            await ValidateCurrentTokenAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusText = $"自动登录检查失败：{exception.Message}";
            RaiseLoginStateChanged();
        }
    }

    public bool TryImportClipboardImages()
    {
        var imagePaths = UploadInputHelper.CollectImagePathsFromClipboard();
        if (imagePaths.Count == 0)
        {
            StatusText = "剪贴板中没有可用的图片";
            return false;
        }

        AddImageFiles(imagePaths);
        StatusText = $"已从剪贴板添加 {imagePaths.Count} 张图片";
        return true;
    }

    public void ImportDroppedImageFiles(IEnumerable<string> filePaths)
    {
        var imagePaths = UploadInputHelper.CollectImagePathsFromPaths(filePaths).ToList();
        if (imagePaths.Count == 0)
        {
            StatusText = "未识别到可上传的图片文件";
            return;
        }

        AddImageFiles(imagePaths);
        StatusText = $"已添加 {imagePaths.Count} 张图片";
    }

    /// <summary>
    /// 贴边快捷上传：添加图片后立即上传，并按完成顺序依次写入剪贴板。
    /// </summary>
    public async Task<bool> QuickUploadImagePathsAsync(IEnumerable<string> filePaths)
    {
        var imagePaths = UploadInputHelper.CollectImagePathsFromPaths(filePaths).ToList();
        if (imagePaths.Count == 0)
        {
            StatusText = "未识别到可上传的图片";
            return false;
        }

        if (IsBusy)
        {
            StatusText = "正在上传，请稍候";
            return false;
        }

        if (!ValidateRepositorySettings(out var validationMessage))
        {
            StatusText = validationMessage;
            return false;
        }

        var uploadListCountBeforeAdd = UploadItems.Count;
        AddImageFiles(imagePaths);
        var newlyAddedItems = UploadItems.Skip(uploadListCountBeforeAdd).ToList();
        if (newlyAddedItems.Count == 0)
        {
            StatusText = "所选图片已在待上传列表中，请换一张或打开主窗口清空列表后重试";
            return false;
        }

        SelectedTabIndex = 1;
        StatusText = $"正在上传 {newlyAddedItems.Count} 张图片…";
        await UploadPendingItemsInternalAsync(newlyAddedItems, copyEachLinkToClipboardSequentially: true).ConfigureAwait(true);
        return newlyAddedItems.All(item => item.Status == UploadItemStatus.Uploaded);
    }

    public void AddImageFiles(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths.Where(ImageFileHelper.IsImageFile))
        {
            if (UploadItems.Any(item => string.Equals(item.LocalFilePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var fileInfo = new FileInfo(path);
            var remoteFileName = FileNameHelper.BuildRemoteFileName(fileInfo.Name, Settings.UserSettings);
            var uploadItem = new UploadItem
            {
                LocalFilePath = path,
                OriginalFileName = fileInfo.Name,
                FileSizeBytes = fileInfo.Length,
                DisplayFileName = fileInfo.Name,
                RemoteFileName = remoteFileName
            };
            HookUploadItemSelectionChanged(uploadItem);
            UploadItems.Add(uploadItem);
        }

        StatusText = $"已选择 {UploadItems.Count} 个文件";
        RaiseUploadListCommandStates();
    }

    private async Task InitializeManagementViewAsync()
    {
        if (!IsLoggedIn || ManagedImages.Count > 0)
        {
            return;
        }

        try
        {
            await LoadAllRepositoryImagesAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusText = $"加载管理页图片失败：{exception.Message}";
        }
    }

    private async Task SyncManagementViewAfterUploadAsync()
    {
        if (!IsLoggedIn)
        {
            return;
        }

        try
        {
            await LoadAllRepositoryImagesAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            StatusText = $"刷新管理页图片列表失败：{exception.Message}";
        }
    }

    private async Task LoadSettingsAsync()
    {
        var settings = await _appSettingsService.LoadAsync().ConfigureAwait(true);
        _appSession.UpdateSettings(settings);
        ManualToken = settings.Authorization.ManualToken;
        NotifyAllSettingsProperties();
        RefreshLinkRuleNames();
        StatusText = string.IsNullOrWhiteSpace(Repository)
            ? "设置已加载，请填写图床配置"
            : $"设置已加载：{Owner}/{Repository}";
    }

    private async Task SaveSettingsAsync()
    {
        await PersistSettingsSilentlyAsync().ConfigureAwait(true);
        StatusText = "设置已保存";
    }

    private void UpdateRepositoryConfiguration<T>(
        T newValue,
        T currentValue,
        Action<T> applyValue,
        string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
        {
            return;
        }

        applyValue(newValue);
        OnPropertyChanged(propertyName);
        SchedulePersistSettings();
    }

    private void UpdateUserSetting<T>(
        T newValue,
        T currentValue,
        Action<T> applyValue,
        string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
        {
            return;
        }

        applyValue(newValue);
        OnPropertyChanged(propertyName);
        SchedulePersistSettings();
    }

    private void SchedulePersistSettings()
    {
        _settingsPersistDebounceCancellationTokenSource?.Cancel();
        _settingsPersistDebounceCancellationTokenSource?.Dispose();
        _settingsPersistDebounceCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _settingsPersistDebounceCancellationTokenSource.Token;
        _ = PersistSettingsDebouncedAsync(cancellationToken);
    }

    private async Task PersistSettingsDebouncedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken).ConfigureAwait(true);
            await PersistSettingsSilentlyAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // 防抖取消，忽略
        }
    }

    private async Task PersistSettingsSilentlyAsync()
    {
        Settings.Authorization.ManualToken = ManualToken;
        await _appSettingsService.SaveAsync(Settings).ConfigureAwait(false);
    }

    public void PersistSettingsOnExit()
    {
        try
        {
            _settingsPersistDebounceCancellationTokenSource?.Cancel();
            Settings.Authorization.ManualToken = ManualToken;
            _appSettingsService.SaveAsync(Settings).GetAwaiter().GetResult();
        }
        catch
        {
            // 退出时保存失败不阻断关闭
        }
    }

    private async Task LoginWithManualTokenAsync()
    {
        if (string.IsNullOrWhiteSpace(ManualToken))
        {
            StatusText = "请先填写 GitHub Token";
            return;
        }

        GitHubToken = ManualToken.Trim();
        Settings.LoginMode = LoginMode.ManualToken;
        await ValidateCurrentTokenAsync().ConfigureAwait(true);
        await SaveSettingsAsync().ConfigureAwait(true);
    }

    private async Task LoginWithGitHubAuthorizeAsync()
    {
        if (IsGitHubAuthorizing)
        {
            return;
        }

        IsGitHubAuthorizing = true;
        IsBusy = true;

        try
        {
            StatusText = "正在打开浏览器，请在 GitHub 页面点击授权...";

            var oauthResult = await _gitHubOAuthService.AuthorizeWithBrowserAsync().ConfigureAwait(true);

            if (oauthResult.NeedsContinueAuthorizeAfterInstall)
            {
                Settings.Authorization.IsAppInstalled = true;
                Settings.Authorization.InstallationId = oauthResult.InstallationId ?? string.Empty;
                StatusText = "GitHub App 已安装，正在打开浏览器继续授权...";
                oauthResult = await _gitHubOAuthService.AuthorizeWithBrowserAsync().ConfigureAwait(true);
            }

            if (!oauthResult.IsSuccess || string.IsNullOrWhiteSpace(oauthResult.Token))
            {
                StatusText = oauthResult.ErrorMessage ?? "GitHub 授权失败或已取消";
                return;
            }

            await ApplyGitHubOAuthTokenAsync(oauthResult.Token).ConfigureAwait(true);
            StatusText = $"GitHub 授权登录成功：{LoginDisplayName}";
        }
        finally
        {
            IsGitHubAuthorizing = false;
            IsBusy = false;
        }
    }

    private async Task ApplyGitHubOAuthTokenAsync(string token)
    {
        GitHubToken = token;
        ManualToken = token;
        Settings.LoginMode = LoginMode.OAuth;
        Settings.Authorization.IsAuthorized = true;
        Settings.Authorization.TokenCreateTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Settings.Authorization.IsAutoAuthorize = true;

        await ValidateCurrentTokenAsync().ConfigureAwait(true);
        await SaveSettingsAsync().ConfigureAwait(true);
    }

    private void OpenInstallPage()
    {
        _gitHubOAuthService.OpenInstallPageInBrowser();
        StatusText = "已在浏览器打开 GitHub App 安装页，安装完成后请点击「GitHub 授权登录」";
    }

    private void Logout()
    {
        GitHubToken = string.Empty;
        ManualToken = string.Empty;
        CurrentUser = null;
        _appSession.CurrentUser = null;
        Settings.Authorization.IsAuthorized = false;
        Settings.Authorization.ManualToken = string.Empty;
        PersistSettingsOnExit();
        StatusText = "已退出登录（图床配置已保留）";
        RaiseLoginStateChanged();
    }

    private async Task ValidateCurrentTokenAsync()
    {
        _gitHubApiClient.SetToken(GitHubToken);
        CurrentUser = await _gitHubApiClient.GetCurrentUserAsync().ConfigureAwait(true);
        _appSession.CurrentUser = CurrentUser;

        if (CurrentUser is null)
        {
            StatusText = "Token 无效或已过期";
            RaiseLoginStateChanged();
            return;
        }

        if (string.IsNullOrWhiteSpace(Owner))
        {
            Owner = CurrentUser.Login;
        }

        await LoadBranchListAsync().ConfigureAwait(true);
        await PersistSettingsSilentlyAsync().ConfigureAwait(true);
        StatusText = $"登录成功：{CurrentUser.Login}";
        RaiseLoginStateChanged();
    }

    private async Task<bool> TryEnsureRepositoryReadyAsync(CancellationToken cancellationToken)
    {
        if (!ValidateRepositorySettings(out var validationMessage))
        {
            StatusText = validationMessage;
            return false;
        }

        _gitHubApiClient.SetToken(GitHubToken);
        return await EnsureRepositoryExistsAsync(cancellationToken).ConfigureAwait(true);
    }

    private async Task<bool> EnsureRepositoryExistsAsync(CancellationToken cancellationToken)
    {
        if (await _gitHubApiClient.RepositoryExistsAsync(Owner, Repository, cancellationToken).ConfigureAwait(true))
        {
            return true;
        }

        StatusText = $"仓库 {Owner}/{Repository} 不存在，正在自动创建…";

        var createResult = await _gitHubApiClient.CreateRepositoryAsync(
            Owner,
            Repository,
            CurrentUser?.Login,
            PicXGitHubConstants.DefaultRepositoryDescription,
            isPrivate: false,
            cancellationToken).ConfigureAwait(true);

        if (!createResult.Success)
        {
            StatusText = createResult.ErrorMessage ?? "创建仓库失败";
            return false;
        }

        var defaultBranch = createResult.DefaultBranch
            ?? await _gitHubApiClient.GetDefaultBranchAsync(Owner, Repository, cancellationToken).ConfigureAwait(true)
            ?? "main";

        var initBranch = string.IsNullOrWhiteSpace(Branch) ? defaultBranch : Branch;
        var branches = await _gitHubApiClient.GetBranchNamesAsync(Owner, Repository, cancellationToken).ConfigureAwait(true);
        if (branches.Count == 0)
        {
            StatusText = "正在初始化仓库 README…";
            var initResult = await _gitHubApiClient.InitializeRepositoryReadmeAsync(
                Owner,
                Repository,
                initBranch,
                cancellationToken).ConfigureAwait(true);

            if (!initResult.Success)
            {
                StatusText = initResult.ErrorMessage ?? "初始化仓库失败";
                return false;
            }
        }

        await LoadBranchListAsync().ConfigureAwait(true);

        if (AvailableBranches.Count > 0 &&
            (string.IsNullOrWhiteSpace(Branch) || !AvailableBranches.Contains(Branch)))
        {
            Branch = AvailableBranches.Contains(defaultBranch) ? defaultBranch : AvailableBranches[0];
        }
        else if (string.IsNullOrWhiteSpace(Branch))
        {
            Branch = defaultBranch;
        }

        StatusText = createResult.AlreadyExisted
            ? $"仓库 {Owner}/{Repository} 已存在"
            : $"已自动创建仓库 {Owner}/{Repository}";
        SchedulePersistSettings();
        return true;
    }

    private async Task LoadBranchListAsync()
    {
        AvailableBranches.Clear();
        if (string.IsNullOrWhiteSpace(Owner) || string.IsNullOrWhiteSpace(Repository))
        {
            return;
        }

        _gitHubApiClient.SetToken(GitHubToken);
        var branches = await _gitHubApiClient.GetBranchNamesAsync(Owner, Repository).ConfigureAwait(true);
        foreach (var branch in branches)
        {
            AvailableBranches.Add(branch);
        }

        if (string.IsNullOrWhiteSpace(Branch))
        {
            Branch = await _gitHubApiClient.GetDefaultBranchAsync(Owner, Repository).ConfigureAwait(true) ?? "main";
        }
    }

    private void PasteImagesFromClipboard() => TryImportClipboardImages();

    private void SelectUploadLinkFormat(string? formatOption)
    {
        if (string.IsNullOrWhiteSpace(formatOption))
        {
            return;
        }

        switch (formatOption)
        {
            case "URL":
                Settings.UserSettings.ImageLinkFormat.Enable = false;
                Settings.UserSettings.ImageLinkFormat.SelectedFormat = ImageLinkFormatType.Plain;
                break;
            case "Markdown":
                Settings.UserSettings.ImageLinkFormat.Enable = true;
                Settings.UserSettings.ImageLinkFormat.SelectedFormat = ImageLinkFormatType.Markdown;
                break;
            case "HTML":
                Settings.UserSettings.ImageLinkFormat.Enable = true;
                Settings.UserSettings.ImageLinkFormat.SelectedFormat = ImageLinkFormatType.Html;
                break;
            case "UBB":
                Settings.UserSettings.ImageLinkFormat.Enable = true;
                Settings.UserSettings.ImageLinkFormat.SelectedFormat = ImageLinkFormatType.BbCode;
                break;
            case "Custom":
                Settings.UserSettings.ImageLinkFormat.Enable = true;
                Settings.UserSettings.ImageLinkFormat.SelectedFormat = ImageLinkFormatType.Custom;
                break;
            default:
                return;
        }

        NotifyUploadLinkFormatBindingsChanged();
        SchedulePersistSettings();
    }

    private string ResolveUploadLinkFormatOption()
    {
        if (!Settings.UserSettings.ImageLinkFormat.Enable)
        {
            return "URL";
        }

        return Settings.UserSettings.ImageLinkFormat.SelectedFormat switch
        {
            ImageLinkFormatType.Markdown => "Markdown",
            ImageLinkFormatType.Html => "HTML",
            ImageLinkFormatType.BbCode => "UBB",
            ImageLinkFormatType.Custom => "Custom",
            _ => "URL"
        };
    }

    private void NotifyUploadLinkFormatBindingsChanged()
    {
        OnPropertyChanged(nameof(EnableLinkFormat));
        OnPropertyChanged(nameof(SelectedLinkFormat));
        OnPropertyChanged(nameof(SelectedUploadLinkFormatOption));
        OnPropertyChanged(nameof(IsCustomUploadLinkFormatPanelVisible));
    }

    private async Task ImportImageFromUrlAsync()
    {
        var promptDialog = new TextPromptDialog("请输入图片 URL（http / https）：")
        {
            Owner = Application.Current.MainWindow
        };

        if (promptDialog.ShowDialog() != true)
        {
            return;
        }

        var imageUrl = promptDialog.EnteredText?.Trim();
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            StatusText = "未输入图片 URL";
            return;
        }

        IsBusy = true;
        try
        {
            var localImagePath = await ImageUrlImportHelper
                .DownloadImageToTempFileAsync(imageUrl, CancellationToken.None)
                .ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(localImagePath) || !ImageFileHelper.IsImageFile(localImagePath))
            {
                StatusText = "无法从 URL 下载或识别为图片";
                return;
            }

            AddImageFiles([localImagePath]);
            StatusText = "已从 URL 添加 1 张图片";
        }
        catch (Exception exception)
        {
            StatusText = $"URL 导入失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddImagesFromDialog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择图片（可多选）",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp;*.avif|所有文件|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            AddImageFiles(dialog.FileNames);
        }
    }

    private void SelectAllUploadItems()
    {
        foreach (var item in UploadItems)
        {
            item.IsSelected = true;
        }
    }

    private void ClearUploadItemSelection()
    {
        foreach (var item in UploadItems)
        {
            item.IsSelected = false;
        }
    }

    private bool CanRemoveSelectedUploadItems() =>
        !IsBusy && (UploadItems.Any(item => item.IsSelected) || SelectedUploadItem is not null);

    private void RemoveSelectedUpload()
    {
        var itemsToRemove = UploadItems.Where(item => item.IsSelected).ToList();
        if (itemsToRemove.Count == 0 && SelectedUploadItem is not null)
        {
            itemsToRemove.Add(SelectedUploadItem);
        }

        foreach (var item in itemsToRemove)
        {
            UploadItems.Remove(item);
        }

        SelectedUploadItem = null;
        RaiseUploadListCommandStates();
    }

    private void ClearUploadList()
    {
        UploadItems.Clear();
        RaiseUploadListCommandStates();
    }

    private void HookUploadItemSelectionChanged(UploadItem uploadItem)
    {
        uploadItem.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(UploadItem.IsSelected))
            {
                RemoveSelectedUploadCommand.RaiseCanExecuteChanged();
                ClearUploadItemSelectionCommand.RaiseCanExecuteChanged();
            }
        };
    }

    private void ReportUploadProgress(int completedCount, int totalCount, UploadItem? currentItem = null, string? phaseDescription = null)
    {
        if (totalCount <= 0)
        {
            UploadOverallProgressPercent = 0;
            UploadProgressText = "上传中…";
            return;
        }

        var currentItemProgress = currentItem?.ProgressPercent ?? 0;
        UploadOverallProgressPercent = Math.Min(100, (completedCount * 100.0 + currentItemProgress) / totalCount);

        if (!string.IsNullOrWhiteSpace(phaseDescription) && currentItem is not null)
        {
            UploadProgressText = totalCount == 1
                ? $"上传中：{currentItem.DisplayFileName} · {phaseDescription}（{UploadOverallProgressPercent:F0}%）"
                : $"上传中 {Math.Min(completedCount + 1, totalCount)}/{totalCount} · {phaseDescription}（{UploadOverallProgressPercent:F0}%）";
            return;
        }

        UploadProgressText = totalCount == 1
            ? $"上传中：{currentItem?.DisplayFileName ?? "处理中"}（{UploadOverallProgressPercent:F0}%）"
            : $"上传中 {Math.Min(completedCount + 1, totalCount)}/{totalCount}（{UploadOverallProgressPercent:F0}%）";
    }

    private void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    private void StopUploadProgressSmoother()
    {
        _uploadProgressSmoother?.Dispose();
        _uploadProgressSmoother = null;
    }

    private void StartUploadProgressSmoother(
        UploadItem item,
        int fromPercent,
        int toPercent,
        int completedCount,
        int totalCount,
        string phaseDescription)
    {
        StopUploadProgressSmoother();
        _uploadProgressSmoother = new UploadProgressSmoother(fromPercent, toPercent, percent =>
        {
            item.ProgressPercent = percent;
            ReportUploadProgress(completedCount, totalCount, item, phaseDescription);
        });
    }

    private static int MapGitHubContentUploadPhaseToPercent(GitHubContentUploadPhase phase) =>
        phase switch
        {
            GitHubContentUploadPhase.CheckingRemoteFile => 48,
            GitHubContentUploadPhase.EncodingFileContent => 58,
            GitHubContentUploadPhase.SubmittingToRepository => 68,
            _ => 42
        };

    private static string DescribeGitHubContentUploadPhase(GitHubContentUploadPhase phase) =>
        phase switch
        {
            GitHubContentUploadPhase.CheckingRemoteFile => "检查远程文件",
            GitHubContentUploadPhase.EncodingFileContent => "准备上传数据",
            GitHubContentUploadPhase.SubmittingToRepository => "提交到 GitHub",
            _ => "上传中"
        };

    private void HandleGitHubContentUploadPhase(
        UploadItem item,
        GitHubContentUploadPhase phase,
        int completedCount,
        int totalCount)
    {
        var phasePercent = MapGitHubContentUploadPhaseToPercent(phase);
        var phaseDescription = DescribeGitHubContentUploadPhase(phase);

        if (phase == GitHubContentUploadPhase.SubmittingToRepository)
        {
            var startPercent = Math.Max(phasePercent, item.ProgressPercent);
            StartUploadProgressSmoother(item, startPercent, 92, completedCount, totalCount, phaseDescription);
            return;
        }

        StopUploadProgressSmoother();
        item.ProgressPercent = phasePercent;
        ReportUploadProgress(completedCount, totalCount, item, phaseDescription);
    }

    private static int MapBatchGitUploadPhaseToPercent(BatchGitUploadPhase phase, int completedBlobCount, int totalBlobCount)
    {
        return phase switch
        {
            BatchGitUploadPhase.FetchingBranchInfo => 55,
            BatchGitUploadPhase.CreatingBlob => 58 + (int)(completedBlobCount * 24.0 / Math.Max(1, totalBlobCount)),
            BatchGitUploadPhase.CreatingTree => 84,
            BatchGitUploadPhase.CreatingCommit => 90,
            BatchGitUploadPhase.UpdatingBranchReference => 95,
            _ => 55
        };
    }

    private static string DescribeBatchGitUploadPhase(BatchGitUploadPhase phase) =>
        phase switch
        {
            BatchGitUploadPhase.FetchingBranchInfo => "获取分支信息",
            BatchGitUploadPhase.CreatingBlob => "上传文件数据",
            BatchGitUploadPhase.CreatingTree => "创建目录树",
            BatchGitUploadPhase.CreatingCommit => "创建提交",
            BatchGitUploadPhase.UpdatingBranchReference => "更新分支",
            _ => "提交到 GitHub"
        };

    private void HandleBatchGitUploadPhase(
        IReadOnlyList<UploadItem> uploadingItems,
        BatchGitUploadPhase phase,
        int completedBlobCount,
        int totalBlobCount,
        int completedItemCount,
        int totalItemCount)
    {
        var phasePercent = MapBatchGitUploadPhaseToPercent(phase, completedBlobCount, totalBlobCount);
        var phaseDescription = DescribeBatchGitUploadPhase(phase);

        foreach (var item in uploadingItems)
        {
            item.ProgressPercent = phasePercent;
        }

        if (phase == BatchGitUploadPhase.CreatingBlob)
        {
            StartUploadProgressSmoother(
                uploadingItems[0],
                phasePercent,
                Math.Min(phasePercent + 6, 82),
                completedItemCount,
                totalItemCount,
                phaseDescription);
            return;
        }

        if (phase == BatchGitUploadPhase.UpdatingBranchReference)
        {
            StartUploadProgressSmoother(
                uploadingItems[0],
                phasePercent,
                98,
                completedItemCount,
                totalItemCount,
                phaseDescription);
            return;
        }

        StopUploadProgressSmoother();
        ReportUploadProgress(completedItemCount, totalItemCount, uploadingItems.FirstOrDefault(), phaseDescription);
    }

    private void ResetUploadProgress()
    {
        IsUploadProgressVisible = true;
        UploadOverallProgressPercent = 0;
        UploadProgressText = "准备上传…";
    }

    private void HideUploadProgress()
    {
        IsUploadProgressVisible = false;
        UploadOverallProgressPercent = 0;
        UploadProgressText = string.Empty;
    }

    private bool CanUploadImages() =>
        !IsBusy &&
        IsLoggedIn &&
        UploadItems.Any(item => item.Status is UploadItemStatus.Pending or UploadItemStatus.Failed);

    private async Task UploadImagesAsync()
    {
        var pendingItems = UploadItems
            .Where(item => item.Status is UploadItemStatus.Pending or UploadItemStatus.Failed)
            .ToList();

        await UploadPendingItemsInternalAsync(pendingItems, copyEachLinkToClipboardSequentially: false).ConfigureAwait(true);
    }

    private async Task UploadPendingItemsInternalAsync(
        IReadOnlyList<UploadItem> pendingItems,
        bool copyEachLinkToClipboardSequentially)
    {
        if (pendingItems.Count == 0)
        {
            return;
        }

        IsBusy = true;
        ResetUploadProgress();
        _operationCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _operationCancellationTokenSource.Token;

        try
        {
            if (!await TryEnsureRepositoryReadyAsync(cancellationToken).ConfigureAwait(true))
            {
                return;
            }

            var branch = await ResolveBranchNameAsync(cancellationToken).ConfigureAwait(true);
            var targetDirectory = FileNameHelper.BuildTargetDirectoryPath(DirectoryMode, TargetDirectoryPath);
            ReportUploadProgress(0, pendingItems.Count);

            foreach (var item in pendingItems)
            {
                var customName = string.Equals(item.DisplayFileName, item.OriginalFileName, StringComparison.OrdinalIgnoreCase)
                    ? null
                    : item.DisplayFileName;
                item.RemoteFileName = FileNameHelper.BuildRemoteFileName(item.OriginalFileName, Settings.UserSettings, customName);
            }

            if (pendingItems.Count == 1)
            {
                await UploadSingleItemAsync(pendingItems[0], targetDirectory, branch, 0, 1, cancellationToken).ConfigureAwait(true);
            }
            else
            {
                await UploadMultipleItemsAsync(pendingItems, targetDirectory, branch, cancellationToken).ConfigureAwait(true);
            }

            ReportUploadProgress(pendingItems.Count, pendingItems.Count);

            var uploadedItems = pendingItems.Where(item => item.Status == UploadItemStatus.Uploaded).ToList();
            var publishSucceeded = false;
            if (uploadedItems.Count > 0)
            {
                publishSucceeded = await ApplyPostUploadPublishAsync(uploadedItems, branch, cancellationToken).ConfigureAwait(true);
            }

            if (copyEachLinkToClipboardSequentially)
            {
                CopyUploadedItemLinksToClipboardSequentially(pendingItems);
            }
            else if (Settings.UserSettings.AutoCopyLinkAfterUpload)
            {
                CopyUploadedItemLinksToClipboardBatch(pendingItems);
            }

            var failedCount = pendingItems.Count(item => item.Status == UploadItemStatus.Failed);
            var successCount = pendingItems.Count(item => item.Status == UploadItemStatus.Uploaded);
            var pagesSuffix = successCount > 0
                ? BuildPostUploadStatusSuffix(publishSucceeded)
                : string.Empty;
            StatusText = failedCount > 0
                ? $"上传完成：成功 {successCount}，失败 {failedCount}（请查看列表「错误」列）{pagesSuffix}"
                : $"上传完成：成功 {successCount} 个文件{pagesSuffix}";

            if (successCount > 0)
            {
                await SyncManagementViewAfterUploadAsync().ConfigureAwait(true);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "上传已取消";
        }
        catch (Exception ex)
        {
            StatusText = $"上传失败：{ex.Message}";
        }
        finally
        {
            StopUploadProgressSmoother();
            HideUploadProgress();
            IsBusy = false;
            _operationCancellationTokenSource?.Dispose();
            _operationCancellationTokenSource = null;
        }
    }

    private void CopyUploadedItemLinksToClipboardSequentially(IReadOnlyList<UploadItem> pendingItems)
    {
        foreach (var item in pendingItems)
        {
            if (item.Status != UploadItemStatus.Uploaded)
            {
                continue;
            }

            var imageLink = item.GitHubPagesImageUrl ?? item.ImageUrl;
            if (string.IsNullOrWhiteSpace(imageLink))
            {
                continue;
            }

            LastGeneratedLink = imageLink;
            CopyToClipboard(imageLink);
        }

        CopyLastLinkCommand.RaiseCanExecuteChanged();
    }

    private void CopyUploadedItemLinksToClipboardBatch(IReadOnlyList<UploadItem> pendingItems)
    {
        var uploadedLinks = pendingItems
            .Where(item => item.Status == UploadItemStatus.Uploaded &&
                           !string.IsNullOrWhiteSpace(item.GitHubPagesImageUrl ?? item.ImageUrl))
            .Select(item => item.GitHubPagesImageUrl ?? item.ImageUrl!)
            .ToList();

        if (uploadedLinks.Count == 1)
        {
            LastGeneratedLink = uploadedLinks[0];
            CopyToClipboard(LastGeneratedLink);
        }
        else if (uploadedLinks.Count > 1)
        {
            LastGeneratedLink = string.Join(Environment.NewLine, uploadedLinks);
            CopyToClipboard(LastGeneratedLink);
        }

        CopyLastLinkCommand.RaiseCanExecuteChanged();
    }

    private async Task UploadSingleItemAsync(
        UploadItem item,
        string targetDirectory,
        string branch,
        int completedCount,
        int totalCount,
        CancellationToken cancellationToken)
    {
        item.Status = UploadItemStatus.Uploading;
        item.ProgressPercent = 8;
        ReportUploadProgress(completedCount, totalCount, item, "处理图片");

        try
        {
            var processedBytes = await _imageProcessingService.ProcessForUploadAsync(item.LocalFilePath, Settings.UserSettings, cancellationToken).ConfigureAwait(true);
            item.ProcessedBytes = processedBytes;
            item.ProgressPercent = 32;
            ReportUploadProgress(completedCount, totalCount, item, "图片处理完成");

            var remotePath = FileNameHelper.BuildRemotePath(targetDirectory, item.RemoteFileName);
            var committerName = CurrentUser?.Login ?? Owner;
            var result = await _gitHubApiClient.UploadContentAsync(
                Owner,
                Repository,
                branch,
                remotePath,
                CommitMessage,
                processedBytes,
                committerName,
                Email,
                cancellationToken,
                phase => RunOnUiThread(() => HandleGitHubContentUploadPhase(item, phase, completedCount, totalCount))).ConfigureAwait(true);

            StopUploadProgressSmoother();

            if (!result.Success)
            {
                item.Status = UploadItemStatus.Failed;
                item.ErrorMessage = result.ErrorMessage;
                item.ProgressPercent = 0;
                return;
            }

            item.RemotePath = result.RemotePath ?? remotePath;
            item.ContentSha = result.ContentSha;
            item.CommitSha = result.CommitSha;
            item.Status = UploadItemStatus.Uploaded;
            item.ProgressPercent = 100;
            ReportUploadProgress(completedCount + 1, totalCount, item);
        }
        catch (Exception ex)
        {
            StopUploadProgressSmoother();
            item.Status = UploadItemStatus.Failed;
            item.ErrorMessage = ex.Message;
            item.ProgressPercent = 0;
            ReportUploadProgress(completedCount + 1, totalCount, item);
        }
    }

    private async Task UploadMultipleItemsAsync(IReadOnlyList<UploadItem> items, string targetDirectory, string branch, CancellationToken cancellationToken)
    {
        var uploadPayload = new List<(string RemotePath, byte[] FileBytes)>();

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            item.Status = UploadItemStatus.Uploading;
            var processingStartPercent = 5 + (int)(index * 40.0 / items.Count);
            item.ProgressPercent = processingStartPercent;
            ReportUploadProgress(index, items.Count, item, "处理图片");

            var processedBytes = await _imageProcessingService.ProcessForUploadAsync(item.LocalFilePath, Settings.UserSettings, cancellationToken).ConfigureAwait(true);
            item.ProcessedBytes = processedBytes;
            var remotePath = FileNameHelper.BuildRemotePath(targetDirectory, item.RemoteFileName);
            item.RemotePath = remotePath;
            uploadPayload.Add((remotePath, processedBytes));
            var processingEndPercent = 12 + (int)((index + 1) * 40.0 / items.Count);
            item.ProgressPercent = processingEndPercent;
            ReportUploadProgress(index, items.Count, item, "图片处理完成");
        }

        var uploadingItems = items.Where(item => item.Status == UploadItemStatus.Uploading).ToList();
        HandleBatchGitUploadPhase(uploadingItems, BatchGitUploadPhase.FetchingBranchInfo, 0, items.Count, items.Count - 1, items.Count);

        var batchResult = await _gitHubApiClient.UploadMultipleAsync(
            Owner,
            Repository,
            branch,
            CommitMessage,
            uploadPayload,
            cancellationToken,
            (phase, completedBlobCount, totalBlobCount) =>
                RunOnUiThread(() => HandleBatchGitUploadPhase(uploadingItems, phase, completedBlobCount, totalBlobCount, items.Count - 1, items.Count))).ConfigureAwait(true);

        StopUploadProgressSmoother();
        if (!batchResult.Success)
        {
            foreach (var item in items)
            {
                item.Status = UploadItemStatus.Failed;
                item.ErrorMessage = batchResult.ErrorMessage;
                item.ProgressPercent = 0;
            }

            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            item.CommitSha = batchResult.CommitSha;
            item.Status = UploadItemStatus.Uploaded;
            item.ProgressPercent = 100;
            ReportUploadProgress(index + 1, items.Count, item);
        }
    }

    private async Task<bool> ApplyPostUploadPublishAsync(
        IReadOnlyList<UploadItem> uploadedItems,
        string branch,
        CancellationToken cancellationToken)
    {
        var publishMode = Settings.UserSettings.PostUploadPublishMode;
        var deploySucceeded = true;

        if (publishMode == PostUploadPublishMode.GitHubPages)
        {
            UploadProgressText = "正在部署 GitHub Pages…";
            deploySucceeded = await _gitHubApiClient.DeployGitHubPagesBranchAsync(
                Owner, Repository, branch, cancellationToken).ConfigureAwait(true);
        }
        else
        {
            UploadProgressText = "正在生成 jsDelivr 链接…";
            await ApplyJsDelivrTagReferenceIfNeededAsync(uploadedItems, cancellationToken).ConfigureAwait(true);
        }

        ApplyPublishedLinksToUploadItems(uploadedItems, branch, publishMode, deploySucceeded);
        return deploySucceeded;
    }

    private async Task ApplyJsDelivrTagReferenceIfNeededAsync(
        IReadOnlyList<UploadItem> uploadedItems,
        CancellationToken cancellationToken)
    {
        if (Settings.UserSettings.JsDelivrVersionReferenceMode != JsDelivrVersionReferenceMode.Tag)
        {
            return;
        }

        var tagName = Settings.UserSettings.JsDelivrTagName?.Trim();
        if (string.IsNullOrWhiteSpace(tagName))
        {
            StatusText = "jsDelivr Tag 模式需要填写标签名";
            return;
        }

        var commitSha = uploadedItems
            .Select(item => item.CommitSha)
            .FirstOrDefault(sha => !string.IsNullOrWhiteSpace(sha));

        if (string.IsNullOrWhiteSpace(commitSha))
        {
            return;
        }

        var tagUpdated = await _gitHubApiClient
            .CreateOrUpdateTagReferenceAsync(Owner, Repository, tagName, commitSha, cancellationToken)
            .ConfigureAwait(true);

        if (!tagUpdated)
        {
            StatusText = $"jsDelivr 标签「{tagName}」更新失败，链接可能仍指向旧提交";
        }
    }

    private void ApplyPublishedLinksToUploadItems(
        IReadOnlyList<UploadItem> uploadedItems,
        string branch,
        PostUploadPublishMode publishMode,
        bool deploySucceeded)
    {
        foreach (var item in uploadedItems)
        {
            if (string.IsNullOrWhiteSpace(item.RemotePath))
            {
                continue;
            }

            var publishedUrl = _imageLinkGenerator.GeneratePublishedImageUrl(
                Settings,
                item.RemotePath,
                branch,
                publishMode,
                item.CommitSha);

            item.GitHubPagesImageUrl = publishedUrl;
            item.ImageUrl = publishMode == PostUploadPublishMode.GitHubPages && !deploySucceeded
                ? _imageLinkGenerator.GenerateImageUrl(Settings, item.RemotePath, branch)
                : publishedUrl;
        }

        CopyUploadItemGitHubPagesLinkCommand.RaiseCanExecuteChanged();
    }

    private void NotifyJsDelivrConfigurationBindingsChanged()
    {
        OnPropertyChanged(nameof(IsJsDelivrVersionReferenceConfigurationVisible));
        OnPropertyChanged(nameof(IsJsDelivrTagNameConfigurationVisible));
    }

    private string BuildPostUploadStatusSuffix(bool publishSucceeded)
    {
        return Settings.UserSettings.PostUploadPublishMode switch
        {
            PostUploadPublishMode.JsDelivr => BuildJsDelivrPostUploadStatusSuffix(),
            PostUploadPublishMode.GitHubPages when publishSucceeded => "，已部署 GitHub Pages（约 1 分钟生效）",
            PostUploadPublishMode.GitHubPages => "，GitHub Pages 部署失败（可复制预生成链接，生效需手动部署）",
            _ => string.Empty
        };
    }

    private string BuildJsDelivrPostUploadStatusSuffix()
    {
        return Settings.UserSettings.JsDelivrVersionReferenceMode switch
        {
            JsDelivrVersionReferenceMode.CommitHash => "，已生成 jsDelivr 链接（Commit Hash，覆盖同路径后为新链接）",
            JsDelivrVersionReferenceMode.Tag => $"，已生成 jsDelivr 链接（Tag：{Settings.UserSettings.JsDelivrTagName}）",
            _ => "，已生成 jsDelivr 链接（分支名，覆盖同路径时 CDN 可能仍缓存旧图）"
        };
    }

    private void CopyUploadItemGitHubPagesLink(UploadItem? uploadItem)
    {
        if (uploadItem is null)
        {
            return;
        }

        var linkToCopy = uploadItem.GitHubPagesImageUrl ?? uploadItem.ImageUrl;
        if (string.IsNullOrWhiteSpace(linkToCopy))
        {
            StatusText = "暂无可复制的图片链接";
            return;
        }

        CopyToClipboard(linkToCopy);
        var publishLabel = Settings.UserSettings.PostUploadPublishMode == PostUploadPublishMode.JsDelivr
            ? "jsDelivr"
            : "GitHub Pages";
        StatusText = $"已复制 {publishLabel} 图片链接";
    }

    private async Task LoadAllRepositoryImagesAsync()
    {
        IsBusy = true;
        NotifyManagedImageGalleryChanged();
        try
        {
            if (!await TryEnsureRepositoryReadyAsync(CancellationToken.None).ConfigureAwait(true))
            {
                return;
            }

            ManagedImages.Clear();
            NotifyManagedImageGalleryChanged();

            var branch = await ResolveBranchNameAsync(CancellationToken.None).ConfigureAwait(true);
            var repositoryImageFiles = await _gitHubApiClient
                .GetAllRepositoryImageFilesAsync(Owner, Repository, branch, CancellationToken.None)
                .ConfigureAwait(true);

            _managedImageThumbnailLoadCancellationTokenSource?.Cancel();
            _managedImageThumbnailLoadCancellationTokenSource?.Dispose();
            _managedImageThumbnailLoadCancellationTokenSource = new CancellationTokenSource();
            var thumbnailLoadCancellation = _managedImageThumbnailLoadCancellationTokenSource.Token;

            foreach (var file in repositoryImageFiles.OrderByDescending(item => item.Path, StringComparer.OrdinalIgnoreCase))
            {
                var directoryPath = Path.GetDirectoryName(file.Path.Replace('\\', '/'))?.Replace('\\', '/') ?? string.Empty;
                var image = new ManagedImageItem
                {
                    Name = file.Name,
                    Path = file.Path,
                    DirectoryPath = directoryPath,
                    Sha = file.Sha,
                    SizeBytes = file.SizeBytes,
                    ImageUrl = _imageLinkGenerator.GenerateImageUrl(Settings, file.Path, branch)
                };
                HookManagedImageItemSelectionChanged(image);
                ManagedImages.Add(image);
                _ = LoadManagedImageThumbnailAsync(image, branch, thumbnailLoadCancellation);
            }

            StatusText = $"已加载 {ManagedImages.Count} 张图片";
        }
        finally
        {
            IsBusy = false;
            NotifyManagedImageGalleryChanged();
        }
    }

    private void SelectAllManagedImages()
    {
        foreach (var image in ManagedImages)
        {
            image.IsSelected = true;
        }

        NotifyManagedImageGalleryChanged();
    }

    private void ClearManagedImageSelection()
    {
        foreach (var image in ManagedImages)
        {
            image.IsSelected = false;
        }

        NotifyManagedImageGalleryChanged();
    }

    private void HookManagedImageItemSelectionChanged(ManagedImageItem managedImage)
    {
        managedImage.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(ManagedImageItem.IsSelected) or nameof(ManagedImageItem.IsDeleting))
            {
                DeleteSelectedImagesCommand.RaiseCanExecuteChanged();
                CopySelectedImageLinksCommand.RaiseCanExecuteChanged();
                ClearManagedImageSelectionCommand.RaiseCanExecuteChanged();
                NotifyManagedImageGalleryChanged();
            }
        };
    }

    private void NotifyManagedImageGalleryChanged()
    {
        OnPropertyChanged(nameof(IsManagedImageGalleryEmpty));
        OnPropertyChanged(nameof(ManagedImageGallerySummary));
    }

    private async Task LoadManagedImageThumbnailAsync(
        ManagedImageItem managedImage,
        string branch,
        CancellationToken cancellationToken)
    {
        await _managedImageThumbnailLoadSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RunOnUiThread(() => managedImage.IsThumbnailLoading = true);

            var candidateUrls = _imageLinkGenerator.BuildThumbnailCandidateUrls(Settings, managedImage.Path, branch);
            var thumbnail = await _remoteThumbnailLoader
                .TryLoadFirstAvailableAsync(candidateUrls, GitHubToken, cancellationToken)
                .ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            RunOnUiThread(() =>
            {
                managedImage.ThumbnailSource = thumbnail;
                managedImage.IsThumbnailLoading = false;
            });
        }
        catch (OperationCanceledException)
        {
            RunOnUiThread(() => managedImage.IsThumbnailLoading = false);
        }
        catch
        {
            RunOnUiThread(() => managedImage.IsThumbnailLoading = false);
        }
        finally
        {
            _managedImageThumbnailLoadSemaphore.Release();
        }
    }

    private async Task DeleteSelectedImagesAsync()
    {
        var selectedImages = ManagedImages.Where(item => item.IsSelected).ToList();
        if (selectedImages.Count == 0)
        {
            return;
        }

        IsBusy = true;
        _operationCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _operationCancellationTokenSource.Token;

        try
        {
            _gitHubApiClient.SetToken(GitHubToken);
            var branch = await ResolveBranchNameAsync(cancellationToken).ConfigureAwait(true);

            foreach (var image in selectedImages)
            {
                image.IsDeleting = true;
            }

            if (selectedImages.Count == 1)
            {
                var image = selectedImages[0];
                var result = await _gitHubApiClient.DeleteContentAsync(Owner, Repository, branch, image.Path, image.Sha, "Delete image by PicX-WPF", cancellationToken).ConfigureAwait(true);
                if (!result.Success)
                {
                    image.IsDeleting = false;
                    StatusText = result.ErrorMessage ?? "删除失败";
                    return;
                }

                ManagedImages.Remove(image);
            }
            else
            {
                var batchResult = await _gitHubApiClient.DeleteMultipleAsync(
                    Owner,
                    Repository,
                    branch,
                    "Delete images by PicX-WPF",
                    selectedImages.Select(item => item.Path).ToList(),
                    cancellationToken).ConfigureAwait(true);

                if (!batchResult.Success)
                {
                    foreach (var image in selectedImages)
                    {
                        image.IsDeleting = false;
                    }

                    StatusText = batchResult.ErrorMessage ?? "批量删除失败";
                    return;
                }

                foreach (var image in selectedImages.ToList())
                {
                    ManagedImages.Remove(image);
                }
            }

            StatusText = "删除完成";
            NotifyManagedImageGalleryChanged();
        }
        finally
        {
            IsBusy = false;
            _operationCancellationTokenSource?.Dispose();
            _operationCancellationTokenSource = null;
            NotifyManagedImageGalleryChanged();
        }
    }

    private void CopySelectedImageLinks()
    {
        var links = ManagedImages.Where(item => item.IsSelected && !string.IsNullOrWhiteSpace(item.ImageUrl))
            .Select(item => item.ImageUrl!)
            .ToList();

        if (links.Count == 0)
        {
            return;
        }

        LastGeneratedLink = string.Join(Environment.NewLine, links);
        CopyToClipboard(LastGeneratedLink);
        StatusText = $"已复制 {links.Count} 条链接";
    }

    private async Task DeployGitHubPagesAsync()
    {
        IsBusy = true;
        try
        {
            if (!await TryEnsureRepositoryReadyAsync(CancellationToken.None).ConfigureAwait(true))
            {
                return;
            }

            var branch = await ResolveBranchNameAsync(CancellationToken.None).ConfigureAwait(true);
            var success = await _gitHubApiClient.DeployGitHubPagesBranchAsync(Owner, Repository, branch).ConfigureAwait(true);
            StatusText = success ? "gh-pages 分支已创建，GitHub Pages 部署可能需要约 1 分钟生效" : "GitHub Pages 部署失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddCustomLinkRule()
    {
        if (string.IsNullOrWhiteSpace(CustomLinkRuleTemplate) || !CustomLinkRuleTemplate.Contains("{{path}}", StringComparison.Ordinal))
        {
            StatusText = "自定义规则必须包含 {{path}}";
            return;
        }

        var customName = $"Custom-{Settings.UserSettings.ImageLinkType.PresetRules.Count + 1}";
        Settings.UserSettings.ImageLinkType.PresetRules.Add(new ImageLinkRule
        {
            Name = customName,
            RuleTemplate = CustomLinkRuleTemplate,
            IsCustom = true
        });

        RefreshLinkRuleNames();
        SelectedLinkRuleName = customName;
        CustomLinkRuleTemplate = string.Empty;
        StatusText = "已添加自定义链接规则";
    }

    private void SelectToolboxFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择工具箱图片",
            Filter = "图片文件|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp;*.avif|所有文件|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            ToolboxSourceFilePath = dialog.FileName;
        }
    }

    private async Task RunToolboxCompressAsync()
    {
        IsBusy = true;
        try
        {
            var bytes = await _imageProcessingService.CompressAsync(
                await File.ReadAllBytesAsync(ToolboxSourceFilePath).ConfigureAwait(true),
                Settings.UserSettings.Compress.Encoder).ConfigureAwait(true);

            var outputPath = BuildToolboxOutputPath("compressed");
            await _imageProcessingService.SaveBytesToFileAsync(bytes, outputPath).ConfigureAwait(true);
            ToolboxProcessedFiles.Add(outputPath);
            StatusText = $"压缩完成：{outputPath}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunToolboxBase64Async()
    {
        ToolboxBase64Result = await _imageProcessingService.ConvertToBase64Async(ToolboxSourceFilePath).ConfigureAwait(true);
        StatusText = "Base64 转换完成";
    }

    private async Task RunToolboxWatermarkAsync()
    {
        IsBusy = true;
        try
        {
            var sourceBytes = await File.ReadAllBytesAsync(ToolboxSourceFilePath).ConfigureAwait(true);
            var bytes = await _imageProcessingService.ApplyWatermarkAsync(sourceBytes, Settings.UserSettings.Watermark).ConfigureAwait(true);
            var outputPath = BuildToolboxOutputPath("watermark");
            await _imageProcessingService.SaveBytesToFileAsync(bytes, outputPath).ConfigureAwait(true);
            ToolboxProcessedFiles.Add(outputPath);
            StatusText = $"水印完成：{outputPath}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void CopyToolboxBase64()
    {
        CopyToClipboard(ToolboxBase64Result);
        StatusText = "Base64 已复制";
    }

    private void CopyLastLink()
    {
        CopyToClipboard(LastGeneratedLink);
        StatusText = "链接已复制";
    }

    private void CancelOperation() => _operationCancellationTokenSource?.Cancel();

    private string BuildToolboxOutputPath(string suffix)
    {
        var sourceFile = new FileInfo(ToolboxSourceFilePath);
        var outputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PicX-WPF", "Toolbox");
        Directory.CreateDirectory(outputDirectory);
        return Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(sourceFile.Name)}_{suffix}{sourceFile.Extension}");
    }

    private async Task<string> ResolveBranchNameAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(Branch))
        {
            return Branch;
        }

        return await _gitHubApiClient.GetDefaultBranchAsync(Owner, Repository, cancellationToken).ConfigureAwait(false) ?? "main";
    }

    private bool ValidateRepositorySettings(out string message)
    {
        if (!IsLoggedIn)
        {
            message = "请先登录";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Owner) || string.IsNullOrWhiteSpace(Repository))
        {
            message = "请填写 Owner 和 Repository";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            StatusText = "复制失败";
        }
    }

    private void RefreshLinkRuleNames()
    {
        LinkRuleNames.Clear();
        foreach (var rule in Settings.UserSettings.ImageLinkType.PresetRules)
        {
            LinkRuleNames.Add(rule.Name);
        }
    }

    private void NotifyAllSettingsProperties()
    {
        OnPropertyChanged(nameof(GitHubToken));
        OnPropertyChanged(nameof(Owner));
        OnPropertyChanged(nameof(Repository));
        OnPropertyChanged(nameof(Branch));
        OnPropertyChanged(nameof(Email));
        OnPropertyChanged(nameof(TargetDirectoryPath));
        OnPropertyChanged(nameof(DirectoryMode));
        OnPropertyChanged(nameof(CommitMessage));
        OnPropertyChanged(nameof(EnableHash));
        OnPropertyChanged(nameof(EnablePrefix));
        OnPropertyChanged(nameof(ImageNamePrefix));
        OnPropertyChanged(nameof(EnableCompress));
        OnPropertyChanged(nameof(CompressEncoder));
        OnPropertyChanged(nameof(EnableWatermark));
        OnPropertyChanged(nameof(WatermarkText));
        OnPropertyChanged(nameof(WatermarkFontSize));
        OnPropertyChanged(nameof(WatermarkPosition));
        OnPropertyChanged(nameof(WatermarkTextColorHex));
        OnPropertyChanged(nameof(WatermarkOpacity));
        OnPropertyChanged(nameof(EnableLinkFormat));
        OnPropertyChanged(nameof(SelectedLinkFormat));
        OnPropertyChanged(nameof(SelectedUploadLinkFormatOption));
        OnPropertyChanged(nameof(IsCustomUploadLinkFormatPanelVisible));
        OnPropertyChanged(nameof(CustomLinkFormatTemplate));
        OnPropertyChanged(nameof(SelectedLinkRuleName));
        OnPropertyChanged(nameof(AutoCopyLinkAfterUpload));
        OnPropertyChanged(nameof(PostUploadPublishMode));
        OnPropertyChanged(nameof(JsDelivrVersionReferenceMode));
        OnPropertyChanged(nameof(JsDelivrTagName));
        NotifyJsDelivrConfigurationBindingsChanged();
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(LoginDisplayName));
    }

    private void RaiseLoginStateChanged()
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(LoginDisplayName));
        LogoutCommand.RaiseCanExecuteChanged();
        RefreshManagedImagesCommand.RaiseCanExecuteChanged();
        SelectAllManagedImagesCommand.RaiseCanExecuteChanged();
        ClearManagedImageSelectionCommand.RaiseCanExecuteChanged();
        DeleteSelectedImagesCommand.RaiseCanExecuteChanged();
        UploadImagesCommand.RaiseCanExecuteChanged();
        DeployGitHubPagesCommand.RaiseCanExecuteChanged();
    }

    private void RaiseUploadListCommandStates()
    {
        UploadImagesCommand.RaiseCanExecuteChanged();
        ClearUploadListCommand.RaiseCanExecuteChanged();
        SelectAllUploadItemsCommand.RaiseCanExecuteChanged();
        ClearUploadItemSelectionCommand.RaiseCanExecuteChanged();
        RemoveSelectedUploadCommand.RaiseCanExecuteChanged();
    }

    private void RaiseManagedImageCommandStates()
    {
        RefreshManagedImagesCommand.RaiseCanExecuteChanged();
        SelectAllManagedImagesCommand.RaiseCanExecuteChanged();
        ClearManagedImageSelectionCommand.RaiseCanExecuteChanged();
        DeleteSelectedImagesCommand.RaiseCanExecuteChanged();
        CopySelectedImageLinksCommand.RaiseCanExecuteChanged();
    }

    private void RaiseAllCommandStates()
    {
        RaiseLoginStateChanged();
        AddImagesCommand.RaiseCanExecuteChanged();
        PasteImagesFromClipboardCommand.RaiseCanExecuteChanged();
        ImportImageFromUrlCommand.RaiseCanExecuteChanged();
        SelectUploadLinkFormatCommand.RaiseCanExecuteChanged();
        RaiseUploadListCommandStates();
        CancelOperationCommand.RaiseCanExecuteChanged();
        SaveSettingsCommand.RaiseCanExecuteChanged();
        RunToolboxCompressCommand.RaiseCanExecuteChanged();
        RunToolboxBase64Command.RaiseCanExecuteChanged();
        RunToolboxWatermarkCommand.RaiseCanExecuteChanged();
        RaiseManagedImageCommandStates();
    }

    public void Dispose()
    {
        StopUploadProgressSmoother();
        PersistSettingsOnExit();
        _settingsPersistDebounceCancellationTokenSource?.Cancel();
        _settingsPersistDebounceCancellationTokenSource?.Dispose();
        _managedImageThumbnailLoadCancellationTokenSource?.Cancel();
        _managedImageThumbnailLoadCancellationTokenSource?.Dispose();
        _operationCancellationTokenSource?.Dispose();
        _managedImageThumbnailLoadSemaphore.Dispose();
        _remoteThumbnailLoader.Dispose();
        _gitHubApiClient.Dispose();
    }

    /// <summary>
    /// 在网络请求耗时较长时缓慢推进进度条，避免长时间停在同一百分比。
    /// </summary>
    private sealed class UploadProgressSmoother : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly Action<int> _onPercentChanged;
        private int _currentPercent;
        private readonly int _maxPercent;

        public UploadProgressSmoother(int fromPercent, int toPercent, Action<int> onPercentChanged)
        {
            _currentPercent = fromPercent;
            _maxPercent = toPercent;
            _onPercentChanged = onPercentChanged;
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _timer.Tick += OnTimerTick;
            _timer.Start();
            _onPercentChanged(_currentPercent);
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_currentPercent >= _maxPercent)
            {
                return;
            }

            _currentPercent++;
            _onPercentChanged(_currentPercent);
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= OnTimerTick;
        }
    }
}
