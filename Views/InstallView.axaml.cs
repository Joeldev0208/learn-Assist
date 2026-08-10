using System;
using Avalonia.Controls;
using learn_Assist.ViewModels;

namespace learn_Assist.Views;

public partial class InstallView : Window
{
    private InstallViewModel? _previousVm;

    public InstallView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_previousVm is not null)
        {
            _previousVm.InstallationFinished -= OnInstallationFinished;
            _previousVm.InstallationSkipped -= OnInstallationSkipped;
        }

        if (DataContext is InstallViewModel vm)
        {
            vm.InstallationFinished += OnInstallationFinished;
            vm.InstallationSkipped += OnInstallationSkipped;
            _previousVm = vm;
        }
        else
        {
            _previousVm = null;
        }
    }

    private void OnInstallationFinished()
    {
        Close();
    }

    private void OnInstallationSkipped()
    {
        Close();
    }
}