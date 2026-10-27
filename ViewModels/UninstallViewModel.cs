using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class UninstallViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool RemoveUserData { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsUninstalling { get; set; }

    [ObservableProperty]
    public partial bool UninstallCompleted { get; set; }

    public event Action? UninstallSucceeded;
    public event Action? UninstallFailed;

    [RelayCommand]
    private async Task UninstallAsync()
    {
        IsUninstalling = true;
        StatusMessage = "Desinstalando la aplicación...";

        try
        {
            var ok = UninstallService.Uninstall(removeUserData: RemoveUserData);

            if (ok)
            {
                StatusMessage = "Desinstalación completada correctamente.";
                UninstallCompleted = true;
                UninstallSucceeded?.Invoke();
            }
            else
            {
                StatusMessage = "La desinstalación se completó con advertencias. Revisa startup-error.log para más detalles.";
                UninstallCompleted = true;
                UninstallSucceeded?.Invoke();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error durante la desinstalación: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[UninstallViewModel] {ex}");
            UninstallFailed?.Invoke();
        }
        finally
        {
            IsUninstalling = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        // El code-behind cerrará el diálogo
    }
}
