using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class ApiConfigViewModel : ViewModelBase
{
    private readonly Dictionary<string, string> _savedModels = new(StringComparer.OrdinalIgnoreCase);
    private string _activeProvider = nameof(AiProvider.OpenAI);

    [ObservableProperty]
    public partial string SelectedProvider { get; set; } = AiProvider.OpenAI.ToString();

    [ObservableProperty]
    public partial string BaseUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ApiKey { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Model { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SessionsDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OpenCodeExecutablePath { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OpenCodeWorkspaceDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    private bool IsEnglish => LanguageService.IsEnglish;

    public string WindowTitle => IsEnglish ? "Configure AI Provider" : "Configurar proveedor de IA";
    public string IntroText => IsEnglish ? "Connect your AI provider to start learning." : "Conecta tu API de IA para comenzar a aprender.";
    public string ProviderLabel => IsEnglish ? "Provider" : "Proveedor";
    public string BaseUrlLabel => IsEnglish ? "Base URL" : "URL base";
    public string BaseUrlPlaceholder => IsEnglish ? "https://api.openai.com" : "https://api.openai.com";
    public string OpenCodeInfo => IsEnglish ? "OpenCode runs locally via the terminal. Install OpenCode separately and authenticate with opencode auth login; Learn-Assist does not store an API key." : "OpenCode se ejecuta localmente mediante la terminal. Instala OpenCode por separado y autentícate con opencode auth login; Learn-Assist no guarda una clave de API.";
    public string ApiKeyLabel => IsEnglish ? "API Key" : "Clave de API";
    public string ApiKeyPlaceholder => IsEnglish ? "sk-..." : "sk-...";
    public string OpenCodeModelLabel => IsEnglish ? "OpenCode Model" : "Modelo de OpenCode";
    public string ModelLabel => IsEnglish ? "Model" : "Modelo";
    public string ModelPlaceholder => IsEnglish ? "gpt-4o-mini" : "gpt-4o-mini";
    public string OpenCodeExecutableLabel => IsEnglish ? "OpenCode Executable (optional)" : "Ejecutable de OpenCode (opcional)";
    public string OpenCodeExecutablePlaceholder => IsEnglish ? "opencode" : "opencode";
    public string OpenCodeWorkspaceLabel => IsEnglish ? "Working Directory (optional)" : "Directorio de trabajo (opcional)";
    public string OpenCodeWorkspacePlaceholder => IsEnglish ? "Current project directory" : "Directorio del proyecto actual";
    public string SessionsDirectoryLabel => IsEnglish ? "Sessions Directory" : "Directorio de sesiones";
    public string BrowseButtonContent => IsEnglish ? "Browse" : "Examinar";
    public string SessionsDirectoryPlaceholder => IsEnglish ? "~/.config/learn-assist/sessions" : "~/.config/learn-assist/sessions";
    public string SkipButtonContent => IsEnglish ? "Skip" : "Omitir";
    public string SaveButtonContent => IsEnglish ? "Save" : "Aceptar";

    public string[] ProviderNames { get; } = Enum.GetNames<AiProvider>();
    public string[] OpenCodeModels { get; } =
    [
        "opencode/big-pickle",
        "opencode/deepseek-v4.1-flash-free",
        "opencode/flint-40b-free",
        "opencode/glm-5.3-flash-free",
        "opencode/mimo-v2.6-flash-free",
        "opencode/nemotron-3.5-lightning-free",
        "opencode/nemotron-3.5-super-vl-free",
    ];
    public bool IsOpenCode => SelectedProvider == nameof(AiProvider.OpenCode);

    public event Action<ApiConfig>? ConfigSaved;
    public event Action? ConfigSkipped;
    public event Action? BrowseDirectoryRequested;

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(IntroText));
        OnPropertyChanged(nameof(ProviderLabel));
        OnPropertyChanged(nameof(BaseUrlLabel));
        OnPropertyChanged(nameof(BaseUrlPlaceholder));
        OnPropertyChanged(nameof(OpenCodeInfo));
        OnPropertyChanged(nameof(ApiKeyLabel));
        OnPropertyChanged(nameof(ApiKeyPlaceholder));
        OnPropertyChanged(nameof(OpenCodeModelLabel));
        OnPropertyChanged(nameof(ModelLabel));
        OnPropertyChanged(nameof(ModelPlaceholder));
        OnPropertyChanged(nameof(OpenCodeExecutableLabel));
        OnPropertyChanged(nameof(OpenCodeExecutablePlaceholder));
        OnPropertyChanged(nameof(OpenCodeWorkspaceLabel));
        OnPropertyChanged(nameof(OpenCodeWorkspacePlaceholder));
        OnPropertyChanged(nameof(SessionsDirectoryLabel));
        OnPropertyChanged(nameof(BrowseButtonContent));
        OnPropertyChanged(nameof(SessionsDirectoryPlaceholder));
        OnPropertyChanged(nameof(SkipButtonContent));
        OnPropertyChanged(nameof(SaveButtonContent));
    }

    public ApiConfigViewModel()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        SessionsDirectory = Path.Combine(appData, "learn-assist", "sessions");
        _activeProvider = SelectedProvider;
        UpdateDefaults();
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    public ApiConfigViewModel(ApiConfig existing)
    {
        SelectedProvider = existing.Provider.ToString();
        BaseUrl = existing.BaseUrl;
        ApiKey = existing.ApiKey;
        Model = existing.Model;
        SessionsDirectory = existing.SessionsDirectory;
        OpenCodeExecutablePath = existing.OpenCodeExecutablePath;
        OpenCodeWorkspaceDirectory = existing.OpenCodeWorkspaceDirectory;
        foreach (var savedModel in existing.SavedModels)
            _savedModels[savedModel.Key] = savedModel.Value;

        if (!string.IsNullOrWhiteSpace(existing.Model))
            _savedModels[SelectedProvider] = existing.Model;

        _activeProvider = SelectedProvider;
    }

    partial void OnSelectedProviderChanging(string value)
    {
        if (!string.IsNullOrWhiteSpace(Model))
            _savedModels[_activeProvider] = Model.Trim();
    }

    partial void OnSelectedProviderChanged(string value)
    {
        OnPropertyChanged(nameof(IsOpenCode));

        if (Enum.TryParse<AiProvider>(value, out var provider))
        {
            _activeProvider = value;
            if (_savedModels.TryGetValue(value, out var savedModel) &&
                !string.IsNullOrWhiteSpace(savedModel))
            {
                Model = savedModel;
            }
            else
            {
                Model = new ApiConfig { Provider = provider }.GetDefaultModel();
            }
        }

        UpdateDefaults();
    }

    private void UpdateDefaults()
    {
        if (!Enum.TryParse<AiProvider>(SelectedProvider, out var provider))
            return;

        var defaults = new ApiConfig { Provider = provider };
        if (string.IsNullOrEmpty(BaseUrl) || BaseUrl == "https://api.openai.com"
            || BaseUrl == "https://api.anthropic.com"
            || BaseUrl == "https://generativelanguage.googleapis.com"
            || BaseUrl == "http://localhost:11434"
            || BaseUrl == "https://integrate.api.nvidia.com/v1"
            || BaseUrl == "https://opencode.ai/zen/v1"
            || BaseUrl == "http://127.0.0.1:4096")
        {
            BaseUrl = defaults.GetDefaultBaseUrl();
        }

        if (string.IsNullOrWhiteSpace(Model))
            Model = _savedModels.TryGetValue(SelectedProvider, out var savedModel) &&
                    !string.IsNullOrWhiteSpace(savedModel)
                ? savedModel
                : defaults.GetDefaultModel();

        if (provider == AiProvider.OpenCode &&
            !OpenCodeModels.Contains(Model, StringComparer.OrdinalIgnoreCase))
        {
            Model = defaults.GetDefaultModel();
        }
    }

    [RelayCommand]
    private void BrowseSessionsDirectory()
    {
        BrowseDirectoryRequested?.Invoke();
    }

    [RelayCommand]
    private void Save()
    {
        var isOllama = SelectedProvider == nameof(AiProvider.Ollama);
        var isLocalOpenCode = SelectedProvider == nameof(AiProvider.OpenCode);
        if (!isOllama && !isLocalOpenCode && string.IsNullOrWhiteSpace(ApiKey))
        {
            ErrorMessage = IsEnglish ? "Enter an API key." : "Introduce una clave de API.";
            return;
        }

        if (!isLocalOpenCode && string.IsNullOrWhiteSpace(BaseUrl))
        {
            ErrorMessage = IsEnglish ? "Enter a base URL." : "Introduce una URL base.";
            return;
        }

        if (!Enum.TryParse<AiProvider>(SelectedProvider, out var provider))
        {
            ErrorMessage = IsEnglish ? "The selected provider is not valid." : "El proveedor seleccionado no es válido.";
            return;
        }

        var config = new ApiConfig
        {
            Provider = provider,
            BaseUrl = BaseUrl.TrimEnd('/'),
            ApiKey = ApiKey,
            Model = string.IsNullOrWhiteSpace(Model) ? new ApiConfig { Provider = provider }.GetDefaultModel() : Model,
            SessionsDirectory = SessionsDirectory,
            OpenCodeExecutablePath = OpenCodeExecutablePath.Trim(),
            OpenCodeWorkspaceDirectory = OpenCodeWorkspaceDirectory.Trim(),
        };
        _savedModels[provider.ToString()] = config.Model.Trim();
        config.SavedModels = new Dictionary<string, string>(_savedModels, StringComparer.OrdinalIgnoreCase);

        try
        {
            ConfigEncryption.SaveConfig(config);
            ConfigSaved?.Invoke(config);
        }
        catch (Exception ex)
        {
            ErrorMessage = IsEnglish
                ? $"Could not save configuration: {ex.Message}"
                : $"No se pudo guardar la configuración: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Skip()
    {
        ConfigSkipped?.Invoke();
    }
}
