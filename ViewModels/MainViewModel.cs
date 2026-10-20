using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly string _userEmail;
    private readonly string _userKey;
    private readonly bool _hasAiConfig;
    private readonly LearningProfileService? _profileService;
    private SessionPersistenceService? _persistence;
    private readonly UpdateService? _updateService;
    private Action? _configureAiRequested;
    private bool _configureAiPending;

    public SessionListViewModel SessionList { get; }
    public ChatViewModel Chat { get; }
    public DocumentListViewModel DocumentList { get; }
    public TutorialViewModel Tutorial { get; }
    public LearningProfile? LearningProfile { get; private set; }
    public string UserId => _userKey;

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
    public event Action? SignOutRequested;
    public event Action? EditLearningProfileRequested;
    public IRelayCommand SignOutCommand { get; }

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

/*     [ObservableProperty]
    public partial bool IsLearningSessionComplete { get; set; }

    public string LearningStepLabel => $"Step {LearningStep} of 3";
    public string LearningProgressText => LearningStep switch
    {
        1 => "Understand the idea",
        2 => "Practice with a small example",
        _ => "Explain it in your own words",
    };
    public string LearningActionText => LearningStep == 3 ? "Finish session" : "Next step";
 */
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
        ThemeService.ToggleAndRestart();
    }

    [RelayCommand]
    private void ReconfigureAi()
    {
        _configureAiRequested?.Invoke();
    }

    private void SignOut()
    {
        SignOutRequested?.Invoke();
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
    private async Task StartLearningSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(LearningGoal))
            LearningGoal = "A topic I want to understand";

        IsLearningSessionActive = true;
/*         IsLearningSessionComplete = false; */
        LearningStep = 1;
        Chat.AddWelcomeMessage();
        Chat.MessageText = $"Help me learn {LearningGoal.Trim()} in a short focused session.";
        Chat.SendMessageCommand.Execute(null);
        await SaveLearningProfileAsync();
    }

    [RelayCommand]
    private async Task StartQuickSessionAsync()
    {
        LearningGoal = "one useful idea in five minutes";
        await StartLearningSessionAsync();
    }

    [RelayCommand]
/*     private async Task CompleteLearningStepAsync()
    {
        if (LearningStep < 3)
        {
            LearningStep++;
        }
        else
        {
            IsLearningSessionComplete = true;
        }

        await SaveLearningProfileAsync();
    } */

 /*    [RelayCommand]
    private async Task EndLearningSessionAsync()
    {
        IsLearningSessionActive = false;
        IsLearningSessionComplete = false;
        LearningStep = 1;
        await SaveLearningProfileAsync();
    } */

/*     [RelayCommand] */
    private void NeedLearningHelp()
    {
        Chat.MessageText = $"Explain {LearningGoal} with a simpler example and one short practice question.";
    }

/*     partial void OnLearningStepChanged(int value)
    {
        OnPropertyChanged(nameof(LearningStepLabel));
        OnPropertyChanged(nameof(LearningProgressText));
        OnPropertyChanged(nameof(LearningActionText));
    }
 */
    public MainViewModel()
        : this(new MockAiService(), "preview@example.com")
    {
        IsLearningSessionActive = true;
        Tutorial.IsVisible = false;
        LearningGoal = "C# async and await";
        Chat.Messages.Add(new ChatMessage
        {
            Role = MessageRole.User,
            Content = "Can you explain how async and await work in C#?",
        });
        Chat.Messages.Add(new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = """
                ## Async and await

                `async` lets a method perform asynchronous work without blocking the UI thread.

                ```csharp
                var result = await LoadAsync();
                ```

                The `await` keyword pauses this method until the operation completes, then continues with the result.
                """,
        });
    }

    public MainViewModel(
        IAiService aiService,
        string userEmail,
        string? userId = null,
        ApiConfig? config = null,
        SessionPersistenceService? persistence = null,
        UpdateService? updateService = null,
        LearningProfileService? profileService = null,
        LearningProfile? learningProfile = null)
    {
        SignOutCommand = new RelayCommand(SignOut);
        _userEmail = userEmail;
        _userKey = string.IsNullOrWhiteSpace(userId) ? userEmail.Trim().ToLowerInvariant() : userId;
        _hasAiConfig = config is not null;
        _profileService = profileService;
        _persistence = persistence;
        _updateService = updateService;
        LearningProfile = learningProfile;

        Chat = new ChatViewModel(aiService, persistence);
        UpdateChatProfileContext();
        SessionList = new SessionListViewModel(persistence);
        DocumentList = new DocumentListViewModel();
        Tutorial = new TutorialViewModel();

        SessionList.CurrentUserEmail = userEmail;
        SessionList.SessionSelected += OnSessionSelected;
        SessionList.NewSessionRequested += OnNewSessionRequested;

        DocumentList.Documents.CollectionChanged += (_, _) => UpdateChatDocumentContext();

        Tutorial.TutorialFinished += OnTutorialFinished;

        if (!TutorialStateService.HasBeenSeen(_userKey))
            Tutorial.IsVisible = true;
        else if (config is null)
        {
            _configureAiPending = true;
        }

        if (_updateService is { IsSupported: true })
            CheckForUpdates();
    }

    public async Task InitializeAsync()
    {
        if (_profileService is not null && !string.IsNullOrWhiteSpace(_userKey))
        {
            LearningProfile ??= await _profileService.LoadAsync(_userKey);
            if (LearningProfile is not null)
                ApplyLearningProfile(LearningProfile);
        }

        await SessionList.LoadSessionsAsync();
        await DocumentList.LoadDocumentsAsync();

        if (SessionList.ActiveSession is null)
        {
            var initialSession = SessionList.CreateNewSession();
            Chat.SetCurrentSession(initialSession);
        }
    }

    private void UpdateChatProfileContext()
    {
        Chat.SetProfileContext(FeatureFlags.ProfileContextEnabled
            ? LearningProfileContext.Build(LearningProfile)
            : null);
    }

    private void UpdateChatDocumentContext()
    {
        var docs = DocumentList.Documents
            .Where(d => !string.IsNullOrWhiteSpace(d.Content))
            .Select(d => $"### {d.Name}\n{d.Content}")
            .ToList();

        Chat.SetDocumentContext(docs.Count > 0
            ? "Documentos importados como contexto:\n\n" + string.Join("\n\n---\n\n", docs)
            : null);
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

    [RelayCommand]
    private void EditLearningProfile()
    {
        EditLearningProfileRequested?.Invoke();
    }

    public void ApplyLearningProfile(LearningProfile profile)
    {
        LearningProfile = profile;
        LearningGoal = profile.LearningGoal;
        LearningStep = profile.LearningStep;
        IsLearningSessionActive = profile.IsLearningSessionActive;
/*         IsLearningSessionComplete = profile.IsLearningSessionComplete; */
        OnPropertyChanged(nameof(LearningProfile));

        UpdateChatProfileContext();
    }

    private void OnTutorialFinished()
    {
        TutorialStateService.MarkSeen(_userKey);

        if (!_hasAiConfig)
            _configureAiRequested?.Invoke();
    }

    public async Task ApplyConfigAsync(ApiConfig config)
    {
        var aiService = AiServiceFactory.Create(config);

        SessionPersistenceService? persistence = null;
        if (!string.IsNullOrEmpty(config.SessionsDirectory))
        {
            persistence = new SessionPersistenceService(config.SessionsDirectory);
            _persistence = persistence;
            await SessionList.SetPersistenceAsync(persistence);
        }

        Chat.SetAiService(aiService, persistence);
    }

    private void OnSessionSelected(Models.ChatSession session)
    {
        IsLearningSessionActive = true;
/*         IsLearningSessionComplete = false; */
        Chat.LoadSession(session);
    }

    private async Task SaveLearningProfileAsync()
    {
        if (_profileService is null || string.IsNullOrWhiteSpace(_userKey))
            return;

        var profile = LearningProfile ?? new LearningProfile { UserId = _userKey };
        profile.LearningGoal = LearningGoal;
        profile.IsLearningSessionActive = IsLearningSessionActive;
        profile.LearningStep = LearningStep;
/*         profile.IsLearningSessionComplete = IsLearningSessionComplete; */
        await _profileService.SaveAsync(profile);
        LearningProfile = profile;
    }

    private void OnNewSessionRequested()
    {
        var session = SessionList.CreateNewSession();
        Chat.SetCurrentSession(session);
        Chat.AddWelcomeMessage();
    }
}
