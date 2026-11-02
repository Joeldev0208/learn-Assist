using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;
using learn_Assist.ViewModels;
using Xunit;

namespace learn_assist.Tests;

public class ExamViewModelUrlTests
{
    private sealed class MockWebContentReader : IWebContentReader
    {
        public string? Result { get; set; }
        public string? LastUrl { get; private set; }
        public int CallCount { get; private set; }

        public Task<WebContentFetchResult> FetchAsync(string url, CancellationToken cancellationToken = default)
        {
            LastUrl = url;
            CallCount++;
            return Task.FromResult(Result is null
                ? WebContentFetchResult.Fail("no hay contenido")
                : WebContentFetchResult.Ok(Result));
        }
    }

    private sealed class SimpleAiService : IAiService
    {
        public Task<string> AskAsync(string message, List<ChatMessage> history)
            => Task.FromResult("""
            {
              "title": "Examen de prueba",
              "questions": [
                {
                  "prompt": "¿Pregunta 1?",
                  "type": "MultipleChoice",
                  "correctAnswer": "A",
                  "citation": "[Unidad 1]",
                  "options": ["A", "B", "C", "D"]
                }
              ]
            }
            """);
        public void Dispose() { }
    }

    private ExamViewModel CreateVm(MockWebContentReader? webReader = null)
        => new(new SimpleAiService(), webContentReader: webReader ?? new MockWebContentReader());

    [Fact]
    public async Task AddUrlCommand_ValidUrl_AddsToUrls()
    {
        var webReader = new MockWebContentReader { Result = "Contenido de la página web." };
        var vm = CreateVm(webReader);
        vm.UseUrls = true;
        vm.NewUrl = "https://ejemplo.com/articulo";

        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        Assert.Single(vm.Urls);
        Assert.Contains("https://ejemplo.com/articulo", vm.Urls);
        Assert.Null(vm.NewUrl);
        Assert.Null(vm.ErrorMessage);
    }

    [Fact]
    public async Task AddUrlCommand_EmptyUrl_DoesNotAdd()
    {
        var webReader = new MockWebContentReader { Result = "contenido" };
        var vm = CreateVm(webReader);
        vm.NewUrl = "   ";

        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        Assert.Empty(vm.Urls);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Equal(0, webReader.CallCount);
    }

    [Fact]
    public async Task AddUrlCommand_InvalidUrl_DoesNotAdd()
    {
        var webReader = new MockWebContentReader { Result = "contenido" };
        var vm = CreateVm(webReader);
        vm.NewUrl = "ftp://invalido.com";

        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        Assert.Empty(vm.Urls);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("http", vm.ErrorMessage);
        Assert.Equal(0, webReader.CallCount);
    }

    [Fact]
    public async Task AddUrlCommand_DuplicateUrl_DoesNotAdd()
    {
        var webReader = new MockWebContentReader { Result = "contenido" };
        var vm = CreateVm(webReader);
        vm.UseUrls = true;
        vm.NewUrl = "https://ejemplo.com/articulo";
        vm.AddUrlCommand.Execute(null);
        await Task.Yield();
        vm.NewUrl = "https://ejemplo.com/articulo";

        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        Assert.Single(vm.Urls);
        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("ya fue agregada", vm.ErrorMessage);
    }

    [Fact]
    public async Task RemoveUrlCommand_RemovesUrl()
    {
        var webReader = new MockWebContentReader { Result = "contenido" };
        var vm = CreateVm(webReader);
        vm.UseUrls = true;
        vm.NewUrl = "https://ejemplo.com/articulo";
        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        vm.RemoveUrlCommand.Execute("https://ejemplo.com/articulo");

        Assert.Empty(vm.Urls);
    }

    [Fact]
    public void CanGenerate_UseUrlsTrue_NoUrls_ReturnsFalse()
    {
        var vm = CreateVm();
        vm.UseProgramContent = false;
        vm.UseDocuments = false;
        vm.UseUrls = true;

        Assert.False(vm.CanGenerate);
    }

    [Fact]
    public void CanGenerate_UseUrlsTrue_WithUrls_ReturnsTrue()
    {
        var vm = CreateVm();
        vm.UseProgramContent = false;
        vm.UseDocuments = false;
        vm.UseUrls = true;
        vm.Urls.Add("https://ejemplo.com/articulo");

        Assert.True(vm.CanGenerate);
    }

    [Fact]
    public async Task GenerateExamAsync_CallsWebReader_WhenUseUrlsAndUrlsConfigured()
    {
        var webReader = new MockWebContentReader { Result = "Contenido de prueba de la página web para generar examen." };
        var vm = CreateVm(webReader);
        vm.UseProgramContent = true;
        vm.UseDocuments = false;
        vm.UseUrls = true;
        vm.Urls.Add("https://ejemplo.com/pagina");
        vm.QuestionCount = 1;

        vm.GenerateExamCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(1, webReader.CallCount);
        Assert.Equal("https://ejemplo.com/pagina", webReader.LastUrl);
    }

    [Fact]
    public async Task GenerateExamAsync_AccumulatesUrlErrors()
    {
        var webReader = new MockWebContentReader { Result = null };
        var vm = CreateVm(webReader);
        vm.UseProgramContent = false;
        vm.UseDocuments = false;
        vm.UseUrls = true;
        vm.Urls.Add("https://ejemplo.com/fallo1");
        vm.Urls.Add("https://ejemplo.com/fallo2");
        vm.QuestionCount = 1;

        vm.GenerateExamCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(2, webReader.CallCount);
        Assert.NotNull(vm.ErrorMessage);
        // Ambas URLs fallaron y no hay otra fuente: no se generó examen.
        Assert.Null(vm.CurrentExam);
    }
}
