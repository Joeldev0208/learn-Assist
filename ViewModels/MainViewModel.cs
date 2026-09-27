using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private static bool _isFirstLogin = true;
    private readonly string _userEmail;
    private SessionPersistenceService? _persistence;
    private readonly UpdateService? _updateService;

    public SessionListViewModel SessionList { get; }
    public ChatViewModel Chat { get; }
    public DocumentListViewModel DocumentList { get; }
    public TutorialViewModel Tutorial { get; }

    public event Action? ConfigureAiRequested;
    public event Action? RestartRequested;

    public string ThemeGlyph => ThemeService.IsDark ? "☀️" : "🌙";

    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; set; }

    [ObservableProperty]
    public partial string? UpdateMessage { get; set; }

    [ObservableProperty]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial string? UpdateError { get; set; }

    public string UpdateButtonText => IsUpdating ? "Downloading..." : "Update and restart";

    partial void OnIsUpdatingChanged(bool value)
    {
        OnPropertyChanged(nameof(UpdateButtonText));
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        ThemeService.Toggle();
        OnPropertyChanged(nameof(ThemeGlyph));
    }

    [RelayCommand]
    private void ReconfigureAi()
    {
        ConfigureAiRequested?.Invoke();
    }

    public MainViewModel(IAiService aiService, string userEmail, ApiConfig? config = null, SessionPersistenceService? persistence = null, UpdateService? updateService = null)
    {
        _userEmail = userEmail;
        _persistence = persistence;
        _updateService = updateService;

        Chat = new ChatViewModel(aiService, persistence);
        SessionList = new SessionListViewModel(persistence);
        DocumentList = new DocumentListViewModel();
        Tutorial = new TutorialViewModel();

        SessionList.CurrentUserEmail = userEmail;
        SessionList.SessionSelected += OnSessionSelected;
        SessionList.NewSessionRequested += OnNewSessionRequested;

        var initialSession = SessionList.CreateNewSession();
        Chat.SetCurrentSession(initialSession);

        Tutorial.TutorialFinished += OnTutorialFinished;

        if (_isFirstLogin)
        {
            _isFirstLogin = false;
            Tutorial.IsVisible = true;
        }
        else if (config is null)
        {
            ConfigureAiRequested?.Invoke();
        }

        if (_updateService is { IsSupported: true })
            CheckForUpdates();
    }

    private async void CheckForUpdates()
    {
        try
        {
            var update = await _updateService!.CheckForUpdatesAsync();
            if (update is { IsUpdateAvailable: true })
            {
                IsUpdateAvailable = true;
                UpdateMessage = $"Update available: v{update.LatestVersion}. Restart the app to install it.";
            }
        }
        catch
        {
            // Updates must never break the app — fail silently.
        }
    }

    [RelayCommand]
    private async Task ApplyUpdateAsync()
    {
        if (_updateService is not { IsSupported: true } || !IsUpdateAvailable)
            return;

        IsUpdating = true;
        UpdateError = null;

        try
        {
            await _updateService.StageUpdateAsync();
            UpdateMessage = "Update ready. Restarting to apply it...";
            RestartRequested?.Invoke();
        }
        catch (Exception ex)
        {
            UpdateError = ex.Message;
            IsUpdating = false;
        }
    }

    [RelayCommand]
    private void DismissUpdate()
    {
        IsUpdateAvailable = false;
        UpdateError = null;
    }

    private void OnTutorialFinished()
    {
        if (!ConfigEncryption.ConfigExists())
            ConfigureAiRequested?.Invoke();
    }

    public void ApplyConfig(ApiConfig config)
    {
        var aiService = AiServiceFactory.Create(config);

        SessionPersistenceService? persistence = null;
        if (!string.IsNullOrEmpty(config.SessionsDirectory))
        {
            persistence = new SessionPersistenceService(config.SessionsDirectory);
            _persistence = persistence;
            SessionList.SetPersistence(persistence);
        }

        Chat.SetAiService(aiService, persistence);
    }

    private void OnSessionSelected(Models.ChatSession session)
    {
        Chat.LoadSession(session);
    }

    private void OnNewSessionRequested()
    {
        var session = SessionList.CreateNewSession();
        Chat.SetCurrentSession(session);
        Chat.AddWelcomeMessage();
    }
}
