using System.Collections.Generic;
using System.Threading.Tasks;
using learn_Assist.Models;
using learn_Assist.Services;
using learn_Assist.ViewModels;
using Xunit;

namespace learn_assist.Tests;

public class DocumentListViewModelUrlTests
{
    private sealed class MockWebContentReader : IWebContentReader
    {
        public string? Result { get; set; }
        public string? LastUrl { get; private set; }
        public int CallCount { get; private set; }
        public bool ThrowException { get; set; }

        public Task<string?> FetchTextAsync(string url)
        {
            LastUrl = url;
            CallCount++;
            if (ThrowException)
                throw new System.Net.Http.HttpRequestException("error de red");
            return Task.FromResult(Result);
        }
    }

    private DocumentListViewModel CreateVm(MockWebContentReader? webReader = null)
        => new(webReader ?? new MockWebContentReader());

    [Fact]
    public async Task AddUrlAsync_ValidUrl_AddsDocument()
    {
        var webReader = new MockWebContentReader { Result = "Contenido extraído de la web." };
        var vm = CreateVm(webReader);
        vm.NewUrl = "https://ejemplo.com/articulo";

        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        Assert.Single(vm.Documents);
        var doc = vm.Documents[0];
        Assert.Equal("[Web] https://ejemplo.com/articulo", doc.Name);
        Assert.Equal("Contenido extraído de la web.", doc.Content);
        Assert.Equal("https://ejemplo.com/articulo", doc.LocalPath);
        Assert.Null(vm.NewUrl);
        Assert.Equal(1, webReader.CallCount);
    }

    [Fact]
    public async Task AddUrlAsync_EmptyUrl_DoesNotAdd()
    {
        var webReader = new MockWebContentReader { Result = "contenido" };
        var vm = CreateVm(webReader);
        vm.NewUrl = "   ";

        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        Assert.Empty(vm.Documents);
        Assert.Equal(0, webReader.CallCount);
    }

    [Fact]
    public async Task AddUrlAsync_InvalidUrl_DoesNotAdd()
    {
        var webReader = new MockWebContentReader { Result = "contenido" };
        var vm = CreateVm(webReader);
        vm.NewUrl = "ftp://invalido.com";

        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        Assert.Empty(vm.Documents);
        Assert.Equal(0, webReader.CallCount);
    }

    [Fact]
    public async Task AddUrlAsync_DuplicateUrl_DoesNotAdd()
    {
        var webReader = new MockWebContentReader { Result = "contenido" };
        var vm = CreateVm(webReader);
        vm.NewUrl = "https://ejemplo.com/articulo";
        vm.AddUrlCommand.Execute(null);
        await Task.Yield();
        vm.NewUrl = "https://ejemplo.com/articulo";

        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        Assert.Single(vm.Documents);
        Assert.Equal(1, webReader.CallCount);
    }

    [Fact]
    public async Task AddUrlAsync_WebReaderReturnsNull_DoesNotAdd()
    {
        var webReader = new MockWebContentReader { Result = null };
        var vm = CreateVm(webReader);
        vm.NewUrl = "https://ejemplo.com/articulo";

        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        Assert.Empty(vm.Documents);
        Assert.Equal(1, webReader.CallCount);
    }

    [Fact]
    public async Task AddUrlAsync_WebReaderThrows_DoesNotAdd()
    {
        var webReader = new MockWebContentReader { ThrowException = true };
        var vm = CreateVm(webReader);
        vm.NewUrl = "https://ejemplo.com/articulo";

        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        Assert.Empty(vm.Documents);
        Assert.Equal(1, webReader.CallCount);
    }
}
