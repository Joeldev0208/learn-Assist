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

        messages.Add(OpenAI.Chat.ChatMessage.CreateUserMessage(message));

        var completion = await _client.CompleteChatAsync(messages);

        return completion.Value.Content[0].Text;
    }
}
