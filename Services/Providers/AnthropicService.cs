using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services.Providers;

public class AnthropicService : IAiService
{
    private readonly HttpClient _http;

    public void Dispose() => _http.Dispose();
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _model;

    public AnthropicService(ApiConfig config)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _baseUrl = string.IsNullOrEmpty(config.BaseUrl)
            ? "https://api.anthropic.com"
            : config.BaseUrl.TrimEnd('/');
        _apiKey = config.ApiKey;
        _model = string.IsNullOrEmpty(config.Model) ? config.GetDefaultModel() : config.Model;
    }

    public async Task<string> AskAsync(string message, List<ChatMessage> history)
    {
        var messages = new List<object>();

        foreach (var msg in history)
        {
            messages.Add(new
            {
                role = msg.Role == MessageRole.User ? "user" : "assistant",
                content = msg.Content,
            });
        }

        messages.Add(new { role = "user", content = message });

        var body = new
        {
            model = _model,
            max_tokens = 2048,
            system = "You are a helpful learning assistant.",
            messages,
        };

        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/messages")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        var response = await _http.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errMsg = TryExtractErrorMessage(responseJson);
            throw new HttpRequestException($"API error ({response.StatusCode}): {errMsg}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        if (!doc.RootElement.TryGetProperty("content", out var contentArray) || contentArray.GetArrayLength() == 0)
            throw new InvalidOperationException("API response missing 'content' array.");

        var text = contentArray[0].GetProperty("text").GetString();
        return text ?? string.Empty;
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
        return "Check your API key, model name, and quota.";
    }
}
