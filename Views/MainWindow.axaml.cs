using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using learn_Assist.Models;
using learn_Assist.Services;
using learn_Assist.ViewModels;

namespace learn_Assist.Views;

public partial class MainWindow : Window
{
    private MainViewModel? _previousVm;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_previousVm is not null)
        {
            _previousVm.Chat.ScrollToBottomRequested -= OnScrollToBottom;
            _previousVm.DocumentList.ImportDialogRequested -= OnImportDialog;
            _previousVm.ConfigureAiRequested -= OnConfigureAi;
        }

        if (DataContext is MainViewModel vm)
        {
            vm.Chat.ScrollToBottomRequested += OnScrollToBottom;
            vm.DocumentList.ImportDialogRequested += OnImportDialog;
            vm.ConfigureAiRequested += OnConfigureAi;
            _previousVm = vm;
        }
        else
        {
            _previousVm = null;
        }
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
        _ = ShowApiConfigDialogAsync();
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
