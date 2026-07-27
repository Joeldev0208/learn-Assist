using CommunityToolkit.Mvvm.ComponentModel;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public SessionListViewModel SessionList { get; }
    public ChatViewModel Chat { get; }
    public DocumentListViewModel DocumentList { get; }

    public MainViewModel(IAiService aiService, string userEmail)
    {
        Chat = new ChatViewModel(aiService);
        SessionList = new SessionListViewModel();
        DocumentList = new DocumentListViewModel();

        SessionList.CurrentUserEmail = userEmail;
        SessionList.SessionSelected += OnSessionSelected;
        SessionList.NewSessionRequested += OnNewSessionRequested;

        Chat.AddWelcomeMessage();
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
