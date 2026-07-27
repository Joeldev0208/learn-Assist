using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;

namespace learn_Assist.ViewModels;

public partial class SessionListViewModel : ViewModelBase
{
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

    public SessionListViewModel()
    {
        Sessions.Add(new ChatSession
        {
            Title = "Analog Clock React app",
            CreatedAt = DateTime.Now.AddHours(-2),
        });
        Sessions.Add(new ChatSession
        {
            Title = "Simple Design System",
            CreatedAt = DateTime.Now.AddDays(-1),
        });
        Sessions.Add(new ChatSession
        {
            Title = "Figma variable planning",
            CreatedAt = DateTime.Now.AddDays(-2),
        });
        Sessions.Add(new ChatSession
        {
            Title = "OKCLH token algorithm",
            CreatedAt = DateTime.Now.AddDays(-3),
        });
        Sessions.Add(new ChatSession
        {
            Title = "Component naming advice",
            CreatedAt = DateTime.Now.AddDays(-4),
        });

        if (Sessions.Count > 0)
            ActiveSession = Sessions[0];
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
