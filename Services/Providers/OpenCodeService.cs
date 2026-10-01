using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services.Providers;

public class OpenCodeService : IAiService
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    private readonly string _model;
    private bool _disposed;

    public OpenCodeService(ApiConfig config)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl)
            ? config.GetDefaultBaseUrl()
            : config.BaseUrl.TrimEnd('/');
        _apiKey = config.ApiKey;
        _model = string.IsNullOrWhiteSpace(config.Model) ? config.GetDefaultModel() : config.Model;
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

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _http.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var error = TryExtractErrorMessage(responseJson);
            throw new HttpRequestException($"OpenCode API error ({(int)response.StatusCode} {response.StatusCode}): {error}");
        }

        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (!doc.RootElement.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("OpenCode response is missing a non-empty 'choices' array.");
            }

            var choice = choices[0];
            if (!choice.TryGetProperty("message", out var responseMessage)
                || !responseMessage.TryGetProperty("content", out var content))
            {
                throw new InvalidOperationException("OpenCode response is missing 'choices[0].message.content'.");
            }

            return content.GetString() ?? string.Empty;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("OpenCode returned invalid JSON.", ex);
        }
    }

    private static string TryExtractErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString() ?? "Unknown OpenCode error.";

                if (error.TryGetProperty("message", out var message))
                    return message.GetString() ?? error.GetRawText();

                return error.GetRawText();
            }
        }
        catch (JsonException)
        {
        }

        return "Check the OpenCode base URL, API key, and model name.";
    }
}
