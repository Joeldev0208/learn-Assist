using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".csv", ".log", ".json", ".xml", ".yaml", ".yml",
        ".cs", ".js", ".ts", ".py", ".java", ".html", ".css", ".sh", ".sql",
    };

    // Formats accepted by all three vision providers (OpenAI, Anthropic, Gemini).
    private static readonly HashSet<string> VisionImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp",
    };

    private IAiService _aiService;
    private SessionPersistenceService? _persistence;
    private ChatSession? _currentSession;

    public ChatViewModel(IAiService aiService, SessionPersistenceService? persistence = null)
    {
        _aiService = aiService;
        _persistence = persistence;
    }

    public ObservableCollection<ChatMessage> Messages { get; } = [];

    public ObservableCollection<UserDocument> AttachedDocuments { get; } = [];

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
        AttachedDocuments.Clear();
        foreach (var msg in session.Messages)
            Messages.Add(msg);
    }

    public void AttachDocument(UserDocument document)
    {
        if (!document.IsAttachable || AttachedDocuments.Any(d => d.Id == document.Id))
            return;

        ErrorMessage = null;
        AttachedDocuments.Add(document);
    }

    [RelayCommand]
    private void RemoveAttachedDocument(UserDocument? document)
    {
        if (document is not null)
            AttachedDocuments.Remove(document);
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        var text = MessageText?.Trim();
        var hasAttachments = AttachedDocuments.Count > 0;
        if ((string.IsNullOrEmpty(text) && !hasAttachments) || IsLoading)
            return;

        MessageText = string.Empty;
        ErrorMessage = null;
        IsLoading = true;

        var history = Messages.ToList();

        var userMsg = new ChatMessage
        {
            Role = MessageRole.User,
            Content = text + (hasAttachments ? $"\n📎 {AttachedDocuments.Count} attachment(s): {string.Join(", ", AttachedDocuments.Select(d => d.Name))}" : string.Empty),
            Timestamp = DateTime.Now,
        };
        Messages.Add(userMsg);
        ScrollToBottomRequested?.Invoke();

        List<MessageAttachment>? attachments = null;
        try
        {
            attachments = hasAttachments ? BuildAttachments() : null;

            var prompt = string.IsNullOrEmpty(text) && hasAttachments
                ? "Please analyze the attached resource(s)."
                : text!;

            var response = await _aiService.AskAsync(prompt, history, attachments);

            var assistantMsg = new ChatMessage
            {
                Role = MessageRole.Assistant,
                Content = response,
                Timestamp = DateTime.Now,
            };
            Messages.Add(assistantMsg);
            ScrollToBottomRequested?.Invoke();

            AttachedDocuments.Clear();

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
            MessageText = text ?? string.Empty;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Unexpected error: {ex.Message}";
            Messages.Remove(userMsg);
            MessageText = text ?? string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private List<MessageAttachment> BuildAttachments()
    {
        var result = new List<MessageAttachment>();

        foreach (var document in AttachedDocuments)
        {
            var path = document.LocalPath ?? document.FilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                continue;

            try
            {
                if (document.ContentType == DocumentContentType.Image)
                {
                    if (VisionImageExtensions.Contains(Path.GetExtension(path)))
                    {
                        result.Add(new MessageAttachment
                        {
                            FileName = document.Name,
                            Kind = AttachmentKind.Image,
                            MimeType = GetImageMimeType(Path.GetExtension(path)),
                            ImageData = File.ReadAllBytes(path),
                        });
                    }
                    else
                    {
                        result.Add(new MessageAttachment
                        {
                            FileName = document.Name,
                            Kind = AttachmentKind.TextDocument,
                            TextContent = $"[image '{document.Name}' not attachable: format not supported by vision providers]",
                        });
                    }
                }
                else if (IsTextExtractable(Path.GetExtension(path)))
                {
                    result.Add(new MessageAttachment
                    {
                        FileName = document.Name,
                        Kind = AttachmentKind.TextDocument,
                        TextContent = File.ReadAllText(path),
                    });
                }
                else
                {
                    result.Add(new MessageAttachment
                    {
                        FileName = document.Name,
                        Kind = AttachmentKind.TextDocument,
                        TextContent = $"[binary or unsupported file: {document.Name} ({document.SizeDisplay})]",
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not read '{document.Name}': {ex.Message}";
            }
        }

        return result;
    }

    private static bool IsTextExtractable(string extension)
        => TextExtensions.Contains(extension);

    private static string GetImageMimeType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".svg" => "image/svg+xml",
        _ => "image/png",
    };

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
