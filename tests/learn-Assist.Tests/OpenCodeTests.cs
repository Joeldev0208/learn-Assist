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
                new() { Role = MessageRole.User, Content = "Explain async." },
                new() { Role = MessageRole.Assistant, Content = "It avoids blocking." },
            ]);

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
