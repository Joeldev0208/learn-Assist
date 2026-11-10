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

        LanguageService.LanguageChanged += OnLanguageChanged;
        PopulateLevels();

        if (existingProfile is not null)
            LoadProfile(existingProfile);
        else
            IsStudent = true;
    }

    private void PopulateLevels()
    {
        Levels.Clear();
        Levels.Add(IsEnglish ? "Beginner" : "Principiante");
        Levels.Add(IsEnglish ? "Intermediate" : "Intermedio");
        Levels.Add(IsEnglish ? "Advanced" : "Avanzado");
    }

    private void OnLanguageChanged()
    {
        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        PopulateLevels();
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(StepLabel));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(WindowDescription));
        OnPropertyChanged(nameof(ProfileTypeLabel));
        OnPropertyChanged(nameof(ProfileTypeDescription));
        OnPropertyChanged(nameof(StudentRadioContent));
        OnPropertyChanged(nameof(UniversityRadioContent));
        OnPropertyChanged(nameof(DetailsSectionTitle));
        OnPropertyChanged(nameof(CourseLabel));
        OnPropertyChanged(nameof(CoursePlaceholder));
        OnPropertyChanged(nameof(SubjectsLabel));
        OnPropertyChanged(nameof(SubjectsPlaceholder));
        OnPropertyChanged(nameof(CareerLabel));
        OnPropertyChanged(nameof(UniversityLabel));
        OnPropertyChanged(nameof(UniversityPlaceholder));
        OnPropertyChanged(nameof(SemesterLabel));
        OnPropertyChanged(nameof(ImportButtonContent));
        OnPropertyChanged(nameof(SemesterLaterHint));
        OnPropertyChanged(nameof(PreferencesSectionTitle));
        OnPropertyChanged(nameof(LearningGoalQuestion));
        OnPropertyChanged(nameof(LearningGoalPlaceholder));
        OnPropertyChanged(nameof(LevelLabel));
        OnPropertyChanged(nameof(LevelPlaceholder));
        OnPropertyChanged(nameof(PreferredLanguageLabel));
        OnPropertyChanged(nameof(CancelButtonContent));
        OnPropertyChanged(nameof(BackButtonContent));
    }

    public bool IsEditing => !_isRequired;
    public bool IsFirstStep => CurrentStep == 1;
    public bool IsDetailsStep => CurrentStep == 2;
    public bool IsPreferencesStep => CurrentStep == 3;
    public bool IsLastStep => CurrentStep == 3;
    public bool IsBackVisible => CurrentStep > 1;

    private bool IsEnglish => LanguageService.IsEnglish;

    public string NextButtonText => IsLastStep
        ? (IsEnglish ? "Save profile" : "Guardar perfil")
        : (IsEnglish ? "Next" : "Siguiente");
    public string StepLabel => IsEnglish
        ? $"Step {CurrentStep} of 3"
        : $"Paso {CurrentStep} de 3";
    public string WindowTitle => IsEditing
        ? (IsEnglish ? "Edit learning profile" : "Editar perfil de aprendizaje")
        : (IsEnglish ? "Configure your learning profile" : "Configura tu perfil de aprendizaje");
    public string WindowDescription => IsEditing
        ? (IsEnglish
            ? "Update the context Learn-Assist uses to help you learn."
            : "Actualiza el contexto que Learn-Assist usa para ayudarte a aprender.")
        : (IsEnglish
            ? "Indicate how you want to learn before starting a conversation."
            : "Indica cómo quieres aprender antes de comenzar una conversación.");

    public ObservableCollection<string> Levels { get; } = [];

    public ObservableCollection<string> Languages { get; } =
        ["Español", "English"];

    public string ProfileTypeLabel => IsEnglish ? "Learning profile" : "Perfil de aprendizaje";
    public string ProfileTypeDescription => IsEnglish
        ? "This choice determines the initial context the learning assistant will use."
        : "Esta elección determina el contexto inicial que usará el asistente de aprendizaje.";
    public string StudentRadioContent => IsEnglish ? "Student" : "Estudiante";
    public string UniversityRadioContent => IsEnglish ? "University student" : "Estudiante universitario";
    public string DetailsSectionTitle => IsEnglish ? "Tell us a bit more" : "Cuéntanos un poco más";
    public string CourseLabel => IsEnglish ? "Current course or level" : "Curso o nivel actual";
    public string CoursePlaceholder => IsEnglish ? "E.g. tenth grade" : "Por ejemplo: décimo grado";
    public string SubjectsLabel => IsEnglish ? "Current subjects, separated by commas" : "Materias actuales, separadas por comas";
    public string SubjectsPlaceholder => IsEnglish ? "E.g. Mathematics, Biology" : "Por ejemplo: Matemáticas, Biología";
    public string CareerLabel => IsEnglish ? "Degree" : "Carrera";
    public string UniversityLabel => IsEnglish ? "University (optional)" : "Universidad (opcional)";
    public string UniversityPlaceholder => IsEnglish ? "E.g. Software engineering" : "Por ejemplo: Ingeniería de software";
    public string SemesterLabel => IsEnglish ? "Semester (optional)" : "Semestre (opcional)";
    public string ImportButtonContent => IsEnglish ? "Import" : "Importar";
    public string SemesterLaterHint => IsEnglish ? "You can add your program later from Resources." : "Puedes agregar tu programa más adelante desde Recursos.";
    public string PreferencesSectionTitle => IsEnglish ? "Shape your learning experience" : "Define tu experiencia de aprendizaje";
    public string LearningGoalQuestion => IsEnglish ? "What do you want to learn first?" : "¿Qué quieres aprender primero?";
    public string LearningGoalPlaceholder => IsEnglish ? "E.g. understand React component state" : "Por ejemplo: entender el estado de los componentes de React";
    public string LevelLabel => IsEnglish ? "Current level" : "Nivel actual";
    public string LevelPlaceholder => IsEnglish ? "Select a level" : "Selecciona un nivel";
    public string PreferredLanguageLabel => IsEnglish ? "Preferred language" : "Idioma preferido";
    public string CancelButtonContent => IsEnglish ? "Cancel" : "Cancelar";
    public string BackButtonContent => IsEnglish ? "Back" : "Atrás";

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
            LanguageService.Set(SelectedLanguage ?? "Español");
            ProfileSaved?.Invoke(profile);
        }
        catch (Exception ex)
        {
            ErrorMessage = IsEnglish
                ? $"Could not save learning profile: {ex.Message}"
                : $"No se pudo guardar el perfil de aprendizaje: {ex.Message}";
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
            ErrorMessage = IsEnglish
                ? "Select the profile that best describes your learning context."
                : "Selecciona el perfil que mejor describa tu contexto de aprendizaje.";
            return false;
        }

        if (CurrentStep == 2)
        {
            if (IsStudent && string.IsNullOrWhiteSpace(Course))
            {
                ErrorMessage = IsEnglish
                    ? "Enter the course or level you are currently studying."
                    : "Introduce el curso o nivel que estás estudiando actualmente.";
                return false;
            }

            if (IsStudent && string.IsNullOrWhiteSpace(SubjectsText))
            {
                ErrorMessage = IsEnglish
                    ? "Add at least one current subject."
                    : "Agrega al menos una materia actual.";
                return false;
            }

            if (IsUniversity && string.IsNullOrWhiteSpace(Career))
            {
                ErrorMessage = IsEnglish
                    ? "Enter your university degree."
                    : "Introduce tu carrera universitaria.";
                return false;
            }
        }

        if (CurrentStep == 3)
        {
            if (LearningGoal.Trim().Length < 3)
            {
                ErrorMessage = IsEnglish
                    ? "Describe what you want to learn with at least 3 characters."
                    : "Describe qué quieres aprender con al menos 3 caracteres.";
                return false;
            }

            if (SessionMinutes is < 5 or > 120)
            {
                ErrorMessage = IsEnglish
                    ? "Session duration must be between 5 and 120 minutes."
                    : "La duración de la sesión debe estar entre 5 y 120 minutos.";
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
