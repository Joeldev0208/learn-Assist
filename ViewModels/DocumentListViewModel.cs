using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class DocumentListViewModel : ViewModelBase
{
    private static readonly string DocumentsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "learn-assist",
        "documents.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public ObservableCollection<UserDocument> Documents { get; } = [];

    public event Action? ImportDialogRequested;

    private readonly IWebContentReader _webContentReader;

    /// <summary>
    /// URL nueva que el usuario quiere agregar como fuente de recurso.
    /// </summary>
    [ObservableProperty]
    public partial string? NewUrl { get; set; }

    [ObservableProperty]
    public partial string? ImportError { get; set; }

    [ObservableProperty]
    public partial bool IsImporting { get; set; }

    public DocumentListViewModel(IWebContentReader? webContentReader = null)
    {
        _webContentReader = webContentReader ?? WebContentReader.Instance;
    }

    [RelayCommand]
    private void ShowImportDialog()
    {
        ImportDialogRequested?.Invoke();
    }

    [RelayCommand]
    public async Task AddUrlAsync()
    {
        ImportError = null;
        IsImporting = true;

        try
        {
            var url = NewUrl?.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                ImportError = "Ingrese una URL válida.";
                return;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ImportError = "La URL debe ser una dirección http:// o https:// válida.";
                NewUrl = null;
                return;
            }

            // Para documentos web, Name es "[Web] {url}" y nunca coincidirá con la URL cruda;
            // solo LocalPath es eficaz para detectar duplicados.
            if (Documents.Any(d => d.LocalPath == url))
            {
                ImportError = "Esta URL ya fue agregada.";
                NewUrl = null;
                return;
            }

            var result = await _webContentReader.FetchAsync(uri.ToString());
            if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
            {
                ImportError = result.Error ?? $"No se pudo extraer contenido de: {url}";
                NewUrl = null;
                return;
            }

            var doc = new UserDocument
            {
                Id = Guid.NewGuid(),
                Name = $"[Web] {url}",
                Type = "web",
                Content = result.Text,
                LocalPath = url,
                ContentType = DocumentContentType.Document,
                FileSize = result.Text.Length,
                ImportedAt = DateTime.Now
            };

            Documents.Insert(0, doc);
            await SaveDocumentsAsync();
            NewUrl = null;
            ImportError = null;
        }
        catch (Exception ex)
        {
            // Solo captura errores de persistencia (SaveDocumentsAsync); FetchAsync nunca lanza.
            ImportError = $"Error al importar: {ex.Message}";
        }
        finally
        {
            IsImporting = false;
        }
    }

    [RelayCommand]
    public async Task AddDocumentAsync(UserDocument doc)
    {
        Documents.Insert(0, doc);
        await SaveDocumentsAsync();
    }

    [RelayCommand]
    private async Task RemoveDocumentAsync(UserDocument? doc)
    {
        if (doc is not null)
        {
            Documents.Remove(doc);
            await SaveDocumentsAsync();
        }
    }

    public async Task LoadDocumentsAsync()
    {
        try
        {
            if (!File.Exists(DocumentsFilePath))
                return;

            var json = await File.ReadAllTextAsync(DocumentsFilePath);
            var docs = JsonSerializer.Deserialize<List<UserDocument>>(json, JsonOptions);
            if (docs is null)
                return;

            Documents.Clear();
            foreach (var doc in docs)
            {
                // Re-extraer contenido si el documento tipo texto lo perdió en la serialización
                if (doc.ContentType == DocumentContentType.Document
                    && string.IsNullOrWhiteSpace(doc.Content)
                    && !string.IsNullOrEmpty(doc.LocalPath)
                    && File.Exists(doc.LocalPath))
                {
                    doc.Content = FileContentReader.ExtractText(doc.LocalPath);
                }

                Documents.Add(doc);
            }
        }
        catch
        {
            // Corrupted or unreadable file — start fresh
        }
    }

    private async Task SaveDocumentsAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(DocumentsFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(Documents.ToList(), JsonOptions);
            var tempPath = DocumentsFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, DocumentsFilePath, overwrite: true);
        }
        catch
        {
            // Persistence must never break the app
        }
    }
}