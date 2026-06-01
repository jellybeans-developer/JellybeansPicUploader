using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using JellybeansPicUploader.Services;
using JellybeansPicUploader.ViewModels;
using JellybeansPicUploader.Views.Pages;

namespace JellybeansPicUploader;

public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private static readonly Type[] NavigationPageTypes =
    [
        typeof(LoginConfigPage),
        typeof(UploadPage),
        typeof(ManagementPage),
        typeof(SettingsPage),
        typeof(ToolboxPage)
    ];

    private readonly ShellViewModel _viewModel = new();
    private QuickUploadDockCoordinator? _quickUploadDockCoordinator;
    private bool _isSyncingNavigation;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Closed += OnClosed;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _quickUploadDockCoordinator = new QuickUploadDockCoordinator(this, _viewModel);
    }

    protected override void OnStateChanged(EventArgs eventArgs)
    {
        base.OnStateChanged(eventArgs);
        _quickUploadDockCoordinator?.NotifyMainWindowStateChanged();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.InitializeAsync();
            NavigateToTabIndex(_viewModel.SelectedTabIndex);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"初始化失败：{ex.Message}", "JellybeansPicUploader", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _quickUploadDockCoordinator?.Dispose();
        _quickUploadDockCoordinator = null;
        _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        _viewModel.PersistSettingsOnExit();
        _viewModel.Dispose();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.SelectedTabIndex))
        {
            NavigateToTabIndex(_viewModel.SelectedTabIndex);
        }
    }

    private void RootNavigation_OnSelectionChanged(Wpf.Ui.Controls.NavigationView sender, RoutedEventArgs args)
    {
        if (_isSyncingNavigation)
        {
            return;
        }

        if (sender.SelectedItem is Wpf.Ui.Controls.NavigationViewItem { TargetPageType: { } pageType })
        {
            var index = Array.IndexOf(NavigationPageTypes, pageType);
            if (index >= 0)
            {
                _viewModel.SelectedTabIndex = index;
            }
        }
    }

    private void NavigateToTabIndex(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= NavigationPageTypes.Length)
        {
            return;
        }

        _isSyncingNavigation = true;
        try
        {
            RootNavigation.Navigate(NavigationPageTypes[tabIndex]);
        }
        finally
        {
            _isSyncingNavigation = false;
        }
    }

    private void MainWindow_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = UploadInputHelper.CanAcceptDataObject(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void MainWindow_OnDrop(object sender, DragEventArgs e)
    {
        if (!UploadInputHelper.CanAcceptDataObject(e.Data))
        {
            return;
        }

        _viewModel.SelectedTabIndex = 1;
        _viewModel.ImportDroppedImageFiles(UploadInputHelper.CollectImagePathsFromDataObject(e.Data));
        e.Handled = true;
    }

    private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        _viewModel.SelectedTabIndex = 1;
        if (_viewModel.TryImportClipboardImages())
        {
            e.Handled = true;
        }
    }
}
