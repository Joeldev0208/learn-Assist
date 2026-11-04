using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using learn_Assist.Services;
using learn_Assist.ViewModels;

namespace learn_Assist.Views;

public partial class LearningProfileView : Window
{
    private LearningProfileViewModel? _previousVm;

    public LearningProfileView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // Desuscribir el ViewModel anterior evita eventos duplicados y fugas.
        if (_previousVm is not null)
            _previousVm.SemesterImportRequested -= OnSemesterImportRequested;

        if (DataContext is LearningProfileViewModel vm)
        {
            // El code-behind atiende el evento que requiere el selector de archivos.
            vm.SemesterImportRequested += OnSemesterImportRequested;
            _previousVm = vm;
        }
        else
        {
            _previousVm = null;
        }
    }

    private async void OnSemesterImportRequested()
    {
        if (DataContext is not LearningProfileViewModel vm)
            return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import semester",
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Semester files")
                {
                    Patterns = new[] { "*.txt", "*.csv", "*.md", "*.ics", "*.json", "*.pdf" },
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*" },
                },
            },
            AllowMultiple = false,
        });

        if (files.Count == 0)
            return;

        var file = files[0];
        var filePath = FileContentReader.NormalizePath(file.Path.LocalPath);
        var name = Path.GetFileNameWithoutExtension(file.Name);

        // Only set the semester name if the field is empty
        if (string.IsNullOrWhiteSpace(vm.Semester))
            vm.Semester = string.IsNullOrWhiteSpace(name) ? file.Name : name;

        // Extraer el texto puede tardar con PDFs grandes; no bloquear la UI.
        var (extracted, errorReason) = await Task.Run(() =>
        {
            var text = FileContentReader.ExtractText(filePath, out var reason);
            return (text, reason);
        });

        vm.SemesterContent = extracted ?? string.Empty;

        if (string.IsNullOrWhiteSpace(extracted))
        {
            vm.SemesterImportFeedback = errorReason switch
            {
                null => "No se pudo extraer texto del archivo",
                var reason when reason.Contains("Extensión no soportada") => $"Formato no soportado: {Path.GetExtension(filePath)}",
                var reason when reason.Contains("supera el límite") => $"Archivo demasiado grande: {errorReason}",
                var reason when reason.Contains("no contiene texto extractable") => "PDF sin texto (puede ser una imagen escaneada)",
                _ => $"No se pudo extraer contenido: {errorReason ?? "motivo desconocido"}",
            };
        }
        else
        {
            vm.SemesterImportFeedback = $"Contenido extraído ({extracted.Length} caracteres) — disponible para la IA";
        }
    }
}
