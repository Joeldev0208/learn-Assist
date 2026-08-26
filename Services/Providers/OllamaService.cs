using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services.Providers;

public class OllamaService : IAiService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _model;
    private bool _disposed;

    public OllamaService(ApiConfig config)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        _baseUrl = string.IsNullOrEmpty(config.BaseUrl)
            ? config.GetDefaultBaseUrl()
            : config.BaseUrl.TrimEnd('/');
        _apiKey = config.ApiKey;
        _model = string.IsNullOrEmpty(config.Model) ? config.GetDefaultModel() : config.Model;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _http.Dispose();
            _disposed = true;
        }
    }

    public Task<string> AskAsync(string message, List<ChatMessage> history)
        => AskAsync(message, history, null);

    public async Task<string> AskAsync(string message, List<ChatMessage> history, IReadOnlyList<MessageAttachment>? attachments)
    {
        var messages = new List<object>
        {
            new { role = "system", content = "You are a helpful learning assistant." },
        };

        foreach (var msg in history)
        {
            var content = msg.Content;
            if (msg.ImagePaths is { Count: > 0 })
            {
                var names = string.Join(", ", msg.ImagePaths.Select(p => Path.GetFileName(p)));
                content += $" [images: {names}]";
            }
            messages.Add(new
            {
                role = msg.Role == MessageRole.User ? "user" : "assistant",
                content,
            });
        }

        var prompt = message + BuildTextBlocks(attachments);
        messages.Add(new { role = "user", content = prompt });

        var body = new
        {
            model = _model,
            messages,
            stream = false,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };

        if (!string.IsNullOrEmpty(_apiKey))
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _http.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errMsg = TryExtractErrorMessage(responseJson);
            throw new HttpRequestException($"API error ({response.StatusCode}): {errMsg}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return text ?? string.Empty;
    }

    private static string BuildTextBlocks(IReadOnlyList<MessageAttachment>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var attachment in attachments)
        {
            if (attachment.Kind == AttachmentKind.TextDocument && !string.IsNullOrEmpty(attachment.TextContent))
                builder.Append("\n\n").Append(attachment.ToTextBlock());
            else if (attachment.Kind == AttachmentKind.Image)
                builder.Append($"\n\n[image '{attachment.FileName}' omitted: this provider does not support vision]");
        }
        return builder.ToString();
    }

    private static string TryExtractErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                var msg = err.TryGetProperty("message", out var m) ? m.GetString() : null;
                return msg ?? err.GetRawText();
            }
        }
        catch { }
        return "Check that Ollama is running and the model is available.";
    }
}
