using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class DocumentViewerViewModel : ViewModelBase
{
    private readonly ObservableCollection<string> _pages = [];

    public ReadOnlyObservableCollection<string> Pages { get; }
    public event Action? Closed;

    [ObservableProperty]
    public partial UserDocument? Document { get; set; }

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial string CurrentPageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PageIndicator { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial Uri PdfUri { get; set; } = new("about:blank");

    [ObservableProperty]
    public partial bool IsPdf { get; set; }

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    public bool HasPages => _pages.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool CanGoPrevious => CurrentPage > 1;
    public bool CanGoNext => CurrentPage < _pages.Count;

    public DocumentViewerViewModel()
    {
        Pages = new ReadOnlyObservableCollection<string>(_pages);
    }

    public async Task OpenAsync(UserDocument document)
    {
        _pages.Clear();
        Document = document;
        CurrentPage = 1;
        CurrentPageText = string.Empty;
        PageIndicator = string.Empty;
        ErrorMessage = null;
        IsPdf = false;
        PdfUri = new Uri("about:blank");
        IsOpen = true;
        NotifyStateChanged();

        var path = document.FilePath ?? document.LocalPath;
        if (!string.Equals(document.Type, "web", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(path)
            && string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            var normalizedPath = FileContentReader.NormalizePath(path);
            if (!File.Exists(normalizedPath))
            {
                ErrorMessage = "El archivo PDF ya no existe en la ruta guardada.";
                NotifyStateChanged();
                return;
            }

            PdfUri = new Uri(Path.GetFullPath(normalizedPath));
            IsPdf = true;
            NotifyStateChanged();
            return;
        }

        if (string.Equals(document.Type, "web", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(path)
            || !string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(document.Content))
                _pages.Add(document.Content);
            else
                ErrorMessage = "Este recurso no contiene un PDF ni texto disponible para mostrar.";

            UpdatePage();
            return;
        }

        var result = await Task.Run(() => FileContentReader.ExtractPdfPages(path));
        if (!result.Success)
        {
            ErrorMessage = result.Error;
            NotifyStateChanged();
            return;
        }

        foreach (var page in result.Pages)
            _pages.Add(page);

        UpdatePage();
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CanGoPrevious)
        {
            CurrentPage--;
            UpdatePage();
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CanGoNext)
        {
            CurrentPage++;
            UpdatePage();
        }
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        Closed?.Invoke();
    }

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
    }

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    private void UpdatePage()
    {
        CurrentPageText = _pages.Count == 0
            ? string.Empty
            : _pages[CurrentPage - 1];
        PageIndicator = _pages.Count == 0
            ? string.Empty
            : $"Página {CurrentPage} de {_pages.Count}";
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(HasPages));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
    }
}
