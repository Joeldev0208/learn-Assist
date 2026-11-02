using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;

namespace learn_Assist.ViewModels;

public partial class ImportDocumentViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string SelectedTypeName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ImportResult { get; set; } = string.Empty;

    /// <summary>
    /// URL que el usuario quiere importar desde una página web.
    /// </summary>
    [ObservableProperty]
    public partial string? NewUrl { get; set; }

    /// <summary>
    /// Indica si se muestra el panel de entrada de URL (tras seleccionar la opci\u00f3n URL).
    /// </summary>
    [ObservableProperty]
    public partial bool IsUrlInputVisible { get; set; }

    public event Action<DocumentContentType>? ImportRequested;
    public event Action<WebsiteContentType>? UrlImportRequested;
    public event Action? CancelRequested;

    [RelayCommand]
    private void SelectDocument()
    {
        SelectedTypeName = "Document";
        IsUrlInputVisible = false;
        ImportRequested?.Invoke(DocumentContentType.Document);
    }

    [RelayCommand]
    private void SelectImage()
    {
        SelectedTypeName = "Image";
        IsUrlInputVisible = false;
        ImportRequested?.Invoke(DocumentContentType.Image);
    }

    [RelayCommand]
    private void SelectVideo()
    {
        SelectedTypeName = "Video";
        IsUrlInputVisible = false;
        ImportRequested?.Invoke(DocumentContentType.Video);
    }

    [RelayCommand]
    private void SelectUrl()
    {
        SelectedTypeName = "URL";
        IsUrlInputVisible = true;
        NewUrl = null;
        ImportResult = string.Empty;
        UrlImportRequested?.Invoke(WebsiteContentType.Url);
    }

    [RelayCommand]
    private void Cancel()
    {
        CancelRequested?.Invoke();
    }
}
