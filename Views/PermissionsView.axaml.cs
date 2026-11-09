using System;
using Avalonia.Controls;
using learn_Assist.Models;
using learn_Assist.ViewModels;

namespace learn_Assist.Views;

public partial class PermissionsView : Window
{
    private PermissionsViewModel? _previousVm;

    public PermissionsView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_previousVm is not null)
        {
            _previousVm.Saved -= OnSaved;
            _previousVm.Cancelled -= OnCancelled;
        }

        if (DataContext is PermissionsViewModel vm)
        {
            vm.Saved += OnSaved;
            vm.Cancelled += OnCancelled;
            _previousVm = vm;
        }
        else
        {
            _previousVm = null;
        }
    }

    private void OnSaved(AiPermissions permissions)
    {
        Close(permissions);
    }

    private void OnCancelled()
    {
        Close();
    }
}
