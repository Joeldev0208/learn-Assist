using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

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

    private bool IsEnglish => LanguageService.IsEnglish;

    public string WindowTitle => IsEnglish ? "Import Resource" : "Importar recurso";
    public string IntroText => IsEnglish ? "Select the type of file you want to import" : "Selecciona el tipo de archivo que quieres importar";
    public string DocumentButtonContent => IsEnglish ? "Document" : "Documento";
    public string ImageButtonContent => IsEnglish ? "Image" : "Imagen";
    public string VideoButtonContent => IsEnglish ? "Video" : "Vídeo";
    public string UrlButtonContent => IsEnglish ? "URL" : "URL";
    public string CancelButtonContent => IsEnglish ? "Cancel" : "Cancelar";

    public event Action<DocumentContentType>? ImportRequested;
    public event Action<WebsiteContentType>? UrlImportRequested;
    public event Action? CancelRequested;

    public ImportDocumentViewModel()
    {
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

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

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(IntroText));
        OnPropertyChanged(nameof(DocumentButtonContent));
        OnPropertyChanged(nameof(ImageButtonContent));
        OnPropertyChanged(nameof(VideoButtonContent));
        OnPropertyChanged(nameof(UrlButtonContent));
        OnPropertyChanged(nameof(CancelButtonContent));
    }
}
