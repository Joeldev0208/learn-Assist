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
    private readonly string _tempDir;

    public DocumentListViewModelUrlTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "learn-assist-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // Limpieza best-effort: no debe hacer fallar la prueba.
        }
    }

    private string DocumentsPath => Path.Combine(_tempDir, "documents.json");

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
        => new(webReader ?? new MockWebContentReader(), DocumentsPath);

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
        Assert.NotEqual(DocumentListViewModel.DefaultDocumentsFilePath, DocumentsPath);
        var vm = CreateVm();

        await vm.AddDocumentAsync(new UserDocument
        {
            Name = "libro.txt",
            Type = "file",
            LocalPath = Path.Combine(_tempDir, "libro.txt"),
        });

        Assert.True(File.Exists(DocumentsPath));
        var saved = JsonSerializer.Deserialize<List<UserDocument>>(
            await File.ReadAllTextAsync(DocumentsPath));
        Assert.NotNull(saved);
        Assert.Single(saved!);
        Assert.Equal("libro.txt", saved![0].Name);
    }

    [Fact]
    public async Task LoadDocumentsAsync_ReadsFromInjectedPath()
    {
        var docs = new List<UserDocument>
        {
            new() { Name = "libro.txt", Type = "file", LocalPath = "C:/inexistente/libro.txt" },
        };
        await File.WriteAllTextAsync(DocumentsPath, JsonSerializer.Serialize(docs));

        var vm = CreateVm();
        await vm.LoadDocumentsAsync();

        Assert.Single(vm.Documents);
        Assert.Equal("libro.txt", vm.Documents[0].Name);
    }

    [Fact]
    public async Task LoadDocumentsAsync_RepairsMissingContentFromDisk()
    {
        var libro = Path.Combine(_tempDir, "libro.txt");
        await File.WriteAllTextAsync(libro, "texto de prueba");

        var docs = new List<UserDocument>
        {
            new() { Name = "libro.txt", Type = "file", LocalPath = libro, Content = string.Empty },
        };
        await File.WriteAllTextAsync(DocumentsPath, JsonSerializer.Serialize(docs));

        var vm = CreateVm();
        await vm.LoadDocumentsAsync();

        // La reparación es en segundo plano: se espera sin bloquear la prueba.
        for (var i = 0; i < 100 && string.IsNullOrEmpty(vm.Documents[0].Content); i++)
            await Task.Delay(50);

        Assert.Equal("texto de prueba", vm.Documents[0].Content);

        for (var i = 0; i < 100 && !File.ReadAllText(DocumentsPath).Contains("texto de prueba"); i++)
            await Task.Delay(50);
        Assert.Contains("texto de prueba", await File.ReadAllTextAsync(DocumentsPath));
    }
}
