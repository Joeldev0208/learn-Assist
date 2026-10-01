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
    private Action? _configureAiRequested;
    private bool _configureAiPending;

    public SessionListViewModel SessionList { get; }
    public ChatViewModel Chat { get; }
    public DocumentListViewModel DocumentList { get; }
    public TutorialViewModel Tutorial { get; }

    public event Action? ConfigureAiRequested
    {
        add
        {
            _configureAiRequested += value;
            if (_configureAiPending && value is not null)
            {
                _configureAiPending = false;
                value();
            }
        }
        remove => _configureAiRequested -= value;
    }
    public event Action? RestartRequested;

    public string ThemeGlyph => ThemeService.IsDark ? "☀️" : "🌙";

    [ObservableProperty]
    public partial bool IsUpdateAvailable { get; set; }

    [ObservableProperty]
    public partial string? UpdateMessage { get; set; }

    [ObservableProperty]
    public partial bool IsUpdating { get; set; }

    [ObservableProperty]
    public partial bool IsCompactLayout { get; set; }

    [ObservableProperty]
    public partial bool IsSessionsVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool IsResourcesVisible { get; set; } = true;

    [ObservableProperty]
    public partial string LearningGoal { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLearningSessionActive { get; set; }

    [ObservableProperty]
    public partial int LearningStep { get; set; } = 1;

    [ObservableProperty]
    public partial bool IsLearningSessionComplete { get; set; }

    public string LearningStepLabel => $"Step {LearningStep} of 3";
    public string LearningProgressText => LearningStep switch
    {
        1 => "Understand the idea",
        2 => "Practice with a small example",
        _ => "Explain it in your own words",
    };
    public string LearningActionText => LearningStep == 3 ? "Finish session" : "Next step";

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
        _configureAiRequested?.Invoke();
    }

    [RelayCommand]
    private void ToggleSessions()
    {
        IsSessionsVisible = !IsSessionsVisible;
    }

    [RelayCommand]
    private void ToggleResources()
    {
        IsResourcesVisible = !IsResourcesVisible;
    }

    [RelayCommand]
    private void StartLearningSession()
    {
        if (string.IsNullOrWhiteSpace(LearningGoal))
            LearningGoal = "A topic I want to understand";

        IsLearningSessionActive = true;
        IsLearningSessionComplete = false;
        LearningStep = 1;
        Chat.AddWelcomeMessage();
        Chat.MessageText = $"Help me learn {LearningGoal.Trim()} in a short focused session.";
        Chat.SendMessageCommand.Execute(null);
    }

    [RelayCommand]
    private void StartQuickSession()
    {
        LearningGoal = "one useful idea in five minutes";
        StartLearningSession();
    }

    [RelayCommand]
    private void CompleteLearningStep()
    {
        if (LearningStep < 3)
        {
            LearningStep++;
            return;
        }

        IsLearningSessionComplete = true;
    }

    [RelayCommand]
    private void EndLearningSession()
    {
        IsLearningSessionActive = false;
        IsLearningSessionComplete = false;
        LearningStep = 1;
    }

    [RelayCommand]
    private void NeedLearningHelp()
    {
        Chat.MessageText = $"Explain {LearningGoal} with a simpler example and one short practice question.";
    }

    partial void OnLearningStepChanged(int value)
    {
        OnPropertyChanged(nameof(LearningStepLabel));
        OnPropertyChanged(nameof(LearningProgressText));
        OnPropertyChanged(nameof(LearningActionText));
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
            _configureAiPending = true;
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
            _configureAiRequested?.Invoke();
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
