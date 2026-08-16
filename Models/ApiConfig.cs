using System.Text.Json.Serialization;

namespace learn_Assist.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AiProvider
{
    OpenAI,
    Anthropic,
    Gemini,
    Ollama,
    Nvidia,
}

public class ApiConfig
{
    public AiProvider Provider { get; set; } = AiProvider.OpenAI;
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SessionsDirectory { get; set; } = string.Empty;

    public string GetDefaultBaseUrl() => Provider switch
    {
        AiProvider.OpenAI => "https://api.openai.com",
        AiProvider.Anthropic => "https://api.anthropic.com",
        AiProvider.Gemini => "https://generativelanguage.googleapis.com",
        AiProvider.Ollama => "http://localhost:11434",
        AiProvider.Nvidia => "https://integrate.api.nvidia.com/v1",
        _ => string.Empty,
    };

    public string GetDefaultModel() => Provider switch
    {
        AiProvider.OpenAI => "gpt-4o-mini",
        AiProvider.Anthropic => "claude-3-haiku-20240307",
        AiProvider.Gemini => "gemini-1.5-flash",
        AiProvider.Ollama => "llama3.2",
        AiProvider.Nvidia => "nvidia/llama-3.3-nemotron-super-49b-v1",
        _ => string.Empty,
    };
}
