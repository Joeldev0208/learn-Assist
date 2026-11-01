using System.Collections.Generic;
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

        public Task<string?> FetchTextAsync(string url)
        {
            LastUrl = url;
            CallCount++;
            return Task.FromResult(Result);
        }
    }

    private sealed class SimpleAiService : IAiService
    {
        public Task<string> AskAsync(string message, List<ChatMessage> history)
            => Task.FromResult("respuesta");
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
        // UseProgramContent=true (aunque no haya SemesterContent) para que el código
        // llegue a la descarga de URLs antes de validar el programa.
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
}
