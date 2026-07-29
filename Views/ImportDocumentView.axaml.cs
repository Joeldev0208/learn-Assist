using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using learn_Assist.Models;
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
        }

        if (DataContext is ImportDocumentViewModel vm)
        {
            vm.ImportRequested += OnImportRequested;
            vm.CancelRequested += OnCancel;
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
        {
            Close();
            return;
        }

        var file = files[0];
        var filePath = file.Path.AbsolutePath;
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

        Result = doc;
        Close(doc);
    }

    private static System.Collections.Generic.IReadOnlyList<FilePickerFileType> GetFileFilters(DocumentContentType type)
    {
        return type switch
        {
            DocumentContentType.Document => new[]
            {
                new FilePickerFileType("Documents")
                {
                    Patterns = new[] { "*.pdf", "*.doc", "*.docx", "*.txt", "*.md", "*.xls", "*.xlsx", "*.ppt", "*.pptx" },
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*" },
                },
            },
            DocumentContentType.Image => new[]
            {
                new FilePickerFileType("Images")
                {
                    Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp", "*.svg", "*.webp" },
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*" },
                },
            },
            DocumentContentType.Video => new[]
            {
                new FilePickerFileType("Videos")
                {
                    Patterns = new[] { "*.mp4", "*.avi", "*.mkv", "*.mov", "*.wmv", "*.webm" },
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*" },
                },
            },
            _ => new[]
            {
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*" },
                },
            },
        };
    }
}
