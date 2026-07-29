using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private IAiService _aiService;
    private SessionPersistenceService? _persistence;
    private ChatSession? _currentSession;

    public ChatViewModel(IAiService aiService, SessionPersistenceService? persistence = null)
    {
        _aiService = aiService;
        _persistence = persistence;
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public event Action? ScrollToBottomRequested;

    public void SetAiService(IAiService service, SessionPersistenceService? persistence = null)
    {
        _aiService.Dispose();
        _aiService = service;
        if (persistence is not null)
            _persistence = persistence;
    }

    public void SetCurrentSession(ChatSession? session)
    {
        _currentSession = session;
    }

    public void LoadSession(ChatSession session)
    {
        _currentSession = session;
        Messages.Clear();
        foreach (var msg in session.Messages)
            Messages.Add(msg);
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var text = MessageText?.Trim();
        if (string.IsNullOrEmpty(text) || IsLoading)
            return;

        MessageText = string.Empty;
        ErrorMessage = null;
        IsLoading = true;

        var history = Messages.ToList();

        var userMsg = new ChatMessage
        {
            Role = MessageRole.User,
            Content = text,
            Timestamp = DateTime.Now,
        };
        Messages.Add(userMsg);
        ScrollToBottomRequested?.Invoke();

        try
        {
            var response = await _aiService.AskAsync(text, history);

            var assistantMsg = new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = response,
                Timestamp = DateTime.Now,
            };
            Messages.Add(assistantMsg);
            ScrollToBottomRequested?.Invoke();

            if (_persistence is not null && _currentSession is not null)
            {
                _currentSession.Messages = new ObservableCollection<ChatMessage>(Messages);

                try
                {
                    await _persistence.SaveSessionAsync(_currentSession);
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"Failed to save conversation: {ex.Message}";
                }
            }
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = ex.Message;
            Messages.Remove(userMsg);
            MessageText = text;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
            Messages.Remove(userMsg);
            MessageText = text;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void AddWelcomeMessage()
    {
        Messages.Clear();
        Messages.Add(new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = "¡Hola! Soy tu asistente de aprendizaje. ¿En qué puedo ayudarte hoy?",
            Timestamp = DateTime.Now,
        });
    }
}
