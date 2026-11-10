using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using learn_Assist.Models;
using learn_Assist.Services;
using learn_Assist.ViewModels;

namespace learn_Assist.Views;

public partial class ImportDocumentView : Window
{
    private ImportDocumentViewModel? _previousVm;

    public UserDocument? Result { get; private set; }

    public ImportDocumentView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_previousVm is not null)
        {
            _previousVm.ImportRequested -= OnImportRequested;
            _previousVm.CancelRequested -= OnCancel;
            _previousVm.UrlImportRequested -= OnUrlImportRequested;
        }

        if (DataContext is ImportDocumentViewModel vm)
        {
            vm.ImportRequested += OnImportRequested;
            vm.CancelRequested += OnCancel;
            vm.UrlImportRequested += OnUrlImportRequested;
            _previousVm = vm;
        }
        else
        {
            _previousVm = null;
        }
    }

    private void OnCancel()
    {
        Close();
    }

    /// <summary>
    /// Maneja la petici\u00f3n de importar URL desde el ViewModel.
    /// El ViewModel ya activ\u00f3 IsUrlInputVisible y mostr\u00f3 el panel de entrada.
    /// </summary>
    private void OnUrlImportRequested(WebsiteContentType type)
    {
        // Nada m\u00e1s que hacer aqu\u00ed; el usuario escribe la URL y pulsa "Importar URL".
    }

    private async void OnImportRequested(DocumentContentType type)
    {
        if (DataContext is not ImportDocumentViewModel vm)
            return;

        var filters = GetFileFilters(type);
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Import {type}",
            FileTypeFilter = filters,
            AllowMultiple = false,
        });

        if (files.Count == 0)
            return;

        var file = files[0];
        // Uri.LocalPath / AbsolutePath pueden dejar la ruta con %20 u otros escapes,
        // y File.Exists falla con ellos. NormalizePath la deja lista para el sistema de archivos.
        var filePath = FileContentReader.NormalizePath(file.Path.LocalPath);
        var fileInfo = new FileInfo(filePath);

        var doc = new UserDocument
        {
            Name = file.Name,
            Type = type.ToString().ToLower(),
            FilePath = filePath,
            LocalPath = filePath,
            FileSize = fileInfo.Exists ? fileInfo.Length : 0,
            ContentType = type,
            ImportedAt = DateTime.Now,
        };

        if (type == DocumentContentType.Document)
        {
            // Extraer un PDF grande puede tardar; hacerlo fuera del hilo de UI evita
            // que la ventana se congele mientras se procesa el archivo.
            vm.ImportResult = "Extrayendo contenido…";
            var (extracted, errorReason) = await Task.Run(() =>
            {
                var text = FileContentReader.ExtractText(filePath, out var reason);
                return (text, reason);
            });

            doc.Content = string.IsNullOrWhiteSpace(extracted) ? string.Empty : extracted;
            vm.ImportResult = string.IsNullOrWhiteSpace(extracted)
                ? $"No se pudo extraer texto del archivo: {errorReason ?? "motivo desconocido"}"
                : $"Contenido extraído ({extracted.Length} caracteres)";
        }
        else
        {
            vm.ImportResult = "Archivo importado correctamente";
        }

        Result = doc;
        Close(doc);
    }

    private async void ImportUrl_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ImportDocumentViewModel vm)
            return;

        var url = vm.NewUrl?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            vm.ImportResult = "Ingrese una URL válida.";
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            vm.ImportResult = "La URL debe ser una dirección http:// o https:// válida.";
            return;
        }

        var result = await WebContentReader.Instance.FetchAsync(uri.ToString());
        if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
        {
            vm.ImportResult = result.Error ?? $"No se pudo extraer contenido de: {url}";
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
            ImportedAt = DateTime.Now,
        };

        vm.ImportResult = $"Contenido extraído ({result.Text.Length} caracteres)";
        Result = doc;
        Close(doc);
    }

    private static IReadOnlyList<FilePickerFileType> GetFileFilters(DocumentContentType type)
    {
        return type switch
        {
            DocumentContentType.Document => new[]
            {
                new FilePickerFileType("Documentos")
                {
                    Patterns = new[] { "*.pdf", "*.doc", "*.docx", "*.txt", "*.md", "*.xls", "*.xlsx", "*.ppt", "*.pptx" },
                },
                new FilePickerFileType("Todos los archivos")
                {
                    Patterns = new[] { "*" },
                },
            },
            DocumentContentType.Image => new[]
            {
                new FilePickerFileType("Imágenes")
                {
                    Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp", "*.svg", "*.webp" },
                },
                new FilePickerFileType("Todos los archivos")
                {
                    Patterns = new[] { "*" },
                },
            },
            DocumentContentType.Video => new[]
            {
                new FilePickerFileType("Vídeos")
                {
                    Patterns = new[] { "*.mp4", "*.avi", "*.mkv", "*.mov", "*.wmv", "*.webm" },
                },
                new FilePickerFileType("Todos los archivos")
                {
                    Patterns = new[] { "*" },
                },
            },
            _ => new[]
            {
                new FilePickerFileType("Todos los archivos")
                {
                    Patterns = new[] { "*" },
                },
            },
        };
    }
}
