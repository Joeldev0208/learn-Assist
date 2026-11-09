using System.Diagnostics;
using System.Text;
using learn_Assist.Models;
using learn_Assist.Services;
using learn_Assist.Services.Providers;
using learn_Assist.ViewModels;
using Xunit;

namespace learn_assist.Tests;

public class OpenCodeTests
{
    [Fact]
    public void BuildArguments_UsesFreeOpenCodeModelByDefault()
    {
        Assert.Equal(
            ["run", "--model", "opencode/big-pickle", "--format", "json"],
            OpenCodeService.BuildArguments(string.Empty));
    }

    [Fact]
    public void ApiConfig_LocalOpenCodeDoesNotRequireApiKey()
    {
        using var service = AiServiceFactory.Create(new ApiConfig
        {
            Provider = AiProvider.OpenCode,
            ApiKey = string.Empty,
        });

        Assert.NotNull(service);
    }

    [Fact]
    public void BuildPromptIncludesHistory()
    {
        var prompt = OpenCodeService.BuildPrompt(
            "What next?",
            [
                new() { Role = MessageRole.System, Content = "Responde en español y conserva las tildes." },
                new() { Role = MessageRole.User, Content = "Explain async." },
                new() { Role = MessageRole.Assistant, Content = "It avoids blocking." },
            ]);

        Assert.Contains("Responde en español y conserva las tildes.", prompt);
        Assert.Contains("### INSTRUCCIONES Y CONTEXTO", prompt);
        Assert.Contains("### HISTORIAL DE CONVERSACIÓN", prompt);
        Assert.Contains("### PREGUNTA ACTUAL", prompt);
        Assert.Contains("Explain async.", prompt);
        Assert.Contains("It avoids blocking.", prompt);
        Assert.EndsWith("User: What next?", prompt);
    }

    [Fact]
    public void ExtractResponseTextReadsJsonTextEvents()
    {
        var output = """
            {"type":"step_start"}
            {"type":"text","part":{"text":"OpenCode "}}
            {"type":"text","part":{"text":"free model works"}}
            """;

        Assert.Equal("OpenCode free model works", OpenCodeService.ExtractResponseText(output));
    }

    [Fact]
    public void BuildWslArgumentsConvertsWorkspaceAndUsesOpenCodeCli()
    {
        var arguments = OpenCodeService.BuildWslArguments(
            "opencode/big-pickle",
            "Say hello",
            @"C:\work\learn-Assist");

        Assert.Equal(
            ["--cd", "/mnt/c/work/learn-Assist", "--exec", "bash", "-lc",
             "exec opencode 'run' '--model' 'opencode/big-pickle' '--format' 'json' 'Say hello'"],
            arguments);
    }

    [Fact]
    public void BuildWslFileArgumentsKeepsPromptOutOfWindowsCommandLine()
    {
        var arguments = OpenCodeService.BuildWslFileArguments(
            "opencode/big-pickle",
            @"C:\Users\user\AppData\Local\Temp\prompt.txt",
            @"C:\work\learn-Assist");

        Assert.Equal(
            ["--cd", "/mnt/c/work/learn-Assist", "--exec", "bash", "-lc",
             "exec opencode 'run' '--model' 'opencode/big-pickle' '--format' 'json' < '/mnt/c/Users/user/AppData/Local/Temp/prompt.txt'"],
            arguments);
    }

    [Fact]
    public void BuildNativeStartInfoDecodesChildOutputAsUtf8()
    {
        using var service = new OpenCodeService(new ApiConfig
        {
            Provider = AiProvider.OpenCode,
        });
        var prompt = "Responde en español: niño, año, ¿qué más? → ≥";

        var startInfo = service.BuildNativeStartInfo(prompt);

        Assert.Equal(Encoding.UTF8, startInfo.StandardOutputEncoding);
        Assert.Equal(Encoding.UTF8, startInfo.StandardErrorEncoding);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.Contains(prompt, startInfo.ArgumentList);
    }

    [Fact]
    public void BuildWslStartInfoDecodesChildOutputAsUtf8()
    {
        using var service = new OpenCodeService(new ApiConfig
        {
            Provider = AiProvider.OpenCode,
        });

        var startInfo = service.BuildWslStartInfo(@"C:\Users\user\AppData\Local\Temp\prompt.txt");

        Assert.Equal(Encoding.UTF8, startInfo.StandardOutputEncoding);
        Assert.Equal(Encoding.UTF8, startInfo.StandardErrorEncoding);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
    }

    [Fact]
    public void ConvertWindowsPathToWslPreservesNonWindowsPaths()
    {
        Assert.Equal("/home/user/project", OpenCodeService.ConvertWindowsPathToWsl("/home/user/project"));
    }

    [Fact]
    public void ApiConfigViewModelListsFreeOpenCodeModels()
    {
        var viewModel = new ApiConfigViewModel();

        Assert.Contains("opencode/big-pickle", viewModel.OpenCodeModels);
        Assert.Contains("opencode/deepseek-v4.1-flash-free", viewModel.OpenCodeModels);
    }
}
