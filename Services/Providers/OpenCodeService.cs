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
                BuildNativeStartInfo(prompt),
                "OpenCode could not be started.");
        }
        catch (Win32Exception) when (OperatingSystem.IsWindows())
        {
            var promptFile = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(promptFile, prompt, new UTF8Encoding(false));
                return await RunProcessAsync(
                    BuildWslStartInfo(promptFile),
                    "OpenCode could not be started natively or through WSL.");
            }
            finally
            {
                File.Delete(promptFile);
            }
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
        var systemMessages = history
            .Where(item => item.Role == MessageRole.System)
            .ToList();

        prompt.AppendLine("### INSTRUCCIONES Y CONTEXTO");
        if (systemMessages.Count == 0)
            prompt.AppendLine(AiPrompt.System);

        foreach (var item in systemMessages)
        {
            prompt.AppendLine(item.Content);
            prompt.AppendLine();
        }

        prompt.AppendLine("### HISTORIAL DE CONVERSACIÓN");
        foreach (var item in history.Where(item => item.Role != MessageRole.System))
        {
            prompt.Append(item.Role == MessageRole.User ? "User: " : "Assistant: ");
            prompt.AppendLine(item.Content);
        }

        prompt.AppendLine();
        prompt.AppendLine("### PREGUNTA ACTUAL");
        prompt.Append("User: ").Append(message);
        return prompt.ToString();
    }

    public static string[] BuildWslArguments(string? model, string prompt, string workspace)
    {
        var commandArguments = new List<string>(BuildArguments(model))
        {
            prompt,
        };
        var command = "exec opencode " + string.Join(
            " ",
            commandArguments.Select(ShellQuote));

        var arguments = new List<string>
        {
            "--cd",
            ConvertWindowsPathToWsl(workspace),
            "--exec",
            "bash",
            "-lc",
            command,
        };
        return [.. arguments];
    }

    public static string[] BuildWslFileArguments(string? model, string promptFile, string workspace)
    {
        var promptPath = ConvertWindowsPathToWsl(promptFile);
        var command = "exec opencode "
            + string.Join(" ", BuildArguments(model).Select(ShellQuote))
            + " < " + ShellQuote(promptPath);

        var arguments = new List<string>
        {
            "--cd",
            ConvertWindowsPathToWsl(workspace),
            "--exec",
            "bash",
            "-lc",
            command,
        };
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

    private static string ShellQuote(string value) =>
        $"'{value.Replace("'", "'\\''")}'";

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
                    error = errorElement.GetRawText();
                }
            }
            catch (JsonException)
            {
            }
        }

        if (error is not null)
            throw new InvalidOperationException($"OpenCode returned an error: {error}");
        if (text.Length == 0)
            throw new InvalidOperationException("OpenCode returned no text.");

        return text.ToString();
    }

    private static string ResolveExecutable(string configuredPath) =>
        string.IsNullOrWhiteSpace(configuredPath) ? "opencode" : configuredPath.Trim();

    private static string ResolveWorkspace(string configuredPath) =>
        string.IsNullOrWhiteSpace(configuredPath) || !Directory.Exists(configuredPath)
            ? Environment.CurrentDirectory
            : configuredPath;

    private ProcessStartInfo BuildNativeStartInfo(string prompt)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveExecutable(_config.OpenCodeExecutablePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = ResolveWorkspace(_config.OpenCodeWorkspaceDirectory),
        };

        foreach (var argument in BuildArguments(_config.Model))
            startInfo.ArgumentList.Add(argument);
        startInfo.ArgumentList.Add(prompt);
        return startInfo;
    }

    private ProcessStartInfo BuildWslStartInfo(string promptFile)
    {
        var workspace = ResolveWorkspace(_config.OpenCodeWorkspaceDirectory);
        var startInfo = new ProcessStartInfo
        {
            FileName = "wsl.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in BuildWslFileArguments(_config.Model, promptFile, workspace))
            startInfo.ArgumentList.Add(argument);
        return startInfo;
    }

    private static async Task<string> RunProcessAsync(
        ProcessStartInfo startInfo,
        string startError)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(startError);
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
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
