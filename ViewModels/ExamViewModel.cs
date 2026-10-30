using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using learn_Assist.Models;
using learn_Assist.Services;

namespace learn_Assist.ViewModels;

/// <summary>
/// ViewModel para generar, cargar y guardar exámenes a partir del programa
/// de estudios y los documentos importados del usuario.
/// </summary>
public partial class ExamViewModel : ViewModelBase
{
    private readonly ExamService _examService;
    private readonly LearningProfile? _learningProfile;
    private readonly IReadOnlyList<UserDocument>? _documents;

    /// <summary>
    /// Crea una instancia con el servicio de IA dado.
    /// </summary>
    /// <param name="aiService">Servicio de IA usado para generar el examen.</param>
    /// <param name="learningProfile">Perfil del aprendiz (opcional).</param>
    /// <param name="documentos">Documentos importados del usuario (opcional).</param>
    public ExamViewModel(IAiService aiService, LearningProfile? learningProfile = null, IReadOnlyList<UserDocument>? documentos = null)
    {
        _examService = new ExamService(aiService ?? throw new ArgumentNullException(nameof(aiService)));
        _learningProfile = learningProfile;
        _documents = documentos;
    }

    /// <summary>
    /// Constructor para tiempo de diseño. Usa el servicio de IA simulado
    /// (<see cref="MockAiService"/>) equivalente a <see cref="ExamService.Instance"/>.
    /// </summary>
    public ExamViewModel() : this(new MockAiService())
    {
    }

    [ObservableProperty]
    public partial int QuestionCount { get; set; } = 5;

    [ObservableProperty]
    public partial QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;

    [ObservableProperty]
    public partial bool UseProgramContent { get; set; } = true;

    [ObservableProperty]
    public partial bool UseDocuments { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludeSolutions { get; set; }

    [ObservableProperty]
    public partial Exam? CurrentExam { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string? ExamTitle { get; set; }

    /// <summary>
    /// Todos los valores del enum <see cref="QuestionType"/>, para binding de enum en XAML.
    /// </summary>
    public IReadOnlyList<QuestionType> QuestionTypes { get; } =
        Enum.GetValues<QuestionType>();

    /// <summary>
    /// Indica si se puede generar un examen en este momento.
    /// </summary>
    public bool CanGenerate =>
        !IsLoading
        && (UseProgramContent || UseDocuments)
        && (!UseProgramContent || !string.IsNullOrWhiteSpace(_learningProfile?.SemesterContent))
        && (!UseDocuments || (_documents?.Any(d => !string.IsNullOrWhiteSpace(d.Content)) ?? false));

    private bool CanSaveExam => CurrentExam is not null && !IsLoading;

    private bool CanLoadExam => !IsLoading;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    public async Task GenerateExamAsync()
    {
        if (IsLoading)
            return;

        ErrorMessage = null;

        if (!UseProgramContent && !UseDocuments)
        {
            ErrorMessage = "Seleccione al menos una fuente: el programa o los documentos.";
            return;
        }

        var level = MapToLearnerLevel(_learningProfile);
        var programContent = UseProgramContent ? _learningProfile?.SemesterContent ?? string.Empty : string.Empty;
        var documents = UseDocuments ? _documents?.ToList() ?? [] : [];

        if (UseProgramContent && string.IsNullOrWhiteSpace(programContent))
        {
            ErrorMessage = "No hay contenido de programa disponible para generar el examen.";
            return;
        }

        if (UseDocuments && documents.Count == 0)
        {
            ErrorMessage = "No hay documentos disponibles para generar el examen.";
            return;
        }

        IsLoading = true;
        try
        {
            var count = Math.Clamp(QuestionCount, 1, 20);
            var exam = await _examService.GenerateExamAsync(
                level,
                programContent,
                documents,
                count,
                QuestionType,
                IncludeSolutions);

            CurrentExam = exam;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo generar el examen: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveExam))]
    public async Task SaveExamAsync()
    {
        if (CurrentExam is null)
            return;

        ErrorMessage = null;
        IsLoading = true;
        try
        {
            CurrentExam.UserId = _learningProfile?.UserId ?? string.Empty;
            CurrentExam.Title = string.IsNullOrWhiteSpace(ExamTitle)
                ? CurrentExam.Title
                : ExamTitle.Trim();

            await _examService.SaveExamAsync(CurrentExam);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo guardar el examen: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanLoadExam))]
    public async Task LoadExamAsync(string examId)
    {
        if (string.IsNullOrWhiteSpace(examId))
            return;

        ErrorMessage = null;
        IsLoading = true;
        try
        {
            var exam = await _examService.LoadExamAsync(examId);
            CurrentExam = exam;
            ErrorMessage = exam is null ? "No se encontró el examen solicitado." : null;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"No se pudo cargar el examen: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Porcentaje de preguntas respondidas correctamente (0-100).
    /// Solo cuenta preguntas con respuesta correcta conocida (modo solución o IA la proporcionó).
    /// </summary>
    public double ScorePercentage =>
        CurrentExam is null || CurrentExam.Questions.Count == 0
            ? 0
            : (double)CurrentExam.Questions.Count(q => q.IsCorrect) / CurrentExam.Questions.Count * 100;

    /// <summary>
    /// Texto formateado del puntaje para mostrar en la UI (ej. "67% — 2 de 3 correctas").
    /// </summary>
    public string ScoreText =>
        CurrentExam is null || CurrentExam.Questions.Count == 0
            ? "Sin preguntas"
            : $"{ScorePercentage:F0}% — {CorrectAnswersCount} de {TotalQuestionsCount} correctas";

    /// <summary>
    /// Número de preguntas respondidas correctamente.
    /// </summary>
    public int CorrectAnswersCount =>
        CurrentExam is null ? 0 : CurrentExam.Questions.Count(q => q.IsCorrect);

    /// <summary>
    /// Número total de preguntas del examen actual.
    /// </summary>
    public int TotalQuestionsCount =>
        CurrentExam is null ? 0 : CurrentExam.Questions.Count;

    /// <summary>
    /// Indica si todas las preguntas han sido respondidas.
    /// </summary>
    public bool AllQuestionsAnswered =>
        CurrentExam is null || CurrentExam.Questions.Count == 0
            ? false
            : CurrentExam.Questions.All(q => q.IsAnswered);

    [RelayCommand]
    public void Clear()
    {
        CurrentExam = null;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void VerifyExam()
    {
        // La verificación es reactiva: IsCorrect e IsIncorrect son propiedades computadas
        // que se evalúan automáticamente cuando SelectedOptionIndex o UserAnswer cambian.
        // Este comando existe para notificar al usuario que se han evaluado las respuestas.
        ErrorMessage = CorrectAnswersCount == TotalQuestionsCount
            ? $"¡Excelente! Respondiste correctamente todas las {TotalQuestionsCount} preguntas."
            : $"Llevas {CorrectAnswersCount} de {TotalQuestionsCount} respuestas correctas.";
    }

    private static LearnerLevel MapToLearnerLevel(LearningProfile? profile) =>
        profile?.ProfileType == LearningProfileType.University
            ? LearnerLevel.University
            : LearnerLevel.Student;

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGenerate));
        GenerateExamCommand.NotifyCanExecuteChanged();
        SaveExamCommand.NotifyCanExecuteChanged();
        LoadExamCommand.NotifyCanExecuteChanged();
    }

    partial void OnUseProgramContentChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGenerate));
        GenerateExamCommand.NotifyCanExecuteChanged();
    }

    partial void OnUseDocumentsChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGenerate));
        GenerateExamCommand.NotifyCanExecuteChanged();
    }

    private Exam? _previousExam;

    partial void OnCurrentExamChanged(Exam? value)
    {
        // Desuscribirse de las preguntas del examen anterior para evitar fugas de memoria
        if (_previousExam is not null)
        {
            foreach (var q in _previousExam.Questions)
                q.PropertyChanged -= OnQuestionPropertyChanged;
        }

        // Suscribirse a las preguntas del nuevo examen
        if (value is not null)
        {
            foreach (var q in value.Questions)
                q.PropertyChanged += OnQuestionPropertyChanged;
        }

        _previousExam = value;

        SaveExamCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(ScorePercentage));
        OnPropertyChanged(nameof(ScoreText));
        OnPropertyChanged(nameof(CorrectAnswersCount));
        OnPropertyChanged(nameof(TotalQuestionsCount));
        OnPropertyChanged(nameof(AllQuestionsAnswered));
    }

    private void OnQuestionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Cuando cambia SelectedOptionIndex o UserAnswer de cualquier pregunta,
        // notificar que las propiedades computadas del ViewModel cambiaron.
        if (e.PropertyName is nameof(ExamQuestion.SelectedOptionIndex)
            or nameof(ExamQuestion.UserAnswer)
            or nameof(ExamQuestion.IsCorrect)
            or nameof(ExamQuestion.IsIncorrect)
            or nameof(ExamQuestion.IsAnswered))
        {
            OnPropertyChanged(nameof(ScorePercentage));
            OnPropertyChanged(nameof(ScoreText));
            OnPropertyChanged(nameof(CorrectAnswersCount));
            OnPropertyChanged(nameof(TotalQuestionsCount));
            OnPropertyChanged(nameof(AllQuestionsAnswered));
        }
    }
}
