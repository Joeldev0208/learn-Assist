using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using learn_Assist.Models;
using learn_Assist.ViewModels;

namespace learn_Assist.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is MainViewModel vm)
        {
            vm.Chat.ScrollToBottomRequested += () =>
            {
                MessagesScroll?.ScrollToEnd();
            };

            vm.DocumentList.ImportDialogRequested += () =>
            {
                _ = ShowImportDialogAsync();
            };
        }
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
}
