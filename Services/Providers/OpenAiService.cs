using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using learn_Assist.Models;
using OpenAI.Chat;
namespace learn_Assist.Services.Providers;

public class OpenAiService : IAiService
{
    private readonly ChatClient _client;
    private readonly string _model;
    private bool _disposed;

    public OpenAiService(ApiConfig config)
    {
        var apiKey = config.ApiKey;
        _model = string.IsNullOrEmpty(config.Model) ? config.GetDefaultModel() : config.Model;
        _client = new ChatClient(_model, apiKey);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            (_client as IDisposable)?.Dispose();
            _disposed = true;
        }
    }

    public async Task<string> AskAsync(string message, List<Models.ChatMessage> history)
        => await AskAsync(message, history, null);

    public async Task<string> AskAsync(string message, List<Models.ChatMessage> history, IReadOnlyList<MessageAttachment>? attachments)
    {
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            OpenAI.Chat.ChatMessage.CreateSystemMessage("You are a helpful learning assistant."),
        };

        foreach (var msg in history)
        {
            if (msg.Role == MessageRole.User)
                messages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage(msg.Content));
            else
                messages.Add(OpenAI.Chat.ChatMessage.CreateAssistantMessage(msg.Content));
        }

        var textParts = new List<string>();
        var imageParts = new List<ChatMessageContentPart>();
        if (attachments is not null)
        {
            foreach (var attachment in attachments)
            {
                if (attachment.Kind == AttachmentKind.TextDocument && !string.IsNullOrEmpty(attachment.TextContent))
                    textParts.Add(attachment.ToTextBlock());
                else if (attachment.Kind == AttachmentKind.Image && attachment.ImageData is { Length: > 0 })
                    imageParts.Add(ChatMessageContentPart.CreateImagePart(
                        BinaryData.FromBytes(attachment.ImageData), attachment.MimeType));
            }
        }

        if (imageParts.Count > 0)
        {
            var parts = new List<ChatMessageContentPart>();
            if (!string.IsNullOrWhiteSpace(message) || textParts.Count > 0)
                parts.Add(ChatMessageContentPart.CreateTextPart(message + AppendBlocks(textParts)));
            parts.AddRange(imageParts);
            messages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage(parts));
        }
        else
        {
            messages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage(message + AppendBlocks(textParts)));
        }

        var completion = await _client.CompleteChatAsync(messages);

        return completion.Value.Content[0].Text;
    }

    private static string AppendBlocks(List<string> blocks)
        => blocks.Count == 0 ? string.Empty : "\n\n" + string.Join("\n\n", blocks);
}
