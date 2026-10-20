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

    /*public DocumentListViewModel()
    {
        Documents.Add(new UserDocument { Name = "React Documentation.pdf", Type = "pdf", FileSize = 245000, ContentType = DocumentContentType.Document });
        Documents.Add(new UserDocument { Name = "Design Tokens.fig", Type = "figma", FileSize = 12000, ContentType = DocumentContentType.Document });
        Documents.Add(new UserDocument { Name = "Architecture Overview.md", Type = "md", FileSize = 3400, ContentType = DocumentContentType.Document });
        Documents.Add(new UserDocument { Name = "API Reference.pdf", Type = "pdf", FileSize = 89000, ContentType = DocumentContentType.Document });
        Documents.Add(new UserDocument { Name = "Component Library.zip", Type = "zip", FileSize = 1500000, ContentType = DocumentContentType.Document });
    }
*/
    [RelayCommand]
    private void ShowImportDialog()
    {
        ImportDialogRequested?.Invoke();
    }

    public void AddDocument(UserDocument doc)
    {
        Documents.Insert(0, doc);
        _ = SaveDocumentsAsync();
    }

    [RelayCommand]
    private void RemoveDocument(UserDocument? doc)
    {
        if (doc is not null)
        {
            Documents.Remove(doc);
            _ = SaveDocumentsAsync();
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
                Documents.Add(doc);
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
