using System;
using Avalonia.Controls;
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
        }
    }
}
