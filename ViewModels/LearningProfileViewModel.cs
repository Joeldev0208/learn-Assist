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
            IsPersonal = true;
    }

    public bool IsEditing => !_isRequired;
    public bool IsFirstStep => CurrentStep == 1;
    public bool IsDetailsStep => CurrentStep == 2;
    public bool IsPreferencesStep => CurrentStep == 3;
    public bool IsLastStep => CurrentStep == 3;
    public bool IsBackVisible => CurrentStep > 1;
    public string NextButtonText => IsLastStep ? "Save profile" : "Next";
    public string StepLabel => $"Step {CurrentStep} of 3";
    public string WindowTitle => IsEditing ? "Edit learning profile" : "Set up your learning profile";
    public string WindowDescription => IsEditing
        ? "Update the context Learn-Assist uses to support your learning."
        : "Tell Learn-Assist how you want to learn before starting a conversation.";

    public ObservableCollection<string> Levels { get; } =
        ["Beginner", "Intermediate", "Advanced"];

    public ObservableCollection<string> Languages { get; } =
        ["Español", "English"];

    [ObservableProperty]
    public partial int CurrentStep { get; set; } = 1;

    [ObservableProperty]
    public partial bool IsStudent { get; set; }

    [ObservableProperty]
    public partial bool IsUniversity { get; set; }

    [ObservableProperty]
    public partial bool IsProfessional { get; set; }

    [ObservableProperty]
    public partial bool IsPersonal { get; set; }

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
    public partial string ProfessionalArea { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? SelectedLanguage { get; set; } = "Español";

    [ObservableProperty]
    public partial int SessionMinutes { get; set; } = 15;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    public event Action<LearningProfile>? ProfileSaved;
    public event Action? Cancelled;

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

    partial void OnIsProfessionalChanged(bool value)
    {
        if (value)
            SelectOnly(LearningProfileType.Professional);
    }

    partial void OnIsPersonalChanged(bool value)
    {
        if (value)
            SelectOnly(LearningProfileType.Personal);
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
            ErrorMessage = $"Could not save the learning profile: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    private bool ValidateCurrentStep()
    {
        if (CurrentStep == 1 && !IsStudent && !IsUniversity && !IsProfessional && !IsPersonal)
        {
            ErrorMessage = "Select the profile that best describes your learning context.";
            return false;
        }

        if (CurrentStep == 2)
        {
            if (IsStudent && string.IsNullOrWhiteSpace(Course))
            {
                ErrorMessage = "Enter the course or level you are currently studying.";
                return false;
            }

            if (IsStudent && string.IsNullOrWhiteSpace(SubjectsText))
            {
                ErrorMessage = "Add at least one current subject.";
                return false;
            }

            if (IsUniversity && string.IsNullOrWhiteSpace(Career))
            {
                ErrorMessage = "Enter your university career.";
                return false;
            }

            if (IsProfessional && string.IsNullOrWhiteSpace(ProfessionalArea))
            {
                ErrorMessage = "Enter the professional area you work in.";
                return false;
            }
        }

        if (CurrentStep == 3)
        {
            if (LearningGoal.Trim().Length < 3)
            {
                ErrorMessage = "Describe what you want to learn in at least 3 characters.";
                return false;
            }

            if (SessionMinutes is < 5 or > 120)
            {
                ErrorMessage = "Session duration must be between 5 and 120 minutes.";
                return false;
            }
        }

        return true;
    }

    private LearningProfile BuildProfile()
    {
        var profileType = IsStudent
            ? LearningProfileType.Student
            : IsUniversity
                ? LearningProfileType.University
                : IsProfessional
                    ? LearningProfileType.Professional
                    : LearningProfileType.Personal;

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
            ProfessionalArea = ProfessionalArea.Trim(),
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
        ProfessionalArea = profile.ProfessionalArea;
        SelectedLanguage = profile.PreferredLanguage == "en" ? "English" : "Español";
        SessionMinutes = profile.SessionMinutes;

        switch (profile.ProfileType)
        {
            case LearningProfileType.Student:
                IsStudent = true;
                break;
            case LearningProfileType.University:
                IsUniversity = true;
                break;
            case LearningProfileType.Professional:
                IsProfessional = true;
                break;
            default:
                IsPersonal = true;
                break;
        }
    }

    private void SelectOnly(LearningProfileType selected)
    {
        if (selected != LearningProfileType.Student)
            IsStudent = false;
        if (selected != LearningProfileType.University)
            IsUniversity = false;
        if (selected != LearningProfileType.Professional)
            IsProfessional = false;
        if (selected != LearningProfileType.Personal)
            IsPersonal = false;
    }
}
