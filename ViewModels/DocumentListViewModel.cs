using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    /// <summary>
    /// Ruta real de la biblioteca del usuario. Los tests deben inyectar una ruta
    /// temporal en el constructor para no sobrescribirla.
    /// </summary>
    public static readonly string DefaultDocumentsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "learn-assist",
        "documents.json");

    private readonly string _documentsFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public ObservableCollection<UserDocument> Documents { get; } = [];

    public event Action? ImportDialogRequested;

    /// <summary>
    /// Se dispara cuando se recuperó contenido de documentos que se habían guardado sin texto.
    /// MainViewModel lo usa para reconstruir el system prompt.
    /// </summary>
    public event Action? ContentRepaired;
    public event Action? ContextSelectionChanged;

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

    public DocumentListViewModel(
        IWebContentReader? webContentReader = null,
        string? documentsFilePath = null)
    {
        _webContentReader = webContentReader ?? WebContentReader.Instance;
        _documentsFilePath = string.IsNullOrWhiteSpace(documentsFilePath)
            ? DefaultDocumentsFilePath
            : documentsFilePath;
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

            TrackDocument(doc);
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
        TrackDocument(doc);
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
            if (!File.Exists(_documentsFilePath))
                return;

            var json = await File.ReadAllTextAsync(_documentsFilePath);
            var docs = JsonSerializer.Deserialize<List<UserDocument>>(json, JsonOptions);
            if (docs is null)
                return;

            Documents.Clear();
            var pending = new List<UserDocument>();

            foreach (var doc in docs)
            {
                NormalizeDocumentPaths(doc);
                TrackDocument(doc);

                // Re-extraer contenido si el documento lo perdió en la serialización.
                // Se hace fuera del hilo de UI y en segundo plano: un libro de cientos
                // de páginas no debe bloquear el arranque de la aplicación.
                if (doc.ContentType == DocumentContentType.Document
                    && string.IsNullOrWhiteSpace(doc.Content)
                    && !string.IsNullOrEmpty(doc.LocalPath))
                {
                    pending.Add(doc);
                }

                Documents.Add(doc);
            }

            if (pending.Count > 0)
                _ = RepairMissingContentAsync(pending);
        }
        catch
        {
            // Corrupted or unreadable file — start fresh
        }
    }

    private void TrackDocument(UserDocument doc)
    {
        doc.PropertyChanged -= OnDocumentPropertyChanged;
        doc.PropertyChanged += OnDocumentPropertyChanged;
    }

    private async void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(UserDocument.IsIncludedInContext))
            return;

        await SaveDocumentsAsync();
        ContextSelectionChanged?.Invoke();
    }

    /// <summary>
    /// Corrige rutas guardadas como URI con escapes (%20, %C3%AD…). Las URLs web
    /// se dejan intactas: decodificarlas cambiaría su significado.
    /// </summary>
    private static void NormalizeDocumentPaths(UserDocument doc)
    {
        if (string.Equals(doc.Type, "web", StringComparison.OrdinalIgnoreCase))
            return;

        if (!string.IsNullOrEmpty(doc.LocalPath))
            doc.LocalPath = FileContentReader.NormalizePath(doc.LocalPath);

        if (!string.IsNullOrEmpty(doc.FilePath))
            doc.FilePath = FileContentReader.NormalizePath(doc.FilePath);
    }

    private async Task RepairMissingContentAsync(List<UserDocument> pending)
    {
        try
        {
            var changed = false;

            foreach (var doc in pending)
            {
                var path = doc.LocalPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;

                var extracted = await Task.Run(() => FileContentReader.ExtractText(path));
                if (string.IsNullOrWhiteSpace(extracted))
                    continue;

                doc.Content = extracted;
                if (doc.FileSize <= 0)
                    doc.FileSize = new FileInfo(path).Length;
                changed = true;
            }

            if (!changed)
                return;

            await SaveDocumentsAsync();
            ContentRepaired?.Invoke();
        }
        catch
        {
            // Recuperar contenido es best-effort; nunca debe romper la app
        }
    }

    private async Task SaveDocumentsAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_documentsFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(Documents.ToList(), JsonOptions);
            var tempPath = _documentsFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _documentsFilePath, overwrite: true);
        }
        catch
        {
            // Persistence must never break the app
        }
    }
}