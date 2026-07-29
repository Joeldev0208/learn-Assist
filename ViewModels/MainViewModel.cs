using System;
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

    public SessionListViewModel SessionList { get; }
    public ChatViewModel Chat { get; }
    public DocumentListViewModel DocumentList { get; }
    public TutorialViewModel Tutorial { get; }

    public event Action? ConfigureAiRequested;

    [RelayCommand]
    private void ReconfigureAi()
    {
        ConfigureAiRequested?.Invoke();
    }

    public MainViewModel(IAiService aiService, string userEmail, ApiConfig? config = null, SessionPersistenceService? persistence = null)
    {
        _userEmail = userEmail;
        _persistence = persistence;

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
