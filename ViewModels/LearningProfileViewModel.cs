using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

public partial class LearningProfileViewModel : ViewModelBase
{
    private readonly LearningProfileService _profileService;
    private readonly string _userId;
    private readonly bool _isRequired;

    public LearningProfileViewModel(
        LearningProfileService profileService,
        string userId,
        LearningProfile? existingProfile = null,
        bool isRequired = true)
    {
        _profileService = profileService;
        _userId = userId;
        _isRequired = isRequired;

        if (existingProfile is not null)
            LoadProfile(existingProfile);
        else
            IsStudent = true;
    }

    public bool IsEditing => !_isRequired;
    public bool IsFirstStep => CurrentStep == 1;
    public bool IsDetailsStep => CurrentStep == 2;
    public bool IsPreferencesStep => CurrentStep == 3;
    public bool IsLastStep => CurrentStep == 3;
    public bool IsBackVisible => CurrentStep > 1;
    public string NextButtonText => IsLastStep ? "Guardar perfil" : "Siguiente";
    public string StepLabel => $"Paso {CurrentStep} de 3";
    public string WindowTitle => IsEditing ? "Editar perfil de aprendizaje" : "Configura tu perfil de aprendizaje";
    public string WindowDescription => IsEditing
        ? "Actualiza el contexto que Learn-Assist usa para ayudarte a aprender."
        : "Indica cómo quieres aprender antes de comenzar una conversación.";

    public ObservableCollection<string> Levels { get; } =
        ["Principiante", "Intermedio", "Avanzado"];

    public ObservableCollection<string> Languages { get; } =
        ["Español", "English"];

    [ObservableProperty]
    public partial int CurrentStep { get; set; } = 1;

    [ObservableProperty]
    public partial bool IsStudent { get; set; }

    [ObservableProperty]
    public partial bool IsUniversity { get; set; }

    [ObservableProperty]
    public partial string LearningGoal { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Course { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedLevel { get; set; }

    [ObservableProperty]
    public partial string SubjectsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Career { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string University { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Semester { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SemesterContent { get; set; }

    [ObservableProperty]
    public partial string? SemesterImportFeedback { get; set; }

    [ObservableProperty]
    public partial string? SelectedLanguage { get; set; } = LanguageService.Current;

    [ObservableProperty]
    public partial int SessionMinutes { get; set; } = 15;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    public event Action<LearningProfile>? ProfileSaved;
    public event Action? Cancelled;
    public event Action? SemesterImportRequested;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsDetailsStep));
        OnPropertyChanged(nameof(IsPreferencesStep));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(IsBackVisible));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(StepLabel));
    }

    partial void OnIsStudentChanged(bool value)
    {
        if (value)
            SelectOnly(LearningProfileType.Student);
    }

    partial void OnIsUniversityChanged(bool value)
    {
        if (value)
            SelectOnly(LearningProfileType.University);
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStep > 1)
            CurrentStep--;
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        ErrorMessage = null;

        if (!ValidateCurrentStep())
            return;

        if (!IsLastStep)
        {
            CurrentStep++;
            return;
        }

        await SaveAsync();
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsEditing)
            Cancelled?.Invoke();
    }

    [RelayCommand]
    private void SemesterImport()
    {
        SemesterImportRequested?.Invoke();
    }

    private async Task SaveAsync()
    {
        if (IsSaving)
            return;

        IsSaving = true;
        ErrorMessage = null;

        try
        {
            var profile = BuildProfile();
            await _profileService.SaveAsync(profile);
            ProfileSaved?.Invoke(profile);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo guardar el perfil de aprendizaje: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool ValidateCurrentStep()
    {
        if (CurrentStep == 1 && !IsStudent && !IsUniversity)
        {
            ErrorMessage = "Selecciona el perfil que mejor describa tu contexto de aprendizaje.";
            return false;
        }

        if (CurrentStep == 2)
        {
            if (IsStudent && string.IsNullOrWhiteSpace(Course))
            {
                ErrorMessage = "Introduce el curso o nivel que estás estudiando actualmente.";
                return false;
            }

            if (IsStudent && string.IsNullOrWhiteSpace(SubjectsText))
            {
                ErrorMessage = "Agrega al menos una materia actual.";
                return false;
            }

            if (IsUniversity && string.IsNullOrWhiteSpace(Career))
            {
                ErrorMessage = "Introduce tu carrera universitaria.";
                return false;
            }
        }

        if (CurrentStep == 3)
        {
            if (LearningGoal.Trim().Length < 3)
            {
                ErrorMessage = "Describe qué quieres aprender con al menos 3 caracteres.";
                return false;
            }

            if (SessionMinutes is < 5 or > 120)
            {
                ErrorMessage = "La duración de la sesión debe estar entre 5 y 120 minutos.";
                return false;
            }
        }

        return true;
    }

    private LearningProfile BuildProfile()
    {
        var profileType = IsStudent
            ? LearningProfileType.Student
            : LearningProfileType.University;

        return new LearningProfile
        {
            UserId = _userId,
            ProfileType = profileType,
            LearningGoal = LearningGoal.Trim(),
            Course = Course.Trim(),
            Level = SelectedLevel?.Trim() ?? string.Empty,
            Subjects = SubjectsText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Career = Career.Trim(),
            University = University.Trim(),
            Semester = Semester.Trim(),
            SemesterContent = SemesterContent ?? string.Empty,
            PreferredLanguage = string.Equals(SelectedLanguage, "English", StringComparison.OrdinalIgnoreCase)
                ? "en"
                : "es",
            SessionMinutes = SessionMinutes,
            IsCompleted = true,
        };
    }

    private void LoadProfile(LearningProfile profile)
    {
        LearningGoal = profile.LearningGoal;
        Course = profile.Course;
        SelectedLevel = profile.Level;
        SubjectsText = string.Join(", ", profile.Subjects);
        Career = profile.Career;
        University = profile.University;
        Semester = profile.Semester;
        SemesterContent = profile.SemesterContent;
        SelectedLanguage = profile.PreferredLanguage == "en" ? "English" : "Español";
        SessionMinutes = profile.SessionMinutes;

        switch (profile.ProfileType)
        {
            case LearningProfileType.Student:
                IsStudent = true;
                break;
            default:
                IsUniversity = true;
                break;
        }
    }

    private void SelectOnly(LearningProfileType selected)
    {
        if (selected != LearningProfileType.Student)
            IsStudent = false;
        if (selected != LearningProfileType.University)
            IsUniversity = false;
    }
}
