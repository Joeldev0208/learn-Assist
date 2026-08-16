using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services.Providers;

public class NvidiaService : IAiService
{
    private const string DefaultBaseUrl = "https://integrate.api.nvidia.com/v1";

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _model;
    private bool _disposed;

    public NvidiaService(ApiConfig config)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _baseUrl = string.IsNullOrEmpty(config.BaseUrl)
            ? DefaultBaseUrl
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

    public async Task<string> AskAsync(string message, List<ChatMessage> history)
    {
        var messages = new List<object>
        {
            new { role = "system", content = "You are a helpful learning assistant." },
        };

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
            messages,
            stream = false,
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        var response = await _http.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errMsg = TryExtractErrorMessage(responseJson);
            throw new HttpRequestException($"API error ({response.StatusCode}): {errMsg}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            throw new InvalidOperationException("API response missing 'choices' array.");

        var text = choices[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

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