using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;

namespace learn_Assist.ViewModels;

public partial class ImportDocumentViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string SelectedTypeName { get; set; } = string.Empty;

    public event Action<DocumentContentType>? ImportRequested;
    public event Action? CancelRequested;

    [RelayCommand]
    private void SelectDocument()
    {
        SelectedTypeName = "Document";
        ImportRequested?.Invoke(DocumentContentType.Document);
    }

    [RelayCommand]
    private void SelectImage()
    {
        SelectedTypeName = "Image";
        ImportRequested?.Invoke(DocumentContentType.Image);
    }

    [RelayCommand]
    private void SelectVideo()
    {
        SelectedTypeName = "Video";
        ImportRequested?.Invoke(DocumentContentType.Video);
    }

    [RelayCommand]
    private void Cancel()
    {
        CancelRequested?.Invoke();
    }
}
