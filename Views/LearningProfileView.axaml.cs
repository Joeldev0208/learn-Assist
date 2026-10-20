using System;
using System.IO;
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
        var filePath = file.Path.AbsolutePath;
        var name = Path.GetFileNameWithoutExtension(file.Name);

        vm.Semester = string.IsNullOrWhiteSpace(name) ? file.Name : name;
        vm.SemesterContent = FileContentReader.ExtractText(filePath);
        vm.SemesterImportFeedback = vm.SemesterContent is not null
            ? "File processed — content available for AI context"
            : "File name saved — no extractable text found";
    }
}
