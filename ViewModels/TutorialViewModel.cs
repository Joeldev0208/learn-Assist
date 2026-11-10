using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public record TutorialStep(string Title, string Description);

public partial class TutorialViewModel : ViewModelBase
{
    public TutorialStep[] Steps => LanguageService.IsEnglish
        ? [
            new("Welcome to Learn-Assist!", "Your AI-powered learning companion. Let's take a quick tour of the interface."),
            new("Chat History", "On the left, you'll find your past conversations. Select a session to continue or start a new one."),
            new("AI Chat", "This is the main workspace. Ask questions, get code examples, and learn interactively with AI."),
            new("Resources", "Import documents, images, or videos in the right panel. AI uses them to provide more personalized answers."),
            new("You're all set!", "Start a conversation or import your first resource. Happy learning!"),
        ]
        : [
            new("¡Bienvenido a Learn-Assist!", "Tu compañero de aprendizaje con IA. Hagamos un recorrido rápido por la interfaz."),
            new("Historial de chats", "A la izquierda encontrarás tus conversaciones anteriores. Selecciona una sesión para continuar o inicia una nueva."),
            new("Chat con IA", "Este es el espacio de trabajo principal. Haz preguntas, obtén ejemplos de código y aprende de forma interactiva con ayuda de la IA."),
            new("Recursos", "Importa documentos, imágenes o vídeos en el panel derecho. La IA usa estos archivos para ofrecer respuestas más personalizadas."),
            new("¡Todo listo!", "Inicia una conversación o importa tu primer recurso. ¡Feliz aprendizaje!"),
        ];

    [ObservableProperty]
    public partial int CurrentStep { get; set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    public string StepTitle => Steps[CurrentStep].Title;
    public string StepDescription => Steps[CurrentStep].Description;
    public bool IsFirstStep => CurrentStep == 0;
    public bool IsLastStep => CurrentStep == Steps.Length - 1;
    public int TotalSteps => Steps.Length;
    public string NextButtonText => IsLastStep
        ? (LanguageService.IsEnglish ? "Let's start!" : "¡Comencemos!")
        : (LanguageService.IsEnglish ? "Next" : "Siguiente");
    public string StepIndicatorText => LanguageService.IsEnglish
        ? $"Step {CurrentStep + 1} of {TotalSteps}"
        : $"Paso {CurrentStep + 1} de {TotalSteps}";
    public string SkipButtonText => LanguageService.IsEnglish ? "Skip" : "Omitir";
    public string BackButtonText => LanguageService.IsEnglish ? "Back" : "Atrás";

    public void RefreshLanguage()
    {
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(StepDescription));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(StepIndicatorText));
        OnPropertyChanged(nameof(SkipButtonText));
        OnPropertyChanged(nameof(BackButtonText));
    }

    public event Action? TutorialFinished;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(StepDescription));
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(StepIndicatorText));
    }

    [RelayCommand]
    private void Next()
    {
        if (IsLastStep)
        {
            Finish();
            return;
        }
        CurrentStep++;
    }

    [RelayCommand]
    private void Back()
    {
        if (!IsFirstStep)
            CurrentStep--;
    }

    [RelayCommand]
    private void Skip()
    {
        Finish();
    }

    private void Finish()
    {
        IsVisible = false;
        TutorialFinished?.Invoke();
    }
}
