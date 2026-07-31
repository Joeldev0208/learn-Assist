using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class SessionListViewModel : ViewModelBase
{
    private SessionPersistenceService? _persistence;

    public ObservableCollection<ChatSession> Sessions { get; } = [];

    [ObservableProperty]
    public partial ChatSession? ActiveSession { get; set; }

    partial void OnActiveSessionChanged(ChatSession? value)
    {
        if (value is not null)
            SessionSelected?.Invoke(value);
    }

    [ObservableProperty]
    public partial string CurrentUserEmail { get; set; } = string.Empty;

    public event Action<ChatSession>? SessionSelected;
    public event Action? NewSessionRequested;

    public SessionListViewModel(SessionPersistenceService? persistence = null)
    {
        _persistence = persistence;
        _ = LoadSessionsAsync();
    }

    public void SetPersistence(SessionPersistenceService? persistence)
    {
        _persistence = persistence;
        _ = LoadSessionsAsync();
    }

    private async Task LoadSessionsAsync()
    {
        try
        {
            if (_persistence is null)
            {
                
                return;
            }

            var loaded = await _persistence.LoadSessionsAsync();
            if (loaded.Count == 0)
            {
               
                return;
            }

            Sessions.Clear();
            foreach (var session in loaded.OrderByDescending(s => s.CreatedAt))
                Sessions.Add(session);

            if (Sessions.Count > 0)
                ActiveSession = Sessions[0];
        }
        catch
        {
        }
    }

    [RelayCommand]
    private void SelectSession(ChatSession? session)
    {
        if (session is null)
            return;
        ActiveSession = session;
        SessionSelected?.Invoke(session);
    }

    [RelayCommand]
    private void NewSession()
    {
        NewSessionRequested?.Invoke();
    }

    public ChatSession CreateNewSession()
    {
        var session = new ChatSession
        {
            Title = "New Chat",
        };
        Sessions.Insert(0, session);
        ActiveSession = session;
        return session;
    }
}
