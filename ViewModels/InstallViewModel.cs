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
    private bool IsEnglish => !string.Equals(SelectedLanguage, "Español", StringComparison.OrdinalIgnoreCase);

    public string WindowTitle => IsEnglish ? "Install learn-Assistant" : "Instalar learn-Assistant";

    public string StepTitle => CurrentStep switch
    {
        0 => IsEnglish ? "Welcome" : "Bienvenido",
        1 => IsEnglish ? "Installation type" : "Tipo de instalación",
        2 => IsEnglish ? "Ready to install" : "Listo para instalar",
        3 => IsEnglish ? "Installing…" : "Instalando…",
        _ => IsEnglish ? "Done!" : "¡Listo!",
    };

    public string StepDescription => CurrentStep switch
    {
        0 => IsEnglish
            ? "Learn-Assistant can be installed on this computer with a menu entry, so you can launch it like any other app."
            : "Learn-Assistant puede instalarse en este equipo con una entrada en el menú para iniciarlo como cualquier otra aplicación.",
        1 => IsEnglish
            ? "Choose where the app should be installed. The user install does not require administrator rights."
            : "Elige dónde quieres instalar la aplicación. La instalación para el usuario no requiere permisos de administrador.",
        2 => IsEnglish
            ? "Everything is ready. Click Install to copy the app and create the menu entry."
            : "Todo está listo. Haz clic en Instalar para copiar la aplicación y crear la entrada del menú.",
        3 => IsEnglish
            ? "Copying files and creating the menu entry…"
            : "Copiando archivos y creando la entrada del menú…",
        _ => IsEnglish
            ? "Learn-Assistant has been installed. You can now log in and start learning."
            : "Learn-Assistant se instaló correctamente. Ahora puedes iniciar sesión y comenzar a aprender.",
    };

    public string WelcomeSectionTitle => IsEnglish ? "What will happen" : "Qué ocurrirá";
    public string WelcomeSectionDescription => IsEnglish
        ? "The app binary will be copied to a known location and a menu entry will be created, so you can start it from your application launcher."
        : "El ejecutable se copiará en una ubicación conocida y se creará una entrada en el menú para que puedas iniciar la aplicación desde el lanzador.";
    public string LanguageLabel => IsEnglish ? "Application language" : "Idioma de la aplicación";
    public string UserInstallLabel => IsEnglish ? "User installation (recommended)" : "Instalación para el usuario (recomendada)";
    public string UserInstallDescription => IsEnglish
        ? "Installs under your own home directory. No administrator password is needed."
        : "Se instala en tu directorio personal. No se necesita contraseña de administrador.";
    public string SystemInstallLabel => IsEnglish ? "System installation (for all users)" : "Instalación del sistema (para todos los usuarios)";
    public string SystemInstallDescription => IsEnglish
        ? "Installs to a system location and requires administrator access (pkexec on Linux / UAC on Windows)."
        : "Se instala en una ubicación del sistema y requiere permisos de administrador (pkexec en Linux / UAC en Windows).";
    public string BinaryLabel => IsEnglish ? "Application binary" : "Ejecutable de la aplicación";
    public string MenuEntryLabel => IsEnglish ? "Menu entry" : "Entrada del menú";
    public string AdminWarning => IsEnglish
        ? "The administrator dialog will open during the copy step."
        : "El diálogo de administrador se abrirá durante el proceso de copia.";
    public string InstallingMessage => IsEnglish
        ? "Please wait; the installation may ask for permissions…"
        : "Espera; la instalación puede solicitar permisos…";
    public string CompleteMessage => IsEnglish
        ? "The installation finished successfully. Click Done to open the login screen and get started."
        : "La instalación terminó correctamente. Haz clic en Listo para abrir la pantalla de inicio de sesión.";
    public string BackButtonText => IsEnglish ? "Back" : "Atrás";

    public bool IsFirstStep => CurrentStep == 0;
    public bool IsScopeStep => CurrentStep == 1;
    public bool IsSummaryStep => CurrentStep == 2;
    public bool IsInstallingStep => CurrentStep == 3;
    public bool IsCompleteStep => CurrentStep == 4;
    public bool IsNavVisible => CurrentStep is >= 0 and <= 4;
    public bool IsBackVisible => CurrentStep is 1 or 2;
    public string NextButtonText => CurrentStep switch
    {
        2 => IsEnglish ? "Install" : "Instalar",
        4 => IsEnglish ? "Done" : "Listo",
        _ => IsEnglish ? "Next" : "Siguiente",
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
        NotifyLocalizedProperties();
        OnPropertyChanged(nameof(BinaryPath));
        OnPropertyChanged(nameof(MenuEntryPath));
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        LanguageService.Set(value);
        NotifyLocalizedProperties();
    }

    private void NotifyLocalizedProperties()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(StepDescription));
        OnPropertyChanged(nameof(WelcomeSectionTitle));
        OnPropertyChanged(nameof(WelcomeSectionDescription));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(UserInstallLabel));
        OnPropertyChanged(nameof(UserInstallDescription));
        OnPropertyChanged(nameof(SystemInstallLabel));
        OnPropertyChanged(nameof(SystemInstallDescription));
        OnPropertyChanged(nameof(BinaryLabel));
        OnPropertyChanged(nameof(MenuEntryLabel));
        OnPropertyChanged(nameof(AdminWarning));
        OnPropertyChanged(nameof(InstallingMessage));
        OnPropertyChanged(nameof(CompleteMessage));
        OnPropertyChanged(nameof(BackButtonText));
        OnPropertyChanged(nameof(NextButtonText));
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

        if (CurrentStep == 0)
            LanguageService.Set(SelectedLanguage);

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
            ErrorMessage = IsEnglish
                ? "Installation failed. You can try again."
                : "La instalación falló. Puedes intentarlo de nuevo.";
            CurrentStep = 2;
        }
    }

    private void Finish()
    {
        LanguageService.Set(SelectedLanguage);
        InstallationFinished?.Invoke();
    }
}