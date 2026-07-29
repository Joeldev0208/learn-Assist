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

public class GeminiService : IAiService
{
    private readonly HttpClient _http;

    public void Dispose() => _http.Dispose();
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiService(ApiConfig config)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _apiKey = config.ApiKey;
        _model = string.IsNullOrEmpty(config.Model) ? config.GetDefaultModel() : config.Model;
    }

    public async Task<string> AskAsync(string message, List<ChatMessage> history)
    {
        var contents = new List<object>();

        foreach (var msg in history)
        {
            contents.Add(new
            {
                role = msg.Role == MessageRole.User ? "user" : "model",
                parts = new[] { new { text = msg.Content } },
            });
        }

        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = message } },
        });

        var body = new
        {
            contents,
            systemInstruction = new
            {
                parts = new[] { new { text = "You are a helpful learning assistant." } },
            },
        };

        var json = JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });

        var url = $"https://generativelanguage.googleapis.com/v1/models/{_model}:generateContent?key={_apiKey}";

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        var response = await _http.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errMsg = TryExtractErrorMessage(responseJson);
            throw new HttpRequestException($"API error ({response.StatusCode}): {errMsg}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            if (doc.RootElement.TryGetProperty("promptFeedback", out var feedback))
                throw new InvalidOperationException($"Gemini blocked the request: {feedback.GetRawText()}");

            throw new InvalidOperationException("API response missing 'candidates' array.");
        }

        var parts = candidates[0]
            .GetProperty("content")
            .GetProperty("parts");

        var text = parts[0].GetProperty("text").GetString();
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
