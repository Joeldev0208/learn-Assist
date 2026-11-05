using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using learn_Assist.Models;
using learn_Assist.Services;
using learn_Assist.ViewModels;
using Xunit;

namespace learn_assist.Tests;

public class DocumentListViewModelUrlTests : IDisposable
{
    private readonly string _tempDirectory;

    public DocumentListViewModelUrlTests()
    {
        // Nunca usar la biblioteca real del usuario: cada prueba escribe en su propia carpeta.
        _tempDirectory = Path.Combine(
            Path.GetTempPath(), "learn-assist-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // La limpieza no debe hacer fallar la prueba.
        }
    }

    private string DocumentsFilePath => Path.Combine(_tempDirectory, "documents.json");

    private sealed class MockWebContentReader : IWebContentReader
    {
        public string? Result { get; set; }
        public string? LastUrl { get; private set; }
        public int CallCount { get; private set; }
        public bool ThrowException { get; set; }

        public Task<WebContentFetchResult> FetchAsync(string url, CancellationToken cancellationToken = default)
        {
            LastUrl = url;
            CallCount++;
            if (ThrowException)
                return Task.FromResult(WebContentFetchResult.Fail("error de red"));
            return Task.FromResult(Result is null
                ? WebContentFetchResult.Fail("no hay contenido")
                : WebContentFetchResult.Ok(Result));
        }
    }

    private DocumentListViewModel CreateVm(MockWebContentReader? webReader = null)
        => new(webReader ?? new MockWebContentReader(), DocumentsFilePath);

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
    public async Task AddUrlAsync_WebReaderReturnsFail_DoesNotAdd()
    {
        var webReader = new MockWebContentReader { Result = null };
        var vm = CreateVm(webReader);
        vm.NewUrl = "https://ejemplo.com/articulo";

        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        Assert.Empty(vm.Documents);
        Assert.Equal(1, webReader.CallCount);
        Assert.NotNull(vm.ImportError);
    }

    [Fact]
    public async Task AddUrlAsync_WebReaderReturnsFailResult_DoesNotAdd()
    {
        var webReader = new MockWebContentReader { ThrowException = true };
        var vm = CreateVm(webReader);
        vm.NewUrl = "https://ejemplo.com/articulo";

        vm.AddUrlCommand.Execute(null);
        await Task.Yield();

        Assert.Empty(vm.Documents);
        Assert.Equal(1, webReader.CallCount);
        Assert.NotNull(vm.ImportError);
    }

    [Fact]
    public async Task AddDocumentAsync_WritesOnlyToInjectedPath()
    {
        var vm = CreateVm();

        await vm.AddDocumentAsync(new UserDocument
        {
            Name = "libro.pdf",
            Type = "document",
            LocalPath = "/tmp/libro.pdf",
            Content = "texto",
        });

        Assert.True(File.Exists(DocumentsFilePath));
        var json = await File.ReadAllTextAsync(DocumentsFilePath);
        Assert.Contains("libro.pdf", json);
    }

    [Fact]
    public async Task LoadDocumentsAsync_ReadsFromInjectedPath()
    {
        var stored = new List<UserDocument>
        {
            new()
            {
                Name = "libro.pdf",
                Type = "document",
                LocalPath = "/tmp/libro.pdf",
                Content = "texto guardado",
            },
        };
        await File.WriteAllTextAsync(DocumentsFilePath, JsonSerializer.Serialize(stored));

        var vm = CreateVm();
        await vm.LoadDocumentsAsync();

        var doc = Assert.Single(vm.Documents);
        Assert.Equal("libro.pdf", doc.Name);
        Assert.Equal("texto guardado", doc.Content);
    }

    [Fact]
    public async Task LoadDocumentsAsync_RepairsMissingContentFromDisk()
    {
        var sourcePath = Path.Combine(_tempDirectory, "apunte.txt");
        await File.WriteAllTextAsync(sourcePath, "contenido recuperado");

        var stored = new List<UserDocument>
        {
            new()
            {
                Name = "apunte.txt",
                Type = "document",
                LocalPath = sourcePath,
                Content = string.Empty,
            },
        };
        await File.WriteAllTextAsync(DocumentsFilePath, JsonSerializer.Serialize(stored));

        var vm = CreateVm();
        var repaired = false;
        vm.ContentRepaired += () => repaired = true;

        await vm.LoadDocumentsAsync();

        var doc = Assert.Single(vm.Documents);
        for (var i = 0; i < 50 && !repaired; i++)
            await Task.Delay(50);

        Assert.True(repaired);
        Assert.Equal("contenido recuperado", doc.Content);
    }
}
