using CommunityToolkit.Mvvm.ComponentModel;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private static bool _isFirstLogin = true;

    public SessionListViewModel SessionList { get; }
    public ChatViewModel Chat { get; }
    public DocumentListViewModel DocumentList { get; }
    public TutorialViewModel Tutorial { get; }

    public MainViewModel(IAiService aiService, string userEmail)
    {
        Chat = new ChatViewModel(aiService);
        SessionList = new SessionListViewModel();
        DocumentList = new DocumentListViewModel();
        Tutorial = new TutorialViewModel();

        SessionList.CurrentUserEmail = userEmail;
        SessionList.SessionSelected += OnSessionSelected;
        SessionList.NewSessionRequested += OnNewSessionRequested;

        Chat.AddWelcomeMessage();

        if (_isFirstLogin)
        {
            _isFirstLogin = false;
            Tutorial.IsVisible = true;
        }
    }

    private void OnSessionSelected(Models.ChatSession session)
    {
        Chat.LoadSession(session);
    }

    private void OnNewSessionRequested()
    {
        var session = SessionList.CreateNewSession();
        Chat.AddWelcomeMessage();
    }
}
