using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class InstallViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial int CurrentStep { get; set; }

    [ObservableProperty]
    public partial bool SystemScopeSelected { get; set; }

    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public event Action? InstallationFinished;
    public event Action? InstallationSkipped;

    private InstallScope Scope => SystemScopeSelected ? InstallScope.System : InstallScope.User;
    private InstallPaths InstallPaths => InstallationService.GetInstallPaths(Scope);

    public string StepTitle => CurrentStep switch
    {
        0 => "Welcome",
        1 => "Install scope",
        2 => "Ready to install",
        3 => "Installing…",
        _ => "Done!",
    };

    public string StepDescription => CurrentStep switch
    {
        0 => "Learn-Assistant can be installed on this computer with a menu entry, so you can launch it like any other app.",
        1 => "Choose where the app should be installed. The user install does not require administrator rights.",
        2 => "Everything is ready. Click Install to copy the app and create the menu entry.",
        3 => "Copying files and creating the menu entry…",
        _ => "Learn-Assistant has been installed. You can now log in and start learning.",
    };

    public bool IsFirstStep => CurrentStep == 0;
    public bool IsScopeStep => CurrentStep == 1;
    public bool IsSummaryStep => CurrentStep == 2;
    public bool IsInstallingStep => CurrentStep == 3;
    public bool IsCompleteStep => CurrentStep == 4;
    public bool IsNavVisible => CurrentStep is >= 0 and <= 2;
    public bool IsBackVisible => CurrentStep is 1 or 2;
    public string NextButtonText => CurrentStep switch
    {
        2 => "Install",
        4 => "Done",
        _ => "Next",
    };

    public string BinaryPath => InstallPaths.Binary;
    public string MenuEntryPath => InstallPaths.MenuEntry;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(StepDescription));
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsScopeStep));
        OnPropertyChanged(nameof(IsSummaryStep));
        OnPropertyChanged(nameof(IsInstallingStep));
        OnPropertyChanged(nameof(IsCompleteStep));
        OnPropertyChanged(nameof(IsNavVisible));
        OnPropertyChanged(nameof(IsBackVisible));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(BinaryPath));
        OnPropertyChanged(nameof(MenuEntryPath));
    }

    partial void OnSystemScopeSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(BinaryPath));
        OnPropertyChanged(nameof(MenuEntryPath));
    }

    [RelayCommand]
    private void Next()
    {
        if (CurrentStep == 2)
        {
            _ = InstallAsync();
            return;
        }

        if (CurrentStep == 4)
        {
            Finish();
            return;
        }

        CurrentStep++;
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep == 1 || CurrentStep == 2)
            CurrentStep--;
    }

    [RelayCommand]
    private void Skip()
    {
        InstallationSkipped?.Invoke();
    }

    private async Task InstallAsync()
    {
        if (IsWorking)
            return;

        IsWorking = true;
        ErrorMessage = null;
        CurrentStep = 3;

        var ok = await Task.Run(() => InstallationService.Install(Scope));

        IsWorking = false;

        if (ok)
        {
            CurrentStep = 4;
        }
        else
        {
            ErrorMessage = "Installation failed. You can try again or skip the setup.";
            CurrentStep = 2;
        }
    }

    private void Finish()
    {
        InstallationFinished?.Invoke();
    }
}