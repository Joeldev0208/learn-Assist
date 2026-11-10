using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

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

    private bool IsEnglish => LanguageService.IsEnglish;

    public string WindowTitle => IsEnglish ? "AI Permissions" : "Permisos de la IA";
    public string IntroText => IsEnglish ? "Control what sources the AI can use and what the local agent can do. Changes apply to the next message." : "Controla qué fuentes puede usar la IA y qué puede hacer el agente local. Los cambios se aplican al siguiente mensaje.";
    public string SourcesSectionTitle => IsEnglish ? "Consultation Sources" : "Fuentes de consulta";
    public string AllowResourcesContent => IsEnglish ? "Use marked resources in chat" : "Usar los recursos marcados en el chat";
    public string AllowResourcesDescription => IsEnglish ? "If disabled, the AI responds only with its general knowledge and the semester program." : "Si lo desactivas, la IA responde solo con su conocimiento general y el programa del semestre.";
    public string AllowWebResourcesContent => IsEnglish ? "Include web resources (URL)" : "Incluir recursos web (URL)";
    public string AllowWebResourcesDescription => IsEnglish ? "Pages and articles added from a web address." : "Páginas y artículos agregados desde una dirección web.";
    public string AllowLocalFilesContent => IsEnglish ? "Include local files" : "Incluir archivos locales";
    public string AllowLocalFilesDescription => IsEnglish ? "PDF, DOCX, presentations, images and videos imported from your computer." : "PDF, DOCX, presentaciones, imágenes y videos importados desde tu equipo.";
    public string AgentSectionTitle => IsEnglish ? "Local Agent" : "Agente local";
    public string AllowAgentToolsContent => IsEnglish ? "Allow agent tools" : "Permitir herramientas del agente";
    public string AllowAgentToolsDescription => IsEnglish ? "With OpenCode, allows executing commands, editing files and accessing the network. Disabled by default: the agent runs with your account permissions." : "Con OpenCode, permite ejecutar comandos, editar archivos y acceder a la red. Desactivado por defecto: el agente corre con los permisos de tu cuenta.";
    public string PermissionsNote => IsEnglish ? "Permissions are saved on this computer and are not sent to any server." : "Los permisos se guardan en este equipo y no se envían a ningún servidor.";
    public string ResetButtonContent => IsEnglish ? "Reset" : "Restablecer";
    public string CancelButtonContent => IsEnglish ? "Cancel" : "Cancelar";
    public string SaveButtonContent => IsEnglish ? "Save" : "Guardar";

    public event Action<AiPermissions>? Saved;
    public event Action? Cancelled;

    public PermissionsViewModel()
        : this(AiPermissions.Default)
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
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

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(IntroText));
        OnPropertyChanged(nameof(SourcesSectionTitle));
        OnPropertyChanged(nameof(AllowResourcesContent));
        OnPropertyChanged(nameof(AllowResourcesDescription));
        OnPropertyChanged(nameof(AllowWebResourcesContent));
        OnPropertyChanged(nameof(AllowWebResourcesDescription));
        OnPropertyChanged(nameof(AllowLocalFilesContent));
        OnPropertyChanged(nameof(AllowLocalFilesDescription));
        OnPropertyChanged(nameof(AgentSectionTitle));
        OnPropertyChanged(nameof(AllowAgentToolsContent));
        OnPropertyChanged(nameof(AllowAgentToolsDescription));
        OnPropertyChanged(nameof(PermissionsNote));
        OnPropertyChanged(nameof(ResetButtonContent));
        OnPropertyChanged(nameof(CancelButtonContent));
        OnPropertyChanged(nameof(SaveButtonContent));
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
