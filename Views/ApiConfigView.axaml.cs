using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using learn_Assist.Models;
using learn_Assist.ViewModels;

namespace learn_Assist.Views;

public partial class ApiConfigView : Window
{
    private ApiConfigViewModel? _previousVm;

    public ApiConfig? Result { get; private set; }

    public ApiConfigView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_previousVm is not null)
        {
            _previousVm.ConfigSaved -= OnConfigSaved;
            _previousVm.ConfigSkipped -= OnConfigSkipped;
            _previousVm.BrowseDirectoryRequested -= OnBrowseDirectoryRequested;
        }

        if (DataContext is ApiConfigViewModel vm)
        {
            vm.ConfigSaved += OnConfigSaved;
            vm.ConfigSkipped += OnConfigSkipped;
            vm.BrowseDirectoryRequested += OnBrowseDirectoryRequested;
            _previousVm = vm;
        }
        else
        {
            _previousVm = null;
        }
    }

    private void OnConfigSaved(ApiConfig config)
    {
        Result = config;
        Close(config);
    }

    private void OnConfigSkipped()
    {
        Close();
    }

    private async void OnBrowseDirectoryRequested()
    {
        if (DataContext is not ApiConfigViewModel vm)
            return;

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Sessions Directory",
            AllowMultiple = false,
        });

        if (folders.Count > 0)
        {
            vm.SessionsDirectory = folders[0].Path.AbsolutePath;
        }
    }
}
