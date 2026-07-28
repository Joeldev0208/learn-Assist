using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;

namespace learn_Assist.ViewModels;

public partial class DocumentListViewModel : ViewModelBase
{
    public ObservableCollection<UserDocument> Documents { get; } = [];

    public event Action? ImportDialogRequested;

    public DocumentListViewModel()
    {
        Documents.Add(new UserDocument { Name = "React Documentation.pdf", Type = "pdf", FileSize = 245000, ContentType = DocumentContentType.Document });
        Documents.Add(new UserDocument { Name = "Design Tokens.fig", Type = "figma", FileSize = 12000, ContentType = DocumentContentType.Document });
        Documents.Add(new UserDocument { Name = "Architecture Overview.md", Type = "md", FileSize = 3400, ContentType = DocumentContentType.Document });
        Documents.Add(new UserDocument { Name = "API Reference.pdf", Type = "pdf", FileSize = 89000, ContentType = DocumentContentType.Document });
        Documents.Add(new UserDocument { Name = "Component Library.zip", Type = "zip", FileSize = 1500000, ContentType = DocumentContentType.Document });
    }

    [RelayCommand]
    private void ShowImportDialog()
    {
        ImportDialogRequested?.Invoke();
    }

    public void AddDocument(UserDocument doc)
    {
        Documents.Insert(0, doc);
    }

    [RelayCommand]
    private void RemoveDocument(UserDocument? doc)
    {
        if (doc is not null)
            Documents.Remove(doc);
    }
}
