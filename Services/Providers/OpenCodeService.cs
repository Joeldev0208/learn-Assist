using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using learn_Assist.Models;

namespace learn_Assist.Services.Providers;

public sealed class OpenCodeService : IAiService
{
    private readonly ApiConfig _config;
    private bool _disposed;

    public OpenCodeService(ApiConfig config)
    {
        _config = config;
    }

    public async Task<string> AskAsync(string message, List<ChatMessage> history)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var prompt = BuildPrompt(message, history);
        try
        {
            return await RunProcessAsync(
                BuildNativeStartInfo(),
                prompt,
                "OpenCode could not be started.");
        }
        catch (Win32Exception) when (OperatingSystem.IsWindows())
        {
            return await RunProcessAsync(
                BuildWslStartInfo(prompt),
                prompt,
                "OpenCode could not be started natively or through WSL.");
        }
    }

    public static string[] BuildArguments(string? model) =>
    [
        "run",
        "--model", string.IsNullOrWhiteSpace(model) ? "opencode/big-pickle" : model.Trim(),
        "--format", "json",
    ];

    public static string BuildPrompt(string message, IReadOnlyList<ChatMessage> history)
    {
        var prompt = new StringBuilder();
        var hasSystemPrompt = false;

        foreach (var item in history)
        {
            if (item.Role == MessageRole.System)
            {
                prompt.AppendLine(item.Content);
                prompt.AppendLine();
                hasSystemPrompt = true;
            }
            else
            {
                prompt.Append(item.Role == MessageRole.User ? "User: " : "Assistant: ");
                prompt.AppendLine(item.Content);
            }
        }

        if (!hasSystemPrompt)
            prompt.AppendLine(AiPrompt.System);

        prompt.Append("User: ").Append(message);
        return prompt.ToString();
    }

    public static string[] BuildWslArguments(string? model, string prompt, string workspace)
    {
        var arguments = new List<string>
        {
            "--cd",
            ConvertWindowsPathToWsl(workspace),
            "--",
            "opencode",
        };
        arguments.AddRange(BuildArguments(model));
        return [.. arguments];
    }

    public static string ConvertWindowsPathToWsl(string path)
    {
        if (path.Length >= 2 && path[1] == ':' && char.IsLetter(path[0]))
        {
            var drive = char.ToLowerInvariant(path[0]);
            var remainder = path[2..].Replace('\\', '/').TrimStart('/');
            return $"/mnt/{drive}/{remainder}";
        }

        return path.Replace('\\', '/');
    }

    public static string ExtractResponseText(string output)
    {
        var text = new StringBuilder();
        string? error = null;

        foreach (var line in output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var type = root.TryGetProperty("type", out var typeElement)
                    ? typeElement.GetString()
                    : null;

                if (type == "text"
                    && root.TryGetProperty("part", out var part)
                    && part.TryGetProperty("text", out var partText))
                {
                    text.Append(partText.GetString());
                }
                else if (type == "error"
                    && root.TryGetProperty("error", out var errorElement))
                {
                    error = ExtractErrorMessage(errorElement);
                }
            }
            catch (JsonException)
            {
            }
        }

        if (error is not null)
            throw new InvalidOperationException($"OpenCode no pudo generar una respuesta: {error}");
        if (text.Length == 0)
            throw new InvalidOperationException("OpenCode no devolvió texto.");

        return text.ToString();
    }

    private static string ExtractErrorMessage(JsonElement error)
    {
        if (error.ValueKind == JsonValueKind.String)
            return error.GetString() ?? "Error desconocido.";

        if (error.ValueKind == JsonValueKind.Object
            && error.TryGetProperty("message", out var message)
            && message.ValueKind == JsonValueKind.String)
        {
            return message.GetString() ?? "Error desconocido.";
        }

        return error.ToString();
    }

    private static string ResolveExecutable(string configuredPath) =>
        string.IsNullOrWhiteSpace(configuredPath) ? "opencode" : configuredPath.Trim();

    private static string ResolveWorkspace(string configuredPath) =>
        string.IsNullOrWhiteSpace(configuredPath) || !Directory.Exists(configuredPath)
            ? Environment.CurrentDirectory
            : configuredPath;

    private ProcessStartInfo BuildNativeStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveExecutable(_config.OpenCodeExecutablePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            WorkingDirectory = ResolveWorkspace(_config.OpenCodeWorkspaceDirectory),
        };

        foreach (var argument in BuildArguments(_config.Model))
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private ProcessStartInfo BuildWslStartInfo(string prompt)
    {
        var workspace = ResolveWorkspace(_config.OpenCodeWorkspaceDirectory);
        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
        };

        foreach (var argument in BuildWslArguments(_config.Model, prompt, workspace))
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private static async Task<string> RunProcessAsync(
        ProcessStartInfo startInfo,
        string prompt,
        string startError)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(startError);
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();
        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new InvalidOperationException(
                $"OpenCode failed with exit code {process.ExitCode}: {detail.Trim()}".Trim());
        }

        return ExtractResponseText(output);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
