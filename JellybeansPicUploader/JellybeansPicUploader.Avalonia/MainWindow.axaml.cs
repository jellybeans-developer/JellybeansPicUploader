using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using JellybeansPicUploader.Services;
using JellybeansPicUploader.ViewModels;
using JellybeansPicUploader.Views.Pages;

namespace JellybeansPicUploader;

public partial class MainWindow : Window
{
    private const int NavigationPageCount = 5;

    private readonly ShellViewModel _viewModel = new();
    private readonly UserControl[] _pageInstances = new UserControl[NavigationPageCount];
    private QuickUploadDockCoordinator? _quickUploadDockCoordinator;
    private bool _isSyncingNavigation;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        PageHost.DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _quickUploadDockCoordinator = new QuickUploadDockCoordinator(this, _viewModel);

        AddHandler(DragDrop.DragOverEvent, MainWindow_OnDragOver);
        AddHandler(DragDrop.DropEvent, MainWindow_OnDrop);
        AddHandler(InputElement.KeyDownEvent, MainWindow_OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            _quickUploadDockCoordinator?.NotifyMainWindowStateChanged();
        }
    }

    private async void OnLoaded(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            await _viewModel.InitializeAsync();
            _viewModel.InitializeApplicationThemeFromCurrent();
            NavigationList.SelectedIndex = _viewModel.SelectedTabIndex;
            NavigateToTabIndex(_viewModel.SelectedTabIndex);
        }
        catch (Exception exception)
        {
            _viewModel.StatusText = $"初始化失败：{exception.Message}";
        }
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        _quickUploadDockCoordinator?.Dispose();
        _quickUploadDockCoordinator = null;
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _viewModel.PersistSettingsOnExit();
        _viewModel.Dispose();
    }

    private void ViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ShellViewModel.SelectedTabIndex))
        {
            NavigateToTabIndex(_viewModel.SelectedTabIndex);
        }
    }

    private void NavigationList_OnSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (_isSyncingNavigation)
        {
            return;
        }

        var selectedIndex = NavigationList.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= NavigationPageCount)
        {
            return;
        }

        if (_viewModel.SelectedTabIndex == selectedIndex)
        {
            // 用户重复点击当前项时仍刷新内容区（避免仅依赖 VM 属性变更）
            NavigateToTabIndex(selectedIndex);
            return;
        }

        _viewModel.SelectedTabIndex = selectedIndex;
    }

    private void NavigateToTabIndex(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= NavigationPageCount)
        {
            return;
        }

        _isSyncingNavigation = true;
        try
        {
            if (NavigationList.SelectedIndex != tabIndex)
            {
                NavigationList.SelectedIndex = tabIndex;
            }

            var page = _pageInstances[tabIndex] ??= CreateNavigationPage(tabIndex);
            PageHost.Content = page;
        }
        finally
        {
            _isSyncingNavigation = false;
        }
    }

    private UserControl CreateNavigationPage(int tabIndex) => tabIndex switch
    {
        0 => new LoginConfigPage(_viewModel),
        1 => new UploadPage(_viewModel),
        2 => new ManagementPage(_viewModel),
        3 => new SettingsPage(_viewModel),
        4 => new ToolboxPage(_viewModel),
        _ => throw new ArgumentOutOfRangeException(nameof(tabIndex), tabIndex, null)
    };

    private void MainWindow_OnDragOver(object? sender, DragEventArgs eventArgs)
    {
        eventArgs.DragEffects = UploadInputHelper.CanAcceptDragEvent(eventArgs)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        eventArgs.Handled = true;
    }

    private void MainWindow_OnDrop(object? sender, DragEventArgs eventArgs)
    {
        if (!UploadInputHelper.CanAcceptDragEvent(eventArgs))
        {
            return;
        }

        _viewModel.SelectedTabIndex = 1;
        _viewModel.ImportDroppedImageFiles(UploadInputHelper.CollectImagePathsFromDragEvent(eventArgs));
        eventArgs.Handled = true;
    }

    private void MainWindow_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.V || eventArgs.KeyModifiers != KeyModifiers.Control)
        {
            return;
        }

        _viewModel.SelectedTabIndex = 1;
        if (_viewModel.TryImportClipboardImages())
        {
            eventArgs.Handled = true;
        }
    }
}
