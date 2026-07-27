using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class ChatViewModel : ViewModelBase
{
    private readonly IAiService _aiService;

    public ChatViewModel(IAiService aiService)
    {
        _aiService = aiService;
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty]
    public partial string MessageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public event Action? ScrollToBottomRequested;

    public void LoadSession(ChatSession session)
    {
        Messages.Clear();
        foreach (var msg in session.Messages)
            Messages.Add(msg);
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var text = MessageText?.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        MessageText = string.Empty;
        IsLoading = true;

        var userMsg = new ChatMessage
        {
            Role = MessageRole.User,
            Content = text,
            Timestamp = DateTime.Now,
        };
        Messages.Add(userMsg);
        ScrollToBottomRequested?.Invoke();

        var history = Messages.Select(m => m).ToList();
        var response = await _aiService.AskAsync(text, history);

        var assistantMsg = new ChatMessage
        {
            Role = MessageRole.Assistant,
            Content = response,
            Timestamp = DateTime.Now,
        };
        Messages.Add(assistantMsg);
        ScrollToBottomRequested?.Invoke();

        IsLoading = false;
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
