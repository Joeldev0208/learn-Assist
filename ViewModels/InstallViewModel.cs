using System;
using System.Collections.ObjectModel;
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

    public ObservableCollection<string> Languages { get; } =
        ["Español", "English"];

    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = "English";

    private InstallScope Scope => SystemScopeSelected ? InstallScope.System : InstallScope.User;
    private InstallPaths InstallPaths => InstallationService.GetInstallPaths(Scope);

    public string StepTitle => CurrentStep switch
    {
        0 => "Bienvenido",
        1 => "Tipo de instalación",
        2 => "Listo para instalar",
        3 => "Instalando…",
        _ => "¡Listo!",
    };

    public string StepDescription => CurrentStep switch
    {
        0 => "Learn-Assistant puede instalarse en este equipo con una entrada en el menú para iniciarlo como cualquier otra aplicación.",
        1 => "Elige dónde quieres instalar la aplicación. La instalación para el usuario no requiere permisos de administrador.",
        2 => "Todo está listo. Haz clic en Instalar para copiar la aplicación y crear la entrada del menú.",
        3 => "Copiando archivos y creando la entrada del menú…",
        _ => "Learn-Assistant se instaló correctamente. Ahora puedes iniciar sesión y comenzar a aprender.",
    };

    public bool IsFirstStep => CurrentStep == 0;
    public bool IsScopeStep => CurrentStep == 1;
    public bool IsSummaryStep => CurrentStep == 2;
    public bool IsInstallingStep => CurrentStep == 3;
    public bool IsCompleteStep => CurrentStep == 4;
    public bool IsNavVisible => CurrentStep is >= 0 and <= 4;
    public bool IsBackVisible => CurrentStep is 1 or 2;
    public string NextButtonText => CurrentStep switch
    {
        2 => "Instalar",
        4 => "Listo",
        _ => "Siguiente",
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
            ErrorMessage = "La instalación falló. Puedes intentarlo de nuevo.";
            CurrentStep = 2;
        }
    }

    private void Finish()
    {
        LanguageService.Set(SelectedLanguage);
        InstallationFinished?.Invoke();
    }
}