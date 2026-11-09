using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;

namespace learn_Assist.ViewModels;

/// <summary>
/// Edita una copia de <see cref="AiPermissions"/>. Solo al confirmar se
/// emite <see cref="Saved"/>, de modo que «Cancelar» no deja cambios a medias.
/// </summary>
public partial class PermissionsViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial bool AllowResources { get; set; } = true;

    [ObservableProperty]
    public partial bool AllowWebResources { get; set; } = true;

    [ObservableProperty]
    public partial bool AllowLocalFiles { get; set; } = true;

    [ObservableProperty]
    public partial bool AllowAgentTools { get; set; }

    public bool CanEditResourceTypes => AllowResources;

    public event Action<AiPermissions>? Saved;
    public event Action? Cancelled;

    public PermissionsViewModel()
        : this(AiPermissions.Default)
    {
    }

    public PermissionsViewModel(AiPermissions permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        AllowResources = permissions.AllowResources;
        AllowWebResources = permissions.AllowWebResources;
        AllowLocalFiles = permissions.AllowLocalFiles;
        AllowAgentTools = permissions.AllowAgentTools;
    }

    partial void OnAllowResourcesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditResourceTypes));
    }

    [RelayCommand]
    private void Save()
    {
        Saved?.Invoke(new AiPermissions
        {
            AllowResources = AllowResources,
            AllowWebResources = AllowWebResources,
            AllowLocalFiles = AllowLocalFiles,
            AllowAgentTools = AllowAgentTools,
        });
    }

    [RelayCommand]
    private void Cancel()
    {
        Cancelled?.Invoke();
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        var defaults = AiPermissions.Default;
        AllowResources = defaults.AllowResources;
        AllowWebResources = defaults.AllowWebResources;
        AllowLocalFiles = defaults.AllowLocalFiles;
        AllowAgentTools = defaults.AllowAgentTools;
    }
}
