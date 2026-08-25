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

    public event Action<UserDocument>? AttachRequested;

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

    [RelayCommand]
    private void AttachToChat(UserDocument? doc)
    {
        if (doc is not null)
            AttachRequested?.Invoke(doc);
    }
}
