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
    private bool _resourcesOnly;
    private bool _hasSelectedResources;

    public ChatViewModel(IAiService aiService, SessionPersistenceService? persistence = null)
    {
        _aiService = aiService;
        _persistence = persistence;
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(ResourceModeHint));
        OnPropertyChanged(nameof(LanguageHint));
        OnPropertyChanged(nameof(Language));
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

    public void Dispose()
    {
        _aiService.Dispose();
    }

    public void SetSystemPrompt(string? systemPrompt)
    {
        _systemPrompt = systemPrompt;
    }

    /// <summary>
    /// Indica si el chat está en modo «Solo recursos».
    /// </summary>
    public bool IsResourcesOnlyMode => _resourcesOnly;

    /// <summary>
    /// Indica si al menos un documento marcado tiene contenido para enviar.
    /// </summary>
    public bool HasSelectedResources => _hasSelectedResources;

    /// <summary>
    /// El modo estricto está activo pero no hay ningún recurso marcado: no se
    /// puede enviar, porque la IA no tendría de dónde responder.
    /// </summary>
    public bool IsResourceModeBlocked => _resourcesOnly && !_hasSelectedResources;

    public string? ResourceModeHint => IsResourceModeBlocked
        ? (LanguageService.IsEnglish
            ? "Resource-only mode: select at least one resource in the Resources panel to send."
            : "Modo Solo recursos: marca al menos un recurso en el panel Recursos para poder enviar.")
        : null;
    public bool Language => LanguageService.IsEnglish is true && !IsResourceModeBlocked;
    public string? LanguageHint => IsResourceModeBlocked
        ? (LanguageService.IsEnglish
            ? "Resource-only mode: select at least one resource in the Resources panel to send."
            : "Modo Solo recursos: marca al menos un recurso en el panel Recursos para poder enviar.")
        : null;
    public bool CanSendMessage => !IsLoading && !IsResourceModeBlocked;

    /// <summary>
    /// Sincroniza el modo estricto y el estado de la selección de recursos.
    /// Lo llama <see cref="MainViewModel"/> cada vez que cambia el interruptor
    /// o la selección de documentos.
    /// </summary>
    public void SetResourceMode(bool resourcesOnly, bool hasSelectedResources)
    {
        if (_resourcesOnly == resourcesOnly && _hasSelectedResources == hasSelectedResources)
            return;

        _resourcesOnly = resourcesOnly;
        _hasSelectedResources = hasSelectedResources;

        OnPropertyChanged(nameof(IsResourcesOnlyMode));
        OnPropertyChanged(nameof(HasSelectedResources));
        OnPropertyChanged(nameof(IsResourceModeBlocked));
        OnPropertyChanged(nameof(ResourceModeHint));
        OnPropertyChanged(nameof(CanSendMessage));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSendMessage));
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

        if (IsResourceModeBlocked)
        {
            ErrorMessage = ResourceModeHint;
            return;
        }

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
                    if (string.Equals(_currentSession.Title, "Nuevo chat", StringComparison.OrdinalIgnoreCase))
                    {
                        _currentSession.Title = CreateSessionTitle(text);
                    }

                    _currentSession.Messages = new ObservableCollection<ChatMessage>(Messages);
                    await _persistence.SaveSessionAsync(_currentSession);
                }
                catch (Exception ex)
                {
                    ErrorMessage = $"No se pudo guardar la conversación: {ex.Message}";
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
