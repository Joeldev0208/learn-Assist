using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace learn_Assist.ViewModels;

public record TutorialStep(string Title, string Description);

public partial class TutorialViewModel : ViewModelBase
{
    public TutorialStep[] Steps { get; } =
    [
        new("Welcome to Learn-Assist!",
            "Your AI-powered learning companion. Let's take a quick tour of the interface."),
        new("Chat History",
            "On the left, you'll find all your past conversations. Click any session to pick up where you left off, or start a new one."),
        new("AI Chat",
            "This is the main workspace. Ask questions, get code examples, and learn interactively with the help of AI."),
        new("Resources",
            "Import documents, images, or videos on the right panel. The AI uses these files to provide more personalized answers."),
        new("You're all set!",
            "Start a conversation or import your first resource. Happy learning!"),
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
    public string NextButtonText => IsLastStep ? "Let's start!" : "Next";

    public event Action? TutorialFinished;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(StepDescription));
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(NextButtonText));
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
