using System;
using System.Collections.Generic;
using System.IO;
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
    public partial string? ErrorMessage { get; set; }

    public string[] ProviderNames { get; } = Enum.GetNames<AiProvider>();
    public bool IsOpenCode => SelectedProvider == nameof(AiProvider.OpenCode);

    public event Action<ApiConfig>? ConfigSaved;
    public event Action? ConfigSkipped;
    public event Action? BrowseDirectoryRequested;

    public ApiConfigViewModel()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        SessionsDirectory = Path.Combine(appData, "learn-assist", "sessions");
        _activeProvider = SelectedProvider;
        UpdateDefaults();
    }

    public ApiConfigViewModel(ApiConfig existing)
    {
        SelectedProvider = existing.Provider.ToString();
        BaseUrl = existing.BaseUrl;
        ApiKey = existing.ApiKey;
        Model = existing.Model;
        SessionsDirectory = existing.SessionsDirectory;
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
            || BaseUrl == "https://opencode.ai/zen/v1")
        {
            BaseUrl = defaults.GetDefaultBaseUrl();
        }

        if (string.IsNullOrWhiteSpace(Model))
            Model = _savedModels.TryGetValue(SelectedProvider, out var savedModel) &&
                    !string.IsNullOrWhiteSpace(savedModel)
                ? savedModel
                : defaults.GetDefaultModel();
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
        if (!isOllama && string.IsNullOrWhiteSpace(ApiKey))
        {
            ErrorMessage = "Please enter an API key.";
            return;
        }

        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            ErrorMessage = "Please enter a base URL.";
            return;
        }

        if (!Enum.TryParse<AiProvider>(SelectedProvider, out var provider))
        {
            ErrorMessage = "Invalid provider selected.";
            return;
        }

        var config = new ApiConfig
        {
            Provider = provider,
            BaseUrl = BaseUrl.TrimEnd('/'),
            ApiKey = ApiKey,
            Model = string.IsNullOrWhiteSpace(Model) ? new ApiConfig { Provider = provider }.GetDefaultModel() : Model,
            SessionsDirectory = SessionsDirectory,
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
            ErrorMessage = $"Failed to save configuration: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Skip()
    {
        ConfigSkipped?.Invoke();
    }
}
