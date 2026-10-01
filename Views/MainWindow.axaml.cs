using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using learn_Assist.Models;
using learn_Assist.Services;
using learn_Assist.ViewModels;

namespace learn_Assist.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _previousVm;
    private Grid? _workspaceGrid;
    private int? _lastBreakpoint;

    public MainWindow()
    {
        InitializeComponent();
        _workspaceGrid = this.FindControl<Grid>("WorkspaceGrid");
        SizeChanged += OnWindowSizeChanged;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_previousVm is not null)
        {
            _previousVm.Chat.ScrollToBottomRequested -= OnScrollToBottom;
            _previousVm.DocumentList.ImportDialogRequested -= OnImportDialog;
            _previousVm.ConfigureAiRequested -= OnConfigureAi;
            _previousVm.RestartRequested -= OnRestartRequested;
            _previousVm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (DataContext is MainViewModel vm)
        {
            vm.Chat.ScrollToBottomRequested += OnScrollToBottom;
            vm.DocumentList.ImportDialogRequested += OnImportDialog;
            vm.ConfigureAiRequested += OnConfigureAi;
            vm.RestartRequested += OnRestartRequested;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            _previousVm = vm;
            UpdateResponsiveLayout(vm);
        }
        else
        {
            _previousVm = null;
            _lastBreakpoint = null;
        }
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            UpdateResponsiveLayout(vm);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsSessionsVisible) or nameof(MainViewModel.IsResourcesVisible)
            && sender is MainViewModel vm)
            ApplyGridColumns(vm);
    }

    private void UpdateResponsiveLayout(MainViewModel vm)
    {
        if (_workspaceGrid is null || _workspaceGrid.ColumnDefinitions.Count < 5)
            return;

        var isCompact = Bounds.Width < 900;
        var isMedium = Bounds.Width < 1200;
        var breakpoint = isCompact ? 0 : isMedium ? 1 : 2;
        vm.IsCompactLayout = isCompact;

        if (_lastBreakpoint != breakpoint)
        {
            if (isCompact)
            {
                vm.IsSessionsVisible = false;
                vm.IsResourcesVisible = false;
            }
            else if (isMedium)
            {
                vm.IsSessionsVisible = true;
                vm.IsResourcesVisible = false;
            }
            else
            {
                vm.IsSessionsVisible = true;
                vm.IsResourcesVisible = true;
            }

            _lastBreakpoint = breakpoint;
        }

        ApplyGridColumns(vm);
    }

    private void ApplyGridColumns(MainViewModel vm)
    {
        if (_workspaceGrid is null || _workspaceGrid.ColumnDefinitions.Count < 5)
            return;

        _workspaceGrid.ColumnDefinitions[0].Width =
            vm.IsSessionsVisible ? new GridLength(260) : new GridLength(0);
        _workspaceGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
        _workspaceGrid.ColumnDefinitions[4].Width =
            vm.IsResourcesVisible ? new GridLength(220) : new GridLength(0);
        _workspaceGrid.ColumnDefinitions[1].Width =
            vm.IsSessionsVisible ? new GridLength(1) : new GridLength(0);
        _workspaceGrid.ColumnDefinitions[3].Width =
            vm.IsResourcesVisible ? new GridLength(1) : new GridLength(0);
    }

    private void OnScrollToBottom()
    {
        MessagesScroll?.ScrollToEnd();
    }

    private void OnImportDialog()
    {
        _ = ShowImportDialogAsync();
    }

    private void OnConfigureAi()
    {
        Dispatcher.UIThread.Post(() => _ = ShowApiConfigDialogAsync());
    }

    private void OnRestartRequested()
    {
        // The apply-update.cmd script waits for this window to close before
        // swapping the binary, then relaunches the new version.
        Close();
    }

    private async Task ShowImportDialogAsync()
    {
        if (DataContext is not MainViewModel vm)
            return;

        var importVm = new ImportDocumentViewModel();
        var dialog = new ImportDocumentView
        {
            DataContext = importVm,
        };

        var result = await dialog.ShowDialog<UserDocument?>(this);
        if (result is not null)
            vm.DocumentList.AddDocument(result!);
    }

    private async Task ShowApiConfigDialogAsync()
    {
        if (DataContext is not MainViewModel vm)
            return;

        var existing = ConfigEncryption.LoadConfig();
        var configVm = existing is not null ? new ApiConfigViewModel(existing) : new ApiConfigViewModel();
        var dialog = new ApiConfigView
        {
            DataContext = configVm,
        };

        await dialog.ShowDialog<ApiConfig?>(this);

        if (dialog.Result is not null)
            vm.ApplyConfig(dialog.Result);
    }
}
