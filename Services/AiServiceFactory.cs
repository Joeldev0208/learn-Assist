using System;
using learn_Assist.Models;
using learn_Assist.Services.Providers;

namespace learn_Assist.Services;

public static class AiServiceFactory
{
    public static IAiService Create(ApiConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrEmpty(config.ApiKey))
            throw new InvalidOperationException("API key is not configured.");

        return config.Provider switch
        {
            AiProvider.OpenAI => new OpenAiService(config),
            AiProvider.Anthropic => new AnthropicService(config),
            AiProvider.Gemini => new GeminiService(config),
            AiProvider.Ollama => new OllamaService(config),
            _ => throw new ArgumentOutOfRangeException(nameof(config.Provider)),
        };
    }
}
