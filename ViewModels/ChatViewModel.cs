using System;
using System.Collections.Generic;
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
    private string? _systemPrompt;

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

    public event Action<bool>? ScrollToBottomRequested;

    public void SetAiService(IAiService service, SessionPersistenceService? persistence = null)
    {
        _aiService.Dispose();
        _aiService = service;
        if (persistence is not null)
            _persistence = persistence;
    }

    public void SetSystemPrompt(string? systemPrompt)
    {
        _systemPrompt = systemPrompt;
    }

    public void SetCurrentSession(ChatSession? session)
    {
        _currentSession = session;
    }

    public void LoadSession(ChatSession session)
    {
        _currentSession = session;
        Messages.Clear();
        MessageText = string.Empty;
        ErrorMessage = null;
        foreach (var msg in session.Messages)
            Messages.Add(msg);
        ScrollToBottomRequested?.Invoke(true);
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        var text = MessageText?.Trim();
        if (string.IsNullOrEmpty(text) || IsLoading)
            return;

        MessageText = string.Empty;
        ErrorMessage = null;
        IsLoading = true;

        var history = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(_systemPrompt))
        {
            history.Add(new ChatMessage
            {
                Role = MessageRole.System,
                Content = _systemPrompt,
            });
        }

        history.AddRange(Messages.ToList());

        var userMsg = new ChatMessage
        {
            Role = MessageRole.User,
            Content = text,
            Timestamp = DateTime.Now,
        };
        Messages.Add(userMsg);
        ScrollToBottomRequested?.Invoke(true);

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
            ScrollToBottomRequested?.Invoke(true);

            if (_persistence is not null && _currentSession is not null)
            {
                try
                {
                    if (string.Equals(_currentSession.Title, "New Chat", StringComparison.OrdinalIgnoreCase))
                    {
                        _currentSession.Title = CreateSessionTitle(text);
                    }

                    _currentSession.Messages = new ObservableCollection<ChatMessage>(Messages);
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
            Content = "Hi! I'm your learning assistant. We'll focus on one useful idea at a time.",
            Timestamp = DateTime.Now,
        });
    }

    private static string CreateSessionTitle(string message)
    {
        const int maxLength = 42;
        var title = message.ReplaceLineEndings(" ").Trim();
        return title.Length <= maxLength ? title : $"{title[..maxLength].TrimEnd()}...";
    }
}
